"""Refresh local research inputs from an updated installation, preserving the old set."""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import shutil

from prepare_client import extract_installation, extract_catalogue, write_json

ROOT = Path(__file__).resolve().parent.parent


def version(directory):
    data = json.loads((directory / 'version').read_text(encoding='utf-8-sig'))
    versions = list(data['Versions'])
    if len(versions) != 1:
        raise ValueError('Ambiguous client version')
    return versions[0]


def workspace_path(path):
    resolved = path.resolve()
    if resolved == ROOT or ROOT not in resolved.parents:
        raise ValueError('Move target is outside the project workspace: ' + str(resolved))
    return resolved


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('installation', type=Path)
    args = parser.parse_args()
    current = workspace_path(ROOT / 'client-inspection')
    old_version = version(current)
    new_version = version(args.installation)
    if old_version == new_version:
        raise ValueError('This version is already imported: ' + new_version)
    stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    staging = workspace_path(ROOT / 'data' / ('client-refresh-' + stamp))
    baseline = workspace_path(ROOT / 'data/client-snapshots' / (old_version + '-' + stamp))
    baseline.mkdir(parents=True, exist_ok=False)
    manifest = extract_installation(args.installation, staging)
    # Finish catalogue/schema-independent validation before changing active inputs.
    extract_catalogue(staging / 'cards.mtga', staging / 'catalogue.json')
    old_manifest = json.loads((current / 'manifest.json').read_text(encoding='utf-8'))
    old = {e['file']: e for e in old_manifest['files']}
    new = {e['file']: e for e in manifest['files']}
    report = {'oldVersion': old_version, 'newVersion': new_version, 'baseline': str(baseline),
              'added': sorted(new.keys() - old.keys()), 'removed': sorted(old.keys() - new.keys()),
              'changed': sorted(k for k in old.keys() & new.keys() if old[k]['sha256'] != new[k]['sha256']),
              'unchanged': sorted(k for k in old.keys() & new.keys() if old[k]['sha256'] == new[k]['sha256'])}
    if (ROOT / 'analysis/schemas').exists():
        shutil.copytree(ROOT / 'analysis/schemas', baseline / 'schemas')
    for name in ('catalogue.json', 'catalogue-metadata.json', 'forge-coverage.json'):
        source = ROOT / 'data' / name
        if source.exists():
            shutil.copy2(source, baseline / name)
    # Both absolute move targets were checked above to stay inside this workspace.
    current.rename(workspace_path(baseline / 'client-inspection'))
    staging.rename(current)
    for name in ('catalogue.json', 'catalogue-metadata.json'):
        shutil.copy2(current / name, ROOT / 'data' / name)
    write_json(ROOT / 'data/client-update-report.json', report)
    print(json.dumps({k: report[k] for k in ('oldVersion', 'newVersion', 'baseline')}))
    print('Changed:', len(report['changed']), 'added:', len(report['added']), 'removed:', len(report['removed']))


if __name__ == '__main__':
    main()
