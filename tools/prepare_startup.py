"""Reuse installed card data on ordinary starts; fully refresh changed inputs.

Use --full-refresh after manually editing cached artwork, individual card JSON,
or vendor rules in place. Normal imports change the manifest and invalidate it.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
STATE = ROOT / 'data/local-client/startup-cache.json'
PACK = ROOT / 'data/custom-arena-cards'


def read(path):
    try:
        return json.loads(path.read_text(encoding='utf-8-sig'))
    except (OSError, ValueError):
        return {}


def stat(path):
    try:
        s = path.stat()
        return [str(path.resolve()), s.st_size, s.st_mtime_ns]
    except FileNotFoundError:
        return [str(path.resolve()), None, None]


def sources(paths):
    return [[*stat(p), hashlib.sha256(p.read_bytes()).hexdigest()
             if p.is_file() and p.suffix.lower() not in ('.mtga', '.dll', '.jar') else None]
            for p in sorted(set(paths))]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--full-refresh', action='store_true')
    args = parser.parse_args()
    config = read(ROOT / 'data/local-client/configuration-state.json')
    installation = Path(config['installation'])
    raw = installation / 'MTGA_Data/Downloads/Raw'
    databases = list(raw.glob('Raw_CardDatabase_*.mtga'))
    if len(databases) != 1:
        raise ValueError('Expected exactly one installed card database')
    database = databases[0]
    managed = installation / 'MTGA_Data/Managed'
    config_inputs = [ROOT / 'data/local-client/configuration-state.json', installation / 'version']
    pack_inputs = [PACK / n for n in ('manifest.json', 'tokens.json', 'planeswalker-subtypes.json')]
    pack_inputs += [ROOT / n for n in ('tools/install_custom_arena_cards.py', 'tools/custom_tokens.py',
                   'tools/export_rulebreakers.py', 'server/rulebreakers.py')]
    pack_inputs += list((ROOT / 'client-card-extension').glob('*.cs'))
    pack_inputs += list((ROOT / 'client-card-extension').glob('*.csproj'))
    pack_inputs += list((ROOT / 'client-card-extension').glob('*.json'))
    pack_inputs += list((ROOT / 'tools/card-art-patcher').glob('*.cs'))
    pack_inputs += list((ROOT / 'tools/card-art-patcher').glob('*.csproj'))
    if (PACK / 'pending-install.json').exists():
        pack_inputs.append(PACK / 'pending-install.json')
    pack_outputs = [PACK / 'installed.json', PACK / 'emblem-display-abilities.json', database,
                    managed / 'Core.dll', managed / 'SharedClientCore.dll', managed / 'ArcaneForge.CardArt.dll']
    sync_inputs = [database, ROOT / 'engine-bridge/launch.json']
    sync_inputs += [ROOT / n for n in ('tools/sync_card_catalogue.py', 'scripts/prepare_client.py',
                    'scripts/forge_coverage.py', 'tools/update_commander.py', 'tools/update_multiplayer_formats.py',
                    'server/formats.py',
                    'engine-bridge/export_arena_metadata.py', 'tools/export_generated_cards.py', 'tools/export_linked_faces.py')]
    sync_inputs += list((ROOT / 'engine-bridge/cards').glob('*.txt'))
    sync_outputs = [ROOT / 'data' / n for n in ('card-sync-report.json', 'catalogue.json', 'catalogue-metadata.json',
        'forge-coverage.json', 'commander-eligible.json', 'arena-card-metadata.json', 'arena-ability-text.json',
        'arena-activated-abilities.json', 'arena-granted-ability-definitions.json', 'arena-generated-cards.json',
        'arena-linked-faces.json')]
    sync_outputs += [ROOT / 'analysis/deckbuilder-formats.json', ROOT / 'analysis/startup-fixtures.json']
    cache = read(STATE)
    if cache.get('version') != 1:
        cache = {'version': 1}
    for name, script, inputs, outputs, marker in (
        ('Card pack', 'tools/install_custom_arena_cards.py', pack_inputs, pack_outputs, PACK / 'installed.json'),
        ('Card catalogue', 'tools/sync_card_catalogue.py', sync_inputs, sync_outputs, ROOT / 'data/card-sync-report.json')):
        key = script
        signature = {'inputs': sources(inputs + config_inputs), 'outputs': [stat(p) for p in outputs]}
        all_present = all(p.is_file() for p in inputs + config_inputs + outputs)
        cached = cache.get(key) == signature and all_present
        # Adopt an already-completed install/sync once: all its inputs must
        # predate the success marker. Subsequent starts compare exact signatures.
        adopted = key not in cache and all_present and all(
            p.stat().st_mtime_ns <= marker.stat().st_mtime_ns for p in inputs + config_inputs + outputs)
        if not args.full_refresh and (cached or adopted):
            print(name + ' unchanged; using installed data.', flush=True)
        else:
            print(name + ' changed; refreshing once...', flush=True)
            subprocess.run([sys.executable, '-u', str(ROOT / script)], cwd=ROOT, check=True)
        cache[key] = {'inputs': sources(inputs + config_inputs), 'outputs': [stat(p) for p in outputs]}
        STATE.parent.mkdir(parents=True, exist_ok=True)
        temporary = STATE.with_suffix('.tmp')
        temporary.write_text(json.dumps(cache, indent=2) + '\n', encoding='utf8')
        temporary.replace(STATE)


if __name__ == '__main__':
    main()
