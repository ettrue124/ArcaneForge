"""Sync playable Arena printings and names from the installed card database.

Preserve native printing IDs so the Arena client resolves its existing art.
Forge resolution is an inventory check, not proof of gameplay equivalence.
"""
import hashlib
import json
import os
from datetime import datetime, timezone
from pathlib import Path
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from scripts.prepare_client import extract_catalogue, write_json
from scripts.forge_coverage import build_report
from server.engine import Engine, EngineError


def file_sha256(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def files_sha256(paths):
    """Include file identity as well as content, so additions/removals invalidate."""
    digest = hashlib.sha256()
    for path in sorted({p.resolve() for p in paths}, key=lambda p: str(p)):
        entry = json.dumps([str(path), file_sha256(path)], ensure_ascii=False).encode('utf8')
        digest.update(len(entry).to_bytes(8, 'big'))
        digest.update(entry)
    return digest.hexdigest()


def engine_identity(config_path=ROOT / 'engine-bridge/launch.json'):
    """Fingerprint the configured engine's classes, libraries, and card inputs.

    Callers must bypass cached results if profileOverridePresent is true: a
    Java profile file may redirect rules to a directory only Forge resolves.
    """
    config_path = Path(config_path).resolve(strict=True)
    config = json.loads(config_path.read_text(encoding='utf8'))
    command = config['command']
    cwd = Path(config['cwd'])
    cwd = (cwd if cwd.is_absolute() else config_path.parent / cwd).resolve()

    def absolute(value):
        path = Path(value)
        return (path if path.is_absolute() else cwd / path).resolve()

    classpath = None
    for index, argument in enumerate(command):
        if argument in ('-cp', '-classpath', '--class-path') and index + 1 < len(command):
            classpath = command[index + 1]
        elif argument.startswith('--class-path='):
            classpath = argument.split('=', 1)[1]
    if classpath is None or 'ForgeEngineMain' not in command:
        raise ValueError('Expected ForgeEngineMain with an explicit Java classpath in launch.json')
    classes, libraries = [], []
    for entry in classpath.split(os.pathsep):
        path = absolute(entry or '.')
        if path.name == '*':
            if path.parent.is_dir():
                libraries.extend(p for p in path.parent.iterdir() if p.is_file() and p.suffix.lower() == '.jar')
        elif path.is_dir():
            classes.extend(path.rglob('*.class'))
        elif path.is_file():
            libraries.append(path)
    if not classes and not libraries:
        raise ValueError('Forge classpath has no loaded classes or libraries')
    main_index = command.index('ForgeEngineMain')
    if main_index + 1 == len(command):
        raise ValueError('Forge asset directory missing from launch.json')
    assets = absolute(command[main_index + 1])
    rule_directories = [assets / 'res/cardsfolder', assets / 'res/editions']
    environment = {**os.environ, **config.get('env', {})}
    appdata = environment.get('APPDATA')
    if appdata:
        profile = absolute(appdata) / 'Forge'
        rule_directories.extend([profile / 'custom/cards', profile / 'custom/editions'])
    rules = [p for directory in rule_directories if directory.is_dir()
             for p in directory.rglob('*') if p.is_file()]
    additional = list((cwd / 'engine-bridge/cards').glob('*.txt'))
    profile_override = assets / 'forge.profile.properties'
    identity = {
        'launchSha256': file_sha256(config_path),
        'bridgeClassesSha256': files_sha256(classes),
        'librariesSha256': files_sha256(libraries),
        'forgeRulesSha256': files_sha256(rules),
        'additionalRulesSha256': files_sha256(additional),
        # A Java profile override may relocate custom rules. Refresh instead
        # of assuming its effective directories without loading Forge.
        'profileOverridePresent': profile_override.exists(),
    }
    identity['engineSha256'] = hashlib.sha256(json.dumps(identity, sort_keys=True).encode('utf8')).hexdigest()
    return identity


def commander_eligibility(cards, engine, previous):
    names = sorted({card['name'] for card in cards})
    if not isinstance(previous, dict):
        raise ValueError('Existing commander eligibility must be an object')
    old_names = previous.get('names', [])
    if not isinstance(old_names, list) or any(not isinstance(name, str) for name in old_names):
        raise ValueError('Existing commander eligibility has invalid names')
    # Refresh every queried name, including nonlegendary exceptions and newly
    # imported commanders. Keep older results only for names outside this sync.
    retained = set(old_names) - set(names)
    eligible = set(retained)
    for start in range(0, len(names), 200):
        if start % 2000 == 0:
            print(f'Checking Commander eligibility {start:,}/{len(names):,}...', flush=True)
        chunk = names[start:start + 200]
        response = engine.cards(names=chunk, timeout=220)
        if not isinstance(response, dict) or not isinstance(response.get('cards'), list):
            raise EngineError('Invalid Forge commander lookup response')
        received = {}
        for result in response['cards']:
            if (not isinstance(result, dict) or result.get('name') not in chunk
                    or result['name'] in received or type(result.get('supported')) is not bool
                    or type(result.get('canBeCommander')) is not bool):
                raise EngineError('Invalid or duplicate Forge commander lookup row')
            received[result['name']] = result
            if result['supported'] and result['canBeCommander']:
                eligible.add(result['name'])
        if set(received) != set(chunk):
            raise EngineError('Forge commander lookup omitted requested names')
    return {**previous, 'source': 'Installed Forge CardRules.canBeCommander',
            'checkedAt': datetime.now(timezone.utc).isoformat(), 'checkedNames': len(names),
            'retainedUnqueriedNames': len(retained), 'names': sorted(eligible)}


def main():
    state=json.loads((ROOT/'data/local-client/configuration-state.json').read_text(encoding='utf-8-sig'))
    databases=list((Path(state['installation'])/'MTGA_Data/Downloads/Raw').glob('Raw_CardDatabase_*.mtga'))
    if len(databases)!=1:
        raise ValueError('Expected one installed Arena card database')
    database=databases[0]
    digest=file_sha256(database)
    report_path=ROOT/'data/card-sync-report.json'
    previous=json.loads(report_path.read_text(encoding='utf8')) if report_path.exists() else {}
    config_path=ROOT/'engine-bridge/launch.json'
    identity=engine_identity(config_path)
    eligible_path=ROOT/'data/commander-eligible.json'
    output_names=('catalogue.json','catalogue-metadata.json','forge-coverage.json','commander-eligible.json',
                  'arena-card-metadata.json','arena-ability-text.json','arena-activated-abilities.json',
                  'arena-granted-ability-definitions.json','arena-generated-cards.json','arena-linked-faces.json')
    if (previous.get('syncVersion')==2 and previous.get('databaseSha256')==digest
            and previous.get('engineSha256')==identity['engineSha256']
            and not identity['profileOverridePresent']
            and all((ROOT/'data'/name).exists() for name in output_names)
            and previous.get('commanderEligibilitySha256')==file_sha256(eligible_path)):
        print('Installed card catalogue is up to date.')
        return
    catalogue_path=ROOT/'data/catalogue.json'
    old=json.loads(catalogue_path.read_text(encoding='utf8')) if catalogue_path.exists() else []
    # Keep a recoverable copy before replacing local metadata.
    backup=ROOT/'data/card-sync-backups'/digest[:16]
    for name in output_names:
        source=ROOT/'data'/name
        destination=backup/name
        if source.exists() and not destination.exists():
            destination.parent.mkdir(parents=True,exist_ok=True)
            destination.write_bytes(source.read_bytes())
    print('Refreshing installed card names and display data...', flush=True)
    cards=extract_catalogue(database,catalogue_path)
    for script in ('engine-bridge/export_arena_metadata.py','tools/export_generated_cards.py','tools/export_linked_faces.py'):
        subprocess.run([sys.executable,str(ROOT/script),'--database',str(database)],cwd=ROOT,check=True)
    old_eligibility=json.loads(eligible_path.read_text(encoding='utf8')) if eligible_path.exists() else {}
    with Engine.from_config(config_path) as engine:
        print(f'Loading Forge and checking {len(cards):,} card printings...', flush=True)
        coverage=build_report(cards,engine,chunk_size=200,
            progress=lambda done,total: print(f'Checking card rules {done:,}/{total:,}...', flush=True)
                if done % 2000 == 0 or done == total else None)
        print('Card rules inventory complete. Checking Commander eligibility...', flush=True)
        eligibility=commander_eligibility(cards,engine,old_eligibility)
    # Do not replace valid eligibility with a partial lookup result. The helper
    # validates every requested row before this atomic file replacement.
    write_json(eligible_path,eligibility)
    write_json(ROOT/'data/forge-coverage.json',coverage)
    subprocess.run([sys.executable,str(ROOT/'tools/update_commander.py'),'--cached'],cwd=ROOT,check=True)
    old_ids={c['grpId'] for c in old}
    added=[{'grpId':c['grpId'],'name':c['name']} for c in cards if c['grpId'] not in old_ids]
    report={'syncVersion':2,'databaseSha256':digest,**identity,
            'commanderEligibilitySha256':file_sha256(eligible_path),
            'commanderEligibleNames':len(eligibility['names']),
            'printings':len(cards),'uniqueNames':len({c['name'] for c in cards}),
            'added':added,'forge':coverage['summary'],'unsupportedNames':coverage['unsupportedNames'],
            'namesSource':'Installed Arena English localization',
            'art':'Native Arena printing IDs retained; client loads its installed/downloadable art.',
            'rulesEquivalence':'Not verified by name resolution. Unsupported cards require Forge implementations.'}
    write_json(report_path,report)
    print(json.dumps({k:report[k] for k in ('printings','uniqueNames','forge')}))
    print('Added',len(added),'card printings. Unsupported names are listed in data/card-sync-report.json.')


if __name__=='__main__':
    main()
