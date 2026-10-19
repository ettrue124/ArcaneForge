"""Refresh Magic's full Oracle catalogue and prepare compatible local Arena cards.

Downloads one Scryfall bulk export, resolves names through the installed Forge
engine, and preserves native printings. Preparation never changes Arena or the
active pack. --install requires Arena closed; it never stops a match or service.
"""
from __future__ import annotations

import argparse
from collections import Counter
from concurrent.futures import ThreadPoolExecutor, as_completed
from contextlib import contextmanager
from datetime import datetime, timezone
import gzip
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import sqlite3
import subprocess
import sys
import time
from urllib.error import HTTPError, URLError
from urllib.parse import urlparse, urlencode
from urllib.request import Request, urlopen

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from server.engine import Engine, EngineError
from scripts.prepare_client import write_json

WORK = ROOT / 'data/magic-import'
PACK = ROOT / 'data/custom-arena-cards'
BULK_API = 'https://api.scryfall.com/bulk-data/oracle-cards'
HEADERS = {'User-Agent': 'ArcaneForge-LocalCardImporter/1.0',
           'Accept': 'application/json;q=0.9,*/*;q=0.8', 'Accept-Encoding': 'identity'}


def read_json(path, default=None):
    return json.loads(path.read_text(encoding='utf-8-sig')) if path.exists() else default


@contextmanager
def import_lock():
    WORK.mkdir(parents=True, exist_ok=True)
    with (WORK / 'import.lock').open('a+b') as handle:
        handle.seek(0)
        if not handle.read(1):
            handle.write(b'0'); handle.flush()
        handle.seek(0)
        try:
            if os.name == 'nt':
                import msvcrt
                msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(handle, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except OSError as exc:
            raise RuntimeError('Another card import is already running. Let it finish first.') from exc
        try:
            yield
        finally:
            handle.seek(0)
            if os.name == 'nt':
                msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(handle, fcntl.LOCK_UN)


def arena_running():
    if os.name != 'nt':
        return False
    result = subprocess.run(['powershell', '-NoProfile', '-Command',
        'if(Get-Process MTGA -ErrorAction SilentlyContinue){exit 1}'], capture_output=True)
    return result.returncode != 0


def require_arena_closed():
    if arena_running():
        raise RuntimeError('Arena is open. Finish your match and close Arena, then run this again. '
                           'Use --catalog-only to refresh the card list during a match.')


def request(url):
    parsed = urlparse(url)
    host = parsed.hostname or ''
    if parsed.scheme != 'https' or not (host == 'api.scryfall.com' or host.endswith('.scryfall.io')):
        raise ValueError('Unexpected card-data download host: ' + host)
    return urlopen(Request(url, headers=HEADERS), timeout=60)


def download(url, destination, maximum, image=False):
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_suffix(destination.suffix + '.part')
    try:
        # Never repeatedly retry 429 or 403: pause the import for the user.
        with request(url) as response, temporary.open('wb') as stream:
            size = 0
            while chunk := response.read(1024 * 1024):
                size += len(chunk)
                if size > maximum:
                    raise ValueError('Download exceeds expected size limit')
                stream.write(chunk)
        if not size:
            raise ValueError('Empty card-data download')
        if image:
            with temporary.open('rb') as stream:
                if stream.read(3) != b'\xff\xd8\xff':
                    raise ValueError('Artwork response is not a JPEG')
        temporary.replace(destination)
    finally:
        temporary.unlink(missing_ok=True)


def bulk_cards(args):
    index_path = WORK / 'bulk-cache.json'
    previous = read_json(index_path, {})
    cached = WORK / previous.get('filename', 'missing')
    if args.offline:
        if not cached.is_file():
            raise ValueError('No cached Magic bulk catalogue. Run once with internet access.')
        descriptor = previous['descriptor']
    else:
        recent = time.time() - previous.get('checkedAt', 0) < 24 * 60 * 60
        if cached.is_file() and recent and not args.refresh:
            descriptor = previous['descriptor']
        else:
            with request(BULK_API) as response:
                descriptor = json.load(response)
            if descriptor.get('type') != 'oracle_cards':
                raise ValueError('Scryfall returned an unexpected bulk-data type')
            # Scryfall's current API uses compressed JSONL; accept older JSON too.
            url = descriptor.get('jsonl_download_uri') or descriptor.get('download_uri')
            if not isinstance(url, str):
                raise ValueError('Bulk descriptor has no supported download URL')
            filename = Path(urlparse(url).path).name
            if not re.fullmatch(r'[A-Za-z0-9_.-]+', filename):
                raise ValueError('Unexpected bulk filename')
            cached = WORK / filename
            if not cached.is_file():
                print('Downloading the complete Magic card catalogue...', flush=True)
                download(url, cached, 1024 * 1024 * 1024)
            write_json(index_path, {'checkedAt': time.time(), 'filename': filename, 'descriptor': descriptor})
    print('Reading Magic catalogue dated ' + descriptor.get('updated_at', 'unknown') + '...', flush=True)
    opener = gzip.open if cached.name.endswith('.gz') else open
    with opener(cached, 'rt', encoding='utf8') as stream:
        if '.jsonl' in cached.name:
            cards = [json.loads(line) for line in stream if line.strip()]
        else:
            cards = json.load(stream)
    if not isinstance(cards, list) or not cards or any(
        not isinstance(c, dict) or not isinstance(c.get('name'), str) for c in cards):
        raise ValueError('Invalid bulk card catalogue; previous active card pack is unchanged')
    return cards, descriptor


def download_art(url, destination):
    """Retry temporary network failures; never retry rate limits or access blocks."""
    for attempt in range(3):
        try:
            download(url, destination, 16 * 1024 * 1024, True)
            return
        except HTTPError as exc:
            if exc.code < 500 or attempt == 2:
                raise
        except (URLError, TimeoutError, ConnectionError) as exc:
            if attempt == 2:
                raise
        time.sleep(2 ** attempt)


def installed_inventory():
    config = read_json(ROOT / 'data/local-client/configuration-state.json')
    if not config:
        raise ValueError('Configure the local Arena installation before importing cards.')
    paths = list((Path(config['installation']) / 'MTGA_Data/Downloads/Raw').glob('Raw_CardDatabase_*.mtga'))
    if len(paths) != 1:
        raise ValueError('Expected exactly one installed Arena card database')
    active = read_json(PACK / 'manifest.json', [])
    if isinstance(active, dict):
        active = active['cards']
    owned = {r['grpId'] for r in active}
    with sqlite3.connect(paths[0].resolve().as_uri() + '?mode=ro', uri=True) as db:
        locs = dict(db.execute('SELECT LocId,Loc FROM Localizations_enUS ORDER BY Formatted DESC'))
        from tools.install_custom_arena_cards import read_native_enums
        enums = read_native_enums(db)
        native = {}
        for gid, title, fallback in db.execute('SELECT GrpId,TitleId,Order_Title FROM Cards WHERE IsPrimaryCard=1 AND IsToken=0'):
            if gid not in owned:
                native.setdefault(locs.get(title) or fallback, []).append(gid)
        occupied = {
            'grpId': {r[0] for r in db.execute('SELECT GrpId FROM Cards')},
            'artId': {r[0] for r in db.execute('SELECT ArtId FROM Cards')},
            'localization': {r[0] for r in db.execute('SELECT LocId FROM Localizations')},
            'ability': {r[0] for r in db.execute('SELECT Id FROM Abilities')},
            'templates': {r[0] for r in db.execute('SELECT DISTINCT Types FROM Cards WHERE IsPrimaryCard=1 AND IsToken=0 AND LinkedFaceType=0 AND GrpId<1800000')},
        }
    return native, active, enums, occupied


def resolve_names(names):
    from tools.sync_card_catalogue import engine_identity
    identity = engine_identity()
    fingerprint = identity['engineSha256']
    path = WORK / 'forge-names.json'
    cache = read_json(path, {})
    rows = cache.get('cards', {}) if cache.get('fingerprint') == fingerprint and not identity['profileOverridePresent'] else {}
    missing = sorted(set(names) - rows.keys())
    if missing:
        print(f'Checking {len(missing):,} card names against the installed Forge rules...', flush=True)
        with Engine.from_config(ROOT / 'engine-bridge/launch.json') as engine:
            for start in range(0, len(missing), 200):
                chunk = missing[start:start + 200]
                reply = engine.cards(names=chunk, timeout=180)
                received = {r['name']: r for r in reply.get('cards', [])}
                if set(received) != set(chunk) or any(type(r.get('supported')) is not bool for r in received.values()):
                    raise ValueError('Forge omitted or returned invalid card-name results')
                rows.update(received)
                if start % 2000 == 0 or start + 200 >= len(missing):
                    write_json(path, {'fingerprint': fingerprint, 'cards': rows})
                    print(f'Checked {min(start + 200, len(missing)):,}/{len(missing):,} names', flush=True)
    return rows


def requested_names(path, cards):
    if path is None:
        return None, []
    canonical = {c['name'].casefold(): c['name'] for c in cards}
    result, unknown = set(), []
    for original in path.read_text(encoding='utf-8-sig').splitlines():
        line = original.strip()
        if not line or line.startswith(('#', '//')) or line.casefold() in {
            'deck', 'commander', 'sideboard', 'companion', 'maybeboard'}:
            continue
        line = re.sub(r'^(?:SB:\s*)?\d+\s*x?\s+', '', line, flags=re.I)
        line = re.sub(r'\s+\([A-Za-z0-9]+\)\s+\S+\s*$', '', line)
        name = canonical.get(line.casefold())
        if name:
            result.add(name)
        else:
            unknown.append(original)
    return result, unknown


def allocate(oracle_id, registry, occupied):
    if oracle_id in registry['cards']:
        return registry['cards'][oracle_id]
    while registry['nextIndex'] < 100_000:
        index = registry['nextIndex']; registry['nextIndex'] += 1
        ids = {'grpId': 2_000_000 + index, 'artId': 10_000_000 + index,
               'localizationBase': 30_000_000 + index * 100, 'abilityBase': 3_000_000 + index * 32}
        if ids['grpId'] in occupied['grpId'] or ids['artId'] in occupied['artId']:
            continue
        if any(ids['localizationBase'] + i in occupied['localization'] for i in range(100)):
            continue
        if any(ids['abilityBase'] + i in occupied['ability'] for i in range(32)):
            continue
        registry['cards'][oracle_id] = ids
        return ids
    raise ValueError('Local card ID allocation range is exhausted')


def write_report(report):
    write_json(WORK / 'report.json', report)
    counts = report['counts']
    labels = {'ready': 'Eligible entries to prepare', 'needs_support': 'Entries needing additional support',
              'arena_existing': 'Entries already in Arena', 'pack_existing': 'Entries in the existing custom pack',
              'prepared': 'Entries prepared for installation', 'download_failed': 'Artwork downloads needing another attempt'}
    text = ['Magic card import', '', 'Source updated: ' + report['sourceUpdatedAt'],
            'Result: ' + report['phase'], '', *[f'{labels.get(k, k)}: {v:,}' for k, v in counts.items()], '',
            'Native cards retain their existing printings. Imported cards use Forge rules.',
            'Name resolution does not prove every card interaction or animation works.',
            'Unsupported layouts and missing rules are listed below; they are not installed.', '',
            'Next: ' + report['nextStep'], '']
    for row in report['cards']:
        if row['status'] not in ('arena_existing', 'pack_existing', 'ready', 'prepared'):
            text.append(row['name'] + ': ' + '; '.join(row.get('reasons', [row['status']])))
    for name in report.get('unmatchedNames', []):
        text.append('Name not found: ' + name)
    (WORK / 'report.txt').write_text('\n'.join(text) + '\n', encoding='utf8')


def valid_paper_cache(card):
    cache=WORK/'paper-printings'/(card['oracle_id']+'.json')
    if not cache.exists():return False
    value=read_json(cache)
    return not value.get('noPaperPrinting') or time.time()-value.get('checkedAt',0)<86400


def paper_printing(card, offline=False):
    """Oracle bulk may select an online-only reprint of a paper card."""
    if 'paper' in card.get('games', []) or not card.get('oracle_id'):
        return card
    cache=WORK/'paper-printings'/(card['oracle_id']+'.json')
    if cache.exists() and (offline or valid_paper_cache(card)):
        candidate=read_json(cache)
        if candidate.get('noPaperPrinting') is True and candidate.get('oracle_id')==card['oracle_id']:
            return card
    elif offline:
        return card
    else:
        query=urlencode({'q':'oracleid:'+card['oracle_id']+' game:paper','order':'released','dir':'desc'})
        time.sleep(.1)
        try:
            with request('https://api.scryfall.com/cards/search?'+query) as response:
                rows=json.load(response).get('data', [])
        except HTTPError as error:
            if error.code==404:return card
            raise
        candidate=next((row for row in rows if row.get('oracle_id')==card['oracle_id']
            and row.get('name')==card['name'] and 'paper' in row.get('games',[])),None)
        if candidate is None:return card
        write_json(cache,candidate)
    if candidate.get('oracle_id')!=card['oracle_id'] or candidate.get('name')!=card['name'] or 'paper' not in candidate.get('games',[]):
        raise ValueError('Cached paper printing has a different card identity')
    return candidate


def cache_paper_printings(cards):
    """Group exact Oracle identities to avoid one request per older reprint."""
    missing={c['oracle_id']:c for c in cards if c.get('oracle_id') and 'paper' not in c.get('games',[])
        and not valid_paper_cache(c)}
    identities=list(missing)
    for start in range(0,len(identities),10):
        batch=identities[start:start+10]
        query=urlencode({'q':'game:paper ('+' or '.join('oracleid:'+oid for oid in batch)+')',
            'unique':'cards','order':'name'})
        time.sleep(.1)
        try:
            with request('https://api.scryfall.com/cards/search?'+query) as response:
                payload=json.load(response)
        except HTTPError as error:
            if error.code!=404:raise
            payload={'data':[]}
        if payload.get('has_more'):
            raise ValueError('Unexpected extra pages in exact paper-printing lookup')
        found={}
        for row in payload.get('data',[]):
            oid=row.get('oracle_id')
            if oid not in missing or oid not in batch or row.get('name')!=missing[oid]['name'] or 'paper' not in row.get('games',[]):
                raise ValueError('Paper-printing batch returned an unexpected card identity')
            found[oid]=row
        for oid in batch:
            write_json(WORK/'paper-printings'/(oid+'.json'),found.get(oid,{'oracle_id':oid,'noPaperPrinting':True,'checkedAt':time.time()}))
        print(f'Checking paper printings {min(start+10,len(identities)):,}/{len(identities):,}...',flush=True)


def run(args):
    if args.install and not args.catalog_only:
        require_arena_closed()
    cards, descriptor = bulk_cards(args)
    native, active, enums, occupied = installed_inventory()
    from tools.install_custom_arena_cards import metadata_support_issues, native_type_key
    selected, unknown = requested_names(args.names, cards)
    # Keep all existing pack cards, including hand-curated flavor-name aliases.
    legacy_names = set()
    managed = {}
    for record in active:
        if record.get('importer') == 'magic-bulk-v1':
            managed[record['oracleId']] = record
        else:
            legacy_names.add(record['name'])
            metadata = read_json(ROOT / record['metadata'], {})
            if metadata.get('name'):
                legacy_names.add(metadata['name'])
    names = [c['name'] for c in cards if selected is None or c['name'] in selected]
    resolutions = resolve_names(names)
    if not args.offline:
        cache_paper_printings([c for c in cards if (selected is None or c['name'] in selected)
            and c['name'] not in native and c['name'] not in legacy_names])
    rows, eligible = [], []
    for card in sorted(cards, key=lambda c: (c['name'], c.get('oracle_id', ''))):
        if selected is not None and card['name'] not in selected:
            continue
        name = card['name']
        row = {'name': name, 'oracleId': card.get('oracle_id'), 'layout': card.get('layout'),
               'source': card.get('scryfall_uri'), 'status': 'ready', 'reasons': []}
        if name in native:
            row.update(status='arena_existing', grpIds=native[name])
        elif name in legacy_names:
            row['status'] = 'pack_existing'
        else:
            card=paper_printing(card,args.offline)
            row['source']=card.get('scryfall_uri')
            reasons = metadata_support_issues(card, enums)
            if not reasons and native_type_key(card, enums) not in occupied['templates'] and 'Planeswalker' not in card.get('type_line','').split(' — ')[0].split():
                reasons.append('No native single-face template for this card type combination')
            if len(card.get('oracle_text', '').split('\n')) > 32:
                reasons.append('Oracle text exceeds the supported 32 ability slots')
            if not card.get('oracle_id'):
                reasons.append('No Oracle identity')
            if 'paper' not in card.get('games', []):
                reasons.append('Not a paper Magic card; requires a digital-card adapter')
            if not card.get('image_uris', {}).get('art_crop'):
                reasons.append('No single-face artwork crop available')
            resolved = resolutions.get(name, {})
            if not resolved.get('supported'):
                reasons.append('No installed Forge rules for this card')
            elif resolved.get('forgeName') != name:
                reasons.append('Forge resolves to a different card name; manual mapping required')
            row['reasons'] = reasons
            if reasons:
                row['status'] = 'needs_support'
            else:
                eligible.append((card, row))
        rows.append(row)
    report = {'generatedAt': datetime.now(timezone.utc).isoformat(), 'source': BULK_API,
              'sourceUpdatedAt': descriptor.get('updated_at', 'unknown'), 'phase': 'catalogue refreshed',
              'bulkEntries': len(cards), 'cards': rows, 'unmatchedNames': unknown,
              'counts': dict(Counter(r['status'] for r in rows)),
              'nextStep': 'Close Arena and run Import-All-Magic-Cards.bat to install compatible missing cards.'}
    write_report(report)
    print(f'Catalogue: {len(cards):,} entries; {len(eligible):,} compatible missing/managed cards.', flush=True)
    if args.catalog_only:
        print('Catalogue refreshed. Arena, the active card pack, and live services are unchanged.')
        print('Details: ' + str(WORK / 'report.txt'))
        return 0
    registry_path = WORK / 'identities.json'
    registry = read_json(registry_path, {'nextIndex': 0, 'cards': {}})
    prepared = []
    for card, row in eligible:
        previous = managed.get(card['oracle_id'])
        ids = ({k: previous[k] for k in ('grpId', 'artId', 'localizationBase', 'abilityBase')}
               if previous else allocate(card['oracle_id'], registry, occupied))
        # Immutable payload paths keep preparation from changing files that
        # the currently active manifest references.
        metadata_hash = hashlib.sha256(json.dumps(card, sort_keys=True).encode()).hexdigest()[:16]
        art_hash = hashlib.sha256(card['image_uris']['art_crop'].encode()).hexdigest()[:16]
        metadata_path = WORK / 'cards' / f"{ids['grpId']}-{metadata_hash}.json"
        art_path = WORK / 'art' / f"{ids['artId']}-{art_hash}.jpg"
        write_json(metadata_path, card)
        record = {**ids, 'name': card['name'], 'oracleId': card['oracle_id'], 'importer': 'magic-bulk-v1',
                  'metadata': metadata_path.relative_to(ROOT).as_posix(),
                  'art': art_path.relative_to(ROOT).as_posix(), 'source': card.get('scryfall_uri', ''),
                  'artSource': card['image_uris']['art_crop']}
        prepared.append((record, row))
    # Persist allocated identities before downloads so interruptions cannot renumber decks.
    write_json(registry_path, registry)
    art_sources = read_json(WORK / 'art-sources.json', {})
    downloads = [(r, row) for r, row in prepared if not (ROOT / r['art']).is_file()
                 or art_sources.get(str(r['artId'])) != r['artSource']]
    if args.offline and downloads:
        raise ValueError(f'{len(downloads):,} artwork files are not cached. Run without --offline to download them.')
    print(f'Downloading {len(downloads):,} missing artwork files; existing downloads are reused...', flush=True)
    errors = []
    with ThreadPoolExecutor(max_workers=4) as pool:
        futures = {pool.submit(download_art, r['artSource'], ROOT / r['art']): (r, row)
                   for r, row in downloads}
        for done, future in enumerate(as_completed(futures), 1):
            record, row = futures[future]
            try:
                future.result()
                art_sources[str(record['artId'])] = record['artSource']
            except (OSError, ValueError) as exc:
                row.update(status='download_failed', reasons=[str(exc)])
                errors.append(row['name'])
                if isinstance(exc, HTTPError) and exc.code in (403, 429):
                    for pending in futures:
                        pending.cancel()
                    write_json(WORK / 'art-sources.json', art_sources)
                    raise RuntimeError('Artwork service requested a pause. Re-run later; completed downloads are retained.') from exc
            if done % 100 == 0 or done == len(downloads):
                write_json(WORK / 'art-sources.json', art_sources)
                print(f'Artwork {done:,}/{len(downloads):,}', flush=True)
    write_json(WORK / 'art-sources.json', art_sources)
    for _, row in prepared:
        if row['status'] == 'ready':
            row['status'] = 'prepared'
    report['counts'] = dict(Counter(r['status'] for r in rows))
    report['phase'] = 'artwork incomplete' if errors else 'prepared; not installed'
    write_report(report)
    if errors:
        print(f'{len(errors)} cards have unavailable artwork and will be skipped for this run. Re-run to retry them.', flush=True)
    merged = {r['grpId']: r for r in active}
    for record, row in prepared:
        if row['status'] == 'prepared':
            merged[record['grpId']] = record
    candidate = WORK / 'manifest.json'
    write_json(candidate, sorted(merged.values(), key=lambda r: r['grpId']))
    installer = [sys.executable, str(ROOT / 'tools/install_custom_arena_cards.py'), '--manifest', str(candidate)]
    if args.install:
        require_arena_closed()
        # Installation includes the same complete preflight before any writes.
        # Avoid hashing every card/artwork file twice during one import.
        subprocess.run(installer, cwd=ROOT, check=True)
        current = PACK / 'manifest.json'
        if current.exists():
            backup = WORK / 'manifest-backups' / (hashlib.sha256(current.read_bytes()).hexdigest() + '.json')
            backup.parent.mkdir(parents=True, exist_ok=True)
            if not backup.exists():
                shutil.copy2(current, backup)
        write_json(current, read_json(candidate))
        subprocess.run([sys.executable, str(ROOT / 'tools/sync_card_catalogue.py')], cwd=ROOT, check=True)
        report['phase'] = 'installed; server reload needed' + ('; some artwork skipped' if errors else '')
        report['nextStep'] = 'Restart your local server, then reopen Arena. Existing services were not stopped.'
        write_report(report)
    else:
        subprocess.run(installer + ['--prepare-only'], cwd=ROOT, check=True)
    print(report['nextStep'] if args.install else 'Prepared. Close Arena and rerun with --install to load the cards.')
    print('Report: ' + str(WORK / 'report.txt'))
    return 0


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true', help='Install the prepared pack; Arena must be closed')
    parser.add_argument('--catalog-only', action='store_true', help='Refresh availability report only; safe during a match')
    parser.add_argument('--refresh', action='store_true', help='Check for a newer bulk export now')
    parser.add_argument('--offline', action='store_true', help='Use cached card data and artwork')
    parser.add_argument('--names', type=Path, help='Optional deck list or one card name per line')
    args = parser.parse_args()
    try:
        with import_lock():
            return run(args)
    except (OSError, ValueError, RuntimeError, EngineError, subprocess.CalledProcessError) as exc:
        print('Card import stopped: ' + str(exc), file=sys.stderr)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
