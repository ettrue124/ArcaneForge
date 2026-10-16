"""Inspect a supplied Arena ZIP offline; never execute client code or contact services."""
import argparse
from contextlib import closing
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import sqlite3
import tempfile
import zipfile


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(mode='w', encoding='utf-8', dir=path.parent,
                                     delete=False, suffix='.tmp') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
        temporary = stream.name
    os.replace(temporary, path)


def selection(name):
    normalized = name.replace('\\', '/')
    parts = PurePosixPath(normalized).parts
    basename = parts[-1] if parts else ''
    if '/Managed/' in normalized and basename.lower().endswith('.dll'):
        return basename
    if basename == 'version' and len(parts) == 2:
        return 'version'
    if '/Downloads/Raw/' in normalized and basename.startswith('Raw_CardDatabase_') and basename.endswith('.mtga'):
        return 'cards.mtga'
    return None


def extract_client(archive, destination):
    destination = Path(destination)
    destination.mkdir(parents=True, exist_ok=True)
    entries = []
    seen = set()
    with zipfile.ZipFile(archive) as package:
        for entry in package.infolist():
            target = selection(entry.filename)
            if target is None:
                continue
            parts = PurePosixPath(entry.filename.replace('\\', '/')).parts
            if '..' in parts or entry.filename.startswith(('/', '\\')) or any(':' in p for p in parts):
                raise ValueError('Unsafe selected archive path')
            if not re.fullmatch(r'[A-Za-z0-9_.-]+', target):
                raise ValueError('Unsafe selected basename')
            if target.casefold() in seen:
                raise ValueError('Ambiguous duplicate selected basename: ' + target)
            seen.add(target.casefold())
            if entry.file_size > 512 * 1024 * 1024:
                raise ValueError('Selected entry exceeds extraction size limit')
            digest = hashlib.sha256()
            output = destination / target
            with tempfile.NamedTemporaryFile(dir=destination, delete=False, suffix='.tmp') as stream:
                temporary = Path(stream.name)
                try:
                    with package.open(entry) as source:
                        while chunk := source.read(1024 * 1024):
                            digest.update(chunk)
                            stream.write(chunk)
                except BaseException:
                    stream.close()
                    temporary.unlink(missing_ok=True)
                    raise
            os.replace(temporary, output)
            entries.append({'archivePath': entry.filename, 'file': target,
                            'bytes': entry.file_size, 'sha256': digest.hexdigest()})
    if 'cards.mtga' not in seen:
        raise ValueError('No Arena card database found in archive')
    manifest = {'sourceArchive': Path(archive).name, 'files': entries}
    write_json(destination / 'manifest.json', manifest)
    return manifest


def numeric_list(value):
    return [int(v) for v in re.findall(r'\d+', str(value or ''))]


def extract_installation(installation, destination):
    installation = Path(installation).resolve(strict=True)
    destination = Path(destination).resolve()
    if destination == installation or installation in destination.parents:
        raise ValueError('Output must be outside the installed game directory')
    managed = installation / 'MTGA_Data/Managed'
    databases = list((installation / 'MTGA_Data/Downloads/Raw').glob('Raw_CardDatabase_*.mtga'))
    if len(databases) != 1:
        raise ValueError('Expected exactly one installed card database; found ' + str(len(databases)))
    sources = [(p, p.name) for p in managed.glob('*.dll')]
    if not sources or not (installation / 'version').is_file():
        raise ValueError('Installation is missing managed assemblies or version metadata')
    sources += [(installation / 'version', 'version'), (databases[0], 'cards.mtga')]
    destination.mkdir(parents=True, exist_ok=True)
    entries = []
    for source, name in sources:
        target = destination / name
        shutil.copy2(source, target)
        with target.open('rb') as stream:
            digest = hashlib.file_digest(stream, 'sha256').hexdigest()
        entries.append({'archivePath': source.relative_to(installation).as_posix(),
                        'file': name, 'bytes': target.stat().st_size, 'sha256': digest})
    manifest = {'sourceInstallation': str(installation), 'files': entries}
    write_json(destination / 'manifest.json', manifest)
    return manifest


def extract_catalogue(database, output):
    uri = Path(database).resolve().as_uri() + '?mode=ro'
    with closing(sqlite3.connect(uri, uri=True)) as connection:
        connection.row_factory = sqlite3.Row
        # Names may have only Formatted=1, including basic lands in this client.
        localized = {}
        for row in connection.execute('SELECT LocId, Loc FROM Localizations_enUS ORDER BY Formatted DESC'):
            if row['Loc']:
                localized[row['LocId']] = row['Loc']
        enums = {}
        for row in connection.execute('SELECT Type, Value, LocId FROM Enums'):
            enums.setdefault(row['Type'], {})[row['Value']] = localized.get(row['LocId'], str(row['Value']))
        def labels(kind, value):
            return [enums.get(kind, {}).get(v, str(v)) for v in numeric_list(value)]
        basic_values = {key for key, value in enums.get('SuperType', {}).items() if value == 'Basic'}
        land_values = {key for key, value in enums.get('CardType', {}).items() if value == 'Land'}
        cards = []
        for row in connection.execute('SELECT * FROM Cards WHERE IsPrimaryCard=1 AND IsToken=0 ORDER BY GrpId'):
            raw = dict(row)
            is_basic_land = bool(basic_values.intersection(numeric_list(row['Supertypes'])) and
                                 land_values.intersection(numeric_list(row['Types'])))
            name = localized.get(row['TitleId']) or row['Order_Title']
            if not name:
                raise ValueError('Missing localized card name: ' + str(row['GrpId']))
            cards.append({'grpId': row['GrpId'], 'name': name,
                          'cardLimit': 0 if is_basic_land else (row['AlternateDeckLimit'] or 4),
                          'isBasicLand': is_basic_land,
                          'types': labels('CardType', row['Types']),
                          'supertypes': labels('SuperType', row['Supertypes']),
                          'subtypes': labels('SubType', row['Subtypes']),
                          'manaCost': row['OldSchoolManaText'], 'power': row['Power'],
                          'toughness': row['Toughness'], 'arenaMetadata': raw})
        versions = dict(connection.execute('SELECT Type, Version FROM Versions'))
    write_json(output, cards)
    write_json(Path(output).with_name('catalogue-metadata.json'), {
        'versions': versions, 'count': len(cards), 'basicLandCount': sum(c['isBasicLand'] for c in cards),
        'rulesEngineIncluded': False,
        'notes': 'Local client metadata; cardLimit=0 means unlimited basic lands. AlternateDeckLimit=0 means ordinary four-copy limit. Format legality and executable card effects are not provided.'})
    return cards


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('archive', nargs='?', type=Path, default=Path('MTGA.zip'))
    parser.add_argument('--destination', type=Path, default=Path('client-inspection'))
    parser.add_argument('--output', type=Path, default=Path('data/catalogue.json'))
    parser.add_argument('--catalogue-only', action='store_true', help='Use already extracted cards.mtga')
    args = parser.parse_args()
    if not args.catalogue_only:
        manifest = (extract_installation(args.archive, args.destination) if args.archive.is_dir()
                    else extract_client(args.archive, args.destination))
        print('Extracted and hashed', len(manifest['files']), 'client files')
    cards = extract_catalogue(args.destination / 'cards.mtga', args.output)
    print('Exported', len(cards), 'primary non-token card records to', args.output)


if __name__ == '__main__':
    main()
