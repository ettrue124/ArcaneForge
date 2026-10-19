"""Prepare/install reversible local Arena display records with stable manifest IDs.

New records require localizationBase and abilityBase. Metadata preflight rejects
unsupported layouts before mutation. Forge gameplay support is a separate check.
"""
import argparse
import bisect
from contextlib import contextmanager, closing
from datetime import datetime, timezone
import hashlib
import json
import math
import os
from pathlib import Path
import re
import shutil
import sqlite3
import subprocess

ROOT = Path(__file__).resolve().parents[1]
PACK = ROOT / 'data/custom-arena-cards'
PACK_VERSION = 23
SUPPORTED_LAYOUTS = frozenset({'normal', 'leveler'})
SUPPORTED_CARD_TYPES = frozenset({'Artifact', 'Creature', 'Enchantment', 'Instant', 'Land', 'Sorcery', 'Kindred', 'Planeswalker'})
COLOR_NAMES = {'W': 'White', 'U': 'Blue', 'B': 'Black', 'R': 'Red', 'G': 'Green'}
# Arena has no timeshifted/special rarity badge. Use its rare display badge;
# retain the original rarity in source metadata.
RARITIES = {'common': 2, 'uncommon': 3, 'rare': 4, 'mythic': 5, 'special': 4}
LEGACY_NAMES = (
    'All Is Dust', 'Basalt Monolith', 'Conduit of Ruin', 'Flayer of Loyalties',
    'Imperial Seal', 'It That Betrays', 'Joo Dee, Public Servant',
    'Kozilek, Butcher of Truth', 'Metalworker', 'Power Artifact',
    'Rise of the Eldrazi', 'Thran Dynamo', 'Time Vault', "Ulamog's Dreadsire",
    'Voltaic Key', 'Zhulodok, Void Gorger', 'City of Brass', 'City of Traitors',
    'Exotic Orchard', 'Forbidden Orchard',
)


def read_json(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + '.tmp')
    temporary.write_text(json.dumps(value, indent=2, ensure_ascii=False), encoding='utf8')
    temporary.replace(path)


def digest_file(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as source:
        for block in iter(lambda: source.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def digest_json(value):
    return hashlib.sha256(json.dumps(value, ensure_ascii=False, sort_keys=True,
                                     separators=(',', ':')).encode('utf8')).hexdigest()


def native_mana_cost(text):
    """Use Arena's installed o(B/R), o(U/P), o(2/G) symbol grammar."""
    return re.sub(r'\{([^}]+)\}', lambda m: 'o' + ('(' + m[1] + ')' if '/' in m[1] else m[1]), text)


def native_rules_text(text):
    return re.sub(r'(?:\{[^}]+\})+', lambda m: '{' + native_mana_cost(m[0]) + '}', text)


def replace_copy(source, destination):
    """Complete each installed file before replacing it; retry reuses the journal."""
    destination = Path(destination)
    pending = destination.with_name(destination.name + '.arcane-forge-pending')
    shutil.copy2(source, pending)
    pending.replace(destination)


def resolve_asset(path):
    value = Path(path)
    return value if value.is_absolute() else ROOT / value


def installation_paths():
    state = read_json(ROOT / 'data/local-client/configuration-state.json')
    installation = Path(state['installation'])
    databases = list((installation / 'MTGA_Data/Downloads/Raw').glob('Raw_CardDatabase_*.mtga'))
    if len(databases) != 1:
        raise ValueError('Expected one installed Arena card database')
    return installation, databases[0]


def read_native_enums(db):
    """Public preflight helper accepting a read-only sqlite connection."""
    enums = {}
    for kind, value, text in db.execute(
            'SELECT e.Type,e.Value,l.Loc FROM Enums e JOIN Localizations_enUS l '
            'ON l.LocId=e.LocId ORDER BY l.Formatted DESC'):
        enums.setdefault(kind, {})[text] = value
    for name,value in read_json(PACK/'planeswalker-subtypes.json').items():
        occupied=[text for text,number in enums.get('SubType',{}).items() if number==value and text!=name]
        if occupied:raise ValueError('Local planeswalker subtype ID is occupied: '+str(value))
        enums.setdefault('SubType',{}).setdefault(name,value)
    return enums


def type_parts(metadata, enums):
    primary, _, subtype_text = metadata.get('type_line', '').partition(' — ')
    words = primary.split()
    supers = [word for word in words if word in enums.get('SuperType', {})]
    return [word for word in words if word not in supers], supers, subtype_text


def native_deck_limit(metadata):
    """Native basic lands/Rats use 250; Seven Dwarves/Nazgul use 7/9."""
    type_line = metadata.get('type_line', '')
    if isinstance(type_line, str) and 'Basic' in type_line.split(' — ')[0].split():
        return 250, None
    text, name = metadata.get('oracle_text', ''), metadata.get('name', '')
    if not isinstance(text, str) or not isinstance(name, str):
        return 0, None  # Required-field validation reports these separately.
    limit_lines = [line for line in text.splitlines() if re.search(r'\bdeck can have\b', line, re.I)
                   and not line.startswith('Rulebreaker')]
    if not limit_lines:
        return 0, None
    if len(limit_lines) != 1:
        return 0, 'Multiple deck-size exception rules need a dedicated importer'
    line = limit_lines[0].strip()
    if re.fullmatch(r'A deck can have any number of cards named ' + re.escape(name) + r'\.', line):
        return 250, None
    match = re.fullmatch(r'A deck can have up to (\d+|[a-z]+) cards named ' + re.escape(name) + r'\.', line)
    if match:
        words = ('zero one two three four five six seven eight nine ten eleven twelve thirteen fourteen '
                 'fifteen sixteen seventeen eighteen nineteen twenty').split()
        count = int(match[1]) if match[1].isdigit() else words.index(match[1]) if match[1] in words else 0
        if 0 < count <= 250:
            return count, None
    return 0, 'Unsupported deck-copy exception; native deck limit would be incorrect'


def metadata_support_issues(metadata, enums):
    """Return metadata blockers before art downloads or installed file changes.

    An empty list means a supported display shape, not verified gameplay or
    visual fidelity. Multiface cards are never flattened into normal cards.
    """
    if not isinstance(metadata, dict):
        return ['Metadata must be a JSON object']
    issues = []
    if metadata.get('layout') not in SUPPORTED_LAYOUTS:
        issues.append('Unsupported layout: ' + str(metadata.get('layout', '(missing)')))
    if metadata.get('card_faces'):
        issues.append('Multiple/alternate faces require linked native face records; never flattened')
    for field in ('name', 'type_line', 'mana_cost', 'oracle_text'):
        if not isinstance(metadata.get(field), str) or (field in ('name', 'type_line') and not metadata[field]):
            issues.append('Missing or invalid metadata field: ' + field)
    cmc = metadata.get('cmc')
    if not isinstance(cmc, (int, float)) or isinstance(cmc, bool) or not math.isfinite(cmc) or cmc < 0:
        issues.append('Missing or invalid mana value')
    elif cmc != int(cmc):
        issues.append('Fractional mana values are unsupported')
    if metadata.get('rarity') not in RARITIES:
        issues.append('Unsupported rarity: ' + str(metadata.get('rarity', '(missing)')))
    for field in ('colors', 'color_identity'):
        values = metadata.get(field)
        if not isinstance(values, list) or any(not isinstance(value, str) or value not in COLOR_NAMES for value in values):
            issues.append('Missing or unsupported ' + field)
    if 'color_indicator' in metadata and (not isinstance(metadata['color_indicator'], list) or
            any(not isinstance(value, str) or value not in COLOR_NAMES for value in metadata['color_indicator'])):
        issues.append('Unsupported color_indicator')
    if 'produced_mana' in metadata and (not isinstance(metadata['produced_mana'], list) or
            any(not isinstance(value, str) or value not in 'WUBRGC' for value in metadata['produced_mana'])):
        issues.append('Unsupported produced_mana')
    if isinstance(metadata.get('type_line'), str):
        types, supers, subtype_text = type_parts(metadata, enums)
        if not types:
            issues.append('No supported card type')
        for kind, values in (('CardType', types), ('SuperType', supers), ('SubType', subtype_text.split())):
            missing = [value for value in values if value not in enums.get(kind, {})]
            if missing:
                issues.append('Unknown native ' + kind + ': ' + ', '.join(missing))
        unsupported = [value for value in types if value not in SUPPORTED_CARD_TYPES]
        if unsupported:
            issues.append('Unsupported display card type: ' + ', '.join(unsupported))
        if 'Planeswalker' in types:
            if not re.fullmatch(r'(?:\d+|X|\*|\d+d\d+(?:[+-]\d+)?)',str(metadata.get('loyalty',''))):
                issues.append('Unsupported starting loyalty expression')
            loyalty_lines=[line.strip() for line in metadata.get('oracle_text','').splitlines()
                           if re.match(r'^[+\-\u2212\dX]',line.strip())]
            if any(not re.fullmatch(r'([+\-\u2212]?(?:\d+|X)):\s*(.+)',line) for line in loyalty_lines):
                issues.append('Unsupported loyalty cost expression')
        if 'Creature' in types:
            for stat in ('power', 'toughness'):
                if not isinstance(metadata.get(stat), str):
                    issues.append('Missing creature ' + stat)
    if isinstance(metadata.get('mana_cost'), str):
        for symbol in re.findall(r'\{([^}]+)\}', metadata['mana_cost']):
            if not re.fullmatch(r'(?:\d+|[WUBRGCXYZS]|[WUBRGC2]/[WUBRG]|[WUBRG]/P|[WUBRG]/[WUBRG]/P)', symbol):
                issues.append('Unsupported mana symbol in casting cost: {' + symbol + '}')
    _, deck_limit_issue = native_deck_limit(metadata)
    if deck_limit_issue:
        issues.append(deck_limit_issue)
    return issues


def legacy_index(record):
    index = record.get('grpId', 0) - 1_800_000 if isinstance(record.get('grpId'), int) else -1
    return index if 0 <= index < len(LEGACY_NAMES) else None


def normalize_record(record):
    if not isinstance(record, dict):
        raise ValueError('Manifest entry must be a JSON object')
    result = dict(record)
    for key in ('grpId', 'artId'):
        if not isinstance(result.get(key), int) or isinstance(result[key], bool) or not 0 < result[key] < 2_147_483_647:
            raise ValueError('Invalid positive 32-bit ' + key)
    for key in ('name', 'metadata', 'art'):
        if not isinstance(result.get(key), str) or not result[key].strip():
            raise ValueError('Missing ' + key)
    old = legacy_index(result)
    if old is not None:
        if result['name'] != LEGACY_NAMES[old] or result['artId'] != 9_000_000 + old:
            raise ValueError('Legacy printing names/art IDs must remain unchanged')
        for key, value in (('localizationBase', 20_000_000 + old * 100), ('abilityBase', 1_900_000 + old * 20)):
            if key in result and result[key] != value:
                raise ValueError('Legacy ' + key + ' must remain ' + str(value))
            result[key] = value
    else:
        if result['grpId'] < 2_000_000:
            raise ValueError('New custom grpId must be at least 2000000')
        if not 10_000_000 <= result['artId'] <= 10_999_999:
            raise ValueError('New custom artId must be within the artwork helper range 10000000..10999999')
        for key, minimum in (('localizationBase', 30_000_000), ('abilityBase', 3_000_000)):
            if not isinstance(result.get(key), int) or isinstance(result[key], bool) or not minimum <= result[key] <= 2_147_483_547:
                raise ValueError('New records require an explicit stable ' + key + ' >= ' + str(minimum))
    return result


def oracle_lines(metadata, record):
    oracle = metadata['oracle_text'].replace(metadata['name'], record['name'])
    if metadata['name'] != record['name'] and ',' in metadata['name']:
        oracle = oracle.replace(metadata['name'].split(',')[0], record['name'].split(',')[0])
    return oracle.split('\n') if oracle else []


def id_blocks(record):
    return (range(record['localizationBase'], record['localizationBase'] + 100),
            range(record['abilityBase'], record['abilityBase'] + (20 if legacy_index(record) is not None else 32)))


def native_type_key(metadata, enums):
    return ','.join(str(enums['CardType'][value]) for value in type_parts(metadata, enums)[0])


def overlapping_block(blocks, start, stop, owner):
    """Find an overlapping reservation owned by a different printing."""
    index = bisect.bisect_left(blocks, (start, -1, -1))
    if index and blocks[index - 1][1] > start and blocks[index - 1][2] != owner:
        return blocks[index - 1][2]
    while index < len(blocks) and blocks[index][0] < stop:
        if blocks[index][2] != owner:
            return blocks[index][2]
        index += 1
    return None


def prepare_pack(records, db, previous):
    """Classify every entry before staging or writing installed files."""
    enums = read_native_enums(db)
    native_cards = {row[0]: row[1:] for row in db.execute('SELECT GrpId,Order_Title,ArtId FROM Cards')}
    native_abilities = {row[0] for row in db.execute('SELECT Id FROM Abilities')}
    native_locs = {row[0] for row in db.execute('SELECT LocId FROM Localizations')}
    templates = {row[0] for row in db.execute(
        'SELECT DISTINCT Types FROM Cards WHERE IsPrimaryCard=1 AND IsToken=0 AND LinkedFaceType=0 AND GrpId<1800000')}
    owned = {}
    for record in previous.get('cards', []):
        try:
            normalized = normalize_record(record)
            owned[normalized['grpId']] = normalized
        except (ValueError, TypeError):
            pass
    # Version-8 legacy installations predate explicit allocation fields.
    for index, name in enumerate(LEGACY_NAMES):
        grpid = 1_800_000 + index
        if native_cards.get(grpid, ('',))[0] == name:
            owned.setdefault(grpid, {'grpId': grpid, 'name': name, 'artId': 9_000_000 + index,
                                    'localizationBase': 20_000_000 + index * 100, 'abilityBase': 1_900_000 + index * 20})
    native_art = {}
    for grpid, (_, artid) in native_cards.items():
        native_art.setdefault(artid, set()).add(grpid)
    outcomes, prepared, fingerprints = [], [], []
    allocated = {key: {} for key in ('grpId', 'artId')}
    reservations = {'localization': [], 'ability': []}
    for old in owned.values():
        for kind, block in zip(reservations, id_blocks(old)):
            reservations[kind].append((block.start, block.stop, old['grpId']))
    for blocks in reservations.values():
        blocks.sort()
    for index, raw in enumerate(records):
        if index % 1000 == 0:
            print(f'Checking card-pack files {index:,}/{len(records):,}...', flush=True)
        outcome = {'index': index, 'name': raw.get('name', '') if isinstance(raw, dict) else '',
                   'grpId': raw.get('grpId') if isinstance(raw, dict) else None, 'issues': []}
        outcomes.append(outcome)
        try:
            record = normalize_record(raw)
        except (ValueError, TypeError) as error:
            outcome.update(status='invalid', issues=[str(error)])
            continue
        issues = outcome['issues']
        for kind in allocated:
            value = record[kind]
            if value in allocated[kind]:
                issues.append(f'Duplicate {kind} allocation {value} with record {allocated[kind][value]}')
            allocated[kind][value] = index
        for kind, block in zip(reservations, id_blocks(record)):
            owner = overlapping_block(reservations[kind], block.start, block.stop, record['grpId'])
            if owner is not None:
                issues.append(f'Overlapping {kind} allocation with printing {owner}')
            else:
                entry = (block.start, block.stop, record['grpId'])
                at = bisect.bisect_left(reservations[kind], entry)
                if at == len(reservations[kind]) or reservations[kind][at] != entry:
                    reservations[kind].insert(at, entry)
        if record['grpId'] in native_cards and record['grpId'] not in owned:
            issues.append('grpId collides with an unowned installed printing')
        if native_art.get(record['artId'], set()) - {record['grpId']}:
            issues.append('artId collides with another installed printing')
        old = owned.get(record['grpId'])
        if old and any(record[key] != old[key] for key in ('artId', 'localizationBase', 'abilityBase')):
            issues.append('Installed printing allocations cannot change during an update')
        locs, abilities = id_blocks(record)
        old_locs, old_abilities = id_blocks(old) if old else ((), ())
        if (set(locs) & native_locs) - set(old_locs):
            issues.append('localizationBase block collides with installed localizations')
        if (set(abilities) & native_abilities) - set(old_abilities):
            issues.append('abilityBase block collides with installed abilities')
        if issues:
            outcome['status'] = 'invalid'
            continue
        metadata_path, art_path = resolve_asset(record['metadata']), resolve_asset(record['art'])
        try:
            metadata = read_json(metadata_path)
        except (OSError, ValueError) as error:
            outcome.update(status='missing_metadata', issues=[str(error)])
            continue
        issues.extend(metadata_support_issues(metadata, enums))
        if issues:
            outcome['status'] = 'unsupported'
            continue
        if native_type_key(metadata, enums) not in templates and 'Planeswalker' not in metadata['type_line'].split(' — ')[0].split():
            issues.append('No native single-face template for this exact card type combination')
        lines = oracle_lines(metadata, record)
        if len(lines) > len(abilities):
            issues.append(f'Oracle text needs {len(lines)} abilities; allocation has {len(abilities)} slots')
        if issues:
            outcome['status'] = 'unsupported'
            continue
        metadata_hash = digest_json(metadata)
        fingerprint = {'record': record, 'metadataSha256': metadata_hash}
        if not art_path.is_file():
            outcome.update(status='missing_art', issues=['Artwork file is missing: ' + str(art_path)])
            fingerprint['artSha256'] = None
            fingerprints.append(fingerprint)
            continue
        with art_path.open('rb') as image:
            signature = image.read(8)
        if not (signature.startswith(b'\xff\xd8\xff') or signature == b'\x89PNG\r\n\x1a\n'):
            outcome.update(status='invalid_art', issues=['Artwork is not a supported JPEG or PNG file: ' + str(art_path)])
            continue
        fingerprint['artSha256'] = digest_file(art_path)
        fingerprints.append(fingerprint)
        outcome.update(status='ready', metadataSha256=metadata_hash, artSha256=fingerprint['artSha256'])
        prepared.append((record, metadata, fingerprint))
    return prepared, {'packVersion': PACK_VERSION, 'manifestSha256': digest_json(records),
                      'contentSha256': digest_json(sorted(fingerprints, key=lambda item: item['record']['grpId'])),
                      'ready': len(prepared), 'total': len(records),
                      'invalidAllocations': any(outcome['status'] == 'invalid' for outcome in outcomes),
                      'outcomes': outcomes,
                      'scope': 'Local display records only; Forge gameplay and in-client appearance require separate support.'}


def stage_database(database, staged, prepared):
    with closing(sqlite3.connect(database.as_uri() + '?mode=ro', uri=True)) as source, closing(sqlite3.connect(staged)) as target:
        source.backup(target)
    with closing(sqlite3.connect(staged)) as db, db:
        db.row_factory = sqlite3.Row
        columns = [row['name'] for row in db.execute('PRAGMA table_info(Cards)')]
        enums = read_native_enums(db)
        lang_tables = [row[0] for row in db.execute(
            "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'Localizations_%'")]
        title_ids = dict(db.execute('SELECT Loc,MIN(LocId) FROM Localizations_enUS GROUP BY Loc'))
        templates = {}
        ability_template = dict(db.execute('SELECT * FROM Abilities WHERE Id=8').fetchone())

        def localize(locid, text):
            db.execute('INSERT OR REPLACE INTO Localizations VALUES (?)', (locid,))
            for table in lang_tables:
                safe_table = '"' + table.replace('"', '""') + '"'
                db.executemany('INSERT OR REPLACE INTO ' + safe_table + ' VALUES (?,?,?)',
                               ((locid, formatted, text) for formatted in (0, 1)))

        # Question text follows the option parameters, preserving their IDs.
        for count in range(65):
            prompt_id=2920000+count
            loc_id=99200000+count
            localize(loc_id, '{'+str(count+1)+', String}')
            db.execute('INSERT OR REPLACE INTO Prompts(Id,LocId) VALUES (?,?)',(prompt_id,loc_id))

        for subtype,value in read_json(PACK/'planeswalker-subtypes.json').items():
            present=db.execute("SELECT Value FROM Enums WHERE Type='SubType' AND Value=?",(value,)).fetchone()
            if not present:
                locid=61_000_000+value
                localize(locid,subtype)
                db.execute('INSERT INTO Enums(Type,Value,LocId) VALUES (?,?,?)',('SubType',value,locid))

        def ids(kind, names):
            return ','.join(str(enums[kind][name]) for name in names)

        def mana(text):
            return native_rules_text(text)

        for index, (record, metadata, _) in enumerate(prepared):
            types, supers, subtype_text = type_parts(metadata, enums)
            key = ids('CardType', types)
            if key not in templates:
                template=db.execute(
                    'SELECT * FROM Cards WHERE IsPrimaryCard=1 AND IsToken=0 AND LinkedFaceType=0 AND Types=? AND GrpId<1800000 LIMIT 1',
                    (key,)).fetchone()
                if template is None and 'Planeswalker' in types:
                    template=db.execute('SELECT * FROM Cards WHERE IsPrimaryCard=1 AND LinkedFaceType=0 AND Types=? AND GrpId<1800000 LIMIT 1',
                        (str(enums['CardType']['Planeswalker']),)).fetchone()
                templates[key] = dict(template)
            card = dict(templates[key])
            card.update({column: '' for column in columns if column.startswith('Linked')})
            base = record['localizationBase']
            localize(base, record['name'])
            localize(base + 1, ' '.join(supers + types))
            localize(base + 2, subtype_text)
            title_id = min(title_ids.get(record['name'], base), base)
            title_ids[record['name']] = title_id
            abilities = []
            lines = oracle_lines(metadata, record)
            # Remove only this printing's reserved abilities on updates; shorter
            # rules must not leave old child definitions behind.
            ability_block = id_blocks(record)[1]
            db.execute('DELETE FROM Abilities WHERE Id>=? AND Id<?', (ability_block.start, ability_block.stop))
            for number, text in enumerate(lines):
                ability_id, locid = record['abilityBase'] + number, base + 10 + number
                loyalty_match=re.fullmatch(r'([+\-\u2212]?(?:\d+|X)):\s*(.+)',text) if 'Planeswalker' in types else None
                if loyalty_match:text=loyalty_match.group(2)
                localize(locid, mana(text))
                ability = dict(ability_template)
                ability.update({key: None for key in ability if key not in (
                    'Id', 'BaseId', 'FakeBaseIdNumeral', 'TextId', 'Category', 'SubCategory', 'AbilityWord',
                    'NumericAid', 'RequiresConfirmation', 'PaymentType', 'FullyParsed', 'IsIntrinsicAbility')})
                category = 1 if ': ' in text else 2 if text.startswith(('When ', 'Whenever ', 'At ')) else 4 if any(
                    kind in types for kind in ('Sorcery', 'Instant')) else 3
                ability.update(Id=ability_id, BaseId=0, BaseIdNumeral=0, FakeBaseIdNumeral=0, TextId=locid,
                               Category=category, SubCategory=0, AbilityWord=0, NumericAid=0, RequiresConfirmation=0,
                               PaymentType=0, FullyParsed=1, IsIntrinsicAbility=0)
                if loyalty_match:
                    loyalty_cost=loyalty_match.group(1).replace('\u2212','-')
                    ability.update(Category=1,PaymentType=2,LoyaltyCost=loyalty_cost,CostTypes='5',
                                   RequiresConfirmation=8 if loyalty_cost.startswith('-') else 0)
                names = list(ability)
                db.execute('INSERT OR REPLACE INTO Abilities (' + ','.join(names) + ') VALUES (' +
                           ','.join('?' for _ in names) + ')', [ability[key] for key in names])
                abilities.append(str(ability_id) + ':' + str(locid))
            colors = metadata['colors']
            color_value = lambda values: ids('CardColor', [COLOR_NAMES[value] for value in values])
            frame_colors = [value for value in COLOR_NAMES if value in metadata.get('produced_mana', [])] if 'Land' in types else colors
            raw_frame = ('Z land' if len(frame_colors) > 1 else ''.join(frame_colors) + ' land' if frame_colors else '') if 'Land' in types else 'A' if 'Artifact' in types and not colors else 'C' if not colors else ''.join(colors)
            if 'Planeswalker' in types:raw_frame=''.join(colors) if colors else 'C'
            old = legacy_index(record)
            card.update(GrpId=record['grpId'], ArtId=record['artId'], ArtPath='', TitleId=title_id,
                        AltTitleId=title_id, InterchangeableTitleId=title_id, FlavorTextId=0, ReminderTextId=0,
                        TypeTextId=base + 1, SubtypeTextId=base + 2 if subtype_text else 0,
                        ArtistCredit=metadata.get('artist', ''), ArtSize=0, Rarity=RARITIES[metadata['rarity']],
                        ExpansionCode='VMA', DigitalReleaseSet='VMA', IsToken=0, IsPrimaryCard=1, IsDigitalOnly=0,
                        IsRebalanced=0, RebalancedCardGrpId=0, DefunctRebalancedCardGrpId=0,
                        AlternateDeckLimit=native_deck_limit(metadata)[0],
                        CollectorNumber=str(old + 1 if old is not None else record['grpId'] - 2_000_000 + 1),
                        CollectorMax=str(len(LEGACY_NAMES) if old is not None else len(prepared)), CollectorSuffix='',
                        DraftContent='', UsesSideboard=0,
                        OldSchoolManaText=native_mana_cost(metadata['mana_cost']),
                        LinkedFaceType=0, RawFrameDetail=raw_frame, Watermark='', TextChangeData='',
                        Power=metadata.get('power', ''), Toughness=metadata.get('loyalty','') if 'Planeswalker' in types else metadata.get('toughness', ''),
                        Colors=color_value(colors), ColorIdentity=color_value(metadata['color_identity']),
                        FrameColors=color_value(frame_colors), IndicatorColors=color_value(metadata.get('color_indicator', [])), Types=key,
                        Subtypes=ids('SubType', subtype_text.split()), Supertypes=ids('SuperType', supers),
                        AbilityIds=','.join(abilities), HiddenAbilityIds='', AbilityIdToLinkedTokenGrpId='',
                        AbilityIdToLinkedConjurations='', KnownSupportedStyles='', AdditionalFrameDetails='',
                        ExtraFrameDetails='', Tags='', PromoLabel='', Order_Title=record['name'],
                        Order_CMCWithXLast=int(metadata['cmc']), Order_ManaCostDifficulty=len(colors))
            db.execute('INSERT OR REPLACE INTO Cards (' + ','.join(columns) + ') VALUES (' +
                       ','.join('?' for _ in columns) + ')', [card[key] for key in columns])
        # Live, display-only progress for Y'shtola's end-step condition.
        for amount in range(1001):
            ability_id,locid=4_100_000+amount,60_100_000+amount
            label=(f'Life lost this turn by a player: {amount}/4' if amount<4 else
                   f'Life lost this turn by a player: {amount} (draw trigger ready)' if amount<1000 else
                   'Life lost this turn by a player: 1000+ (draw trigger ready)')
            prior=db.execute('SELECT TextId FROM Abilities WHERE Id=?',(ability_id,)).fetchone()
            if prior and prior[0]!=locid:raise ValueError("Y'shtola progress ability identity is occupied")
            localize(locid,label)
            ability=dict(ability_template)
            ability.update({key:None for key in ability if key not in (
                'Id','BaseId','FakeBaseIdNumeral','TextId','Category','SubCategory','AbilityWord',
                'NumericAid','RequiresConfirmation','PaymentType','FullyParsed','IsIntrinsicAbility')})
            ability.update(Id=ability_id,BaseId=0,BaseIdNumeral=0,FakeBaseIdNumeral=0,TextId=locid,
                           Category=3,SubCategory=0,AbilityWord=0,NumericAid=0,RequiresConfirmation=0,
                           PaymentType=0,FullyParsed=1,IsIntrinsicAbility=0)
            names=list(ability)
            db.execute('INSERT OR REPLACE INTO Abilities ('+','.join(names)+') VALUES ('+
                       ','.join('?' for _ in names)+')',[ability[key] for key in names])
        # Slivdrazi's playtest token has no native Arena printing. Reuse the
        # colorless Sliver illustration and the existing Scion sacrifice mana
        # ability, but keep its own name and both creature types.
        slivdrazi=[record for record,_,_ in prepared if record['name']=='Slivdrazi Monstrosity']
        if slivdrazi:
            token_id,title_id=2_900_000,59_000_000
            existing=db.execute('SELECT TitleId FROM Cards WHERE GrpId=?',(token_id,)).fetchone()
            if existing and existing[0]!=title_id:
                raise ValueError('Generated Sliver token identity is occupied')
            for locid,text in ((title_id,'Eldrazi Sliver'),(title_id+1,'Eldrazi Sliver')):
                prior=db.execute('SELECT Loc FROM Localizations_enUS WHERE LocId=?',(locid,)).fetchone()
                if prior and prior[0]!=text:
                    raise ValueError('Generated Sliver token localization is occupied')
                localize(locid,text)
            token=dict(db.execute('SELECT * FROM Cards WHERE GrpId=79390 AND IsToken=1').fetchone())
            art,artist=db.execute('SELECT ArtId,ArtistCredit FROM Cards WHERE GrpId=96846 AND IsToken=1').fetchone()
            token.update(GrpId=token_id,ArtId=art,TitleId=title_id,AltTitleId=title_id,
                InterchangeableTitleId=title_id,SubtypeTextId=title_id+1,
                Subtypes=ids('SubType',['Eldrazi','Sliver']),ArtistCredit=artist,IsPrimaryCard=0,Order_Title='Eldrazi Sliver')
            db.execute('INSERT OR REPLACE INTO Cards ('+','.join(columns)+') VALUES ('+
                ','.join('?' for _ in columns)+')',[token[key] for key in columns])
            for source in slivdrazi:
                row=db.execute('SELECT AbilityIds FROM Cards WHERE GrpId=?',(source['grpId'],)).fetchone()
                mana_token_ability=None
                for item in row[0].split(','):
                    ability,locid=map(int,item.split(':'))
                    text=db.execute('SELECT Loc FROM Localizations_enUS WHERE LocId=?',(locid,)).fetchone()[0]
                    if 'Create a 1/1 colorless Eldrazi Sliver' in text:mana_token_ability=ability
                if mana_token_ability is None:raise ValueError('Slivdrazi token creation ability missing')
                db.execute('UPDATE Cards SET AbilityIdToLinkedTokenGrpId=? WHERE GrpId=?',
                    (str(mana_token_ability)+':'+str(token_id),source['grpId']))
        if __package__:
            from .custom_tokens import stage as stage_tokens
        else:
            from custom_tokens import stage as stage_tokens
        stage_tokens(db,localize,mana)
        if db.execute('PRAGMA integrity_check').fetchone()[0] != 'ok':
            raise ValueError('Staged card database failed integrity check')


def arena_is_running():
    result = subprocess.run(['powershell', '-NoProfile', '-Command',
                             'if(Get-Process MTGA -ErrorAction SilentlyContinue){exit 1}'], capture_output=True)
    return result.returncode != 0


@contextmanager
def installation_lock():
    """Keep startup and bulk import from sharing staging files concurrently."""
    PACK.mkdir(parents=True, exist_ok=True)
    with (PACK / 'installation.lock').open('a+b') as lock:
        if lock.tell() == 0:
            lock.write(b'0')
            lock.flush()
        lock.seek(0)
        try:
            if os.name == 'nt':
                import msvcrt
                msvcrt.locking(lock.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(lock.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        except OSError as error:
            raise RuntimeError('Another card-pack preparation or installation is running; retry after it finishes') from error
        try:
            yield
        finally:
            lock.seek(0)
            if os.name == 'nt':
                msvcrt.locking(lock.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(lock.fileno(), fcntl.LOCK_UN)


def run(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest', type=Path, default=PACK / 'manifest.json')
    parser.add_argument('--prepare-only', action='store_true', help='Write preflight report without changing installed Arena files')
    parser.add_argument('--report', type=Path, help='Outcome report path (default: manifest directory/preflight.json)')
    parser.add_argument('--skip-unsupported', action='store_true', help='Install ready entries only; invalid/colliding IDs always block')
    args = parser.parse_args(argv)
    manifest_path = args.manifest.resolve()
    if not manifest_path.exists():
        if args.manifest == PACK / 'manifest.json':
            return 0
        raise ValueError('Manifest does not exist: ' + str(manifest_path))
    records = read_json(manifest_path)
    if not isinstance(records, list):
        raise ValueError('Manifest must be a JSON array of stable printing records')
    pack = manifest_path.parent
    report_path = args.report or pack / 'preflight.json'
    # Ownership is global to this Arena installation. Candidate manifests may
    # live elsewhere before promotion to PACK/manifest.json.
    installed_path = PACK / 'installed.json'
    pending_path = PACK / 'pending-install.json'
    previous = read_json(installed_path) if installed_path.exists() else {}
    installation, database = installation_paths()
    pending = read_json(pending_path) if pending_path.exists() else {}
    if pending.get('database') != str(database):
        pending = {}
    known_records = {record['grpId']: record for record in previous.get('cards', [])}
    known_records.update({record['grpId']: record for record in pending.get('cards', [])})
    ownership = dict(previous, cards=list(known_records.values()))
    managed = installation / 'MTGA_Data/Managed'
    core, helper = managed / 'Core.dll', managed / 'ArcaneForge.CardArt.dll'
    with closing(sqlite3.connect(database.as_uri() + '?mode=ro', uri=True)) as db:
        prepared, report = prepare_pack(records, db, ownership)
        installed = all(db.execute('SELECT Order_Title,ArtId FROM Cards WHERE GrpId=?',
                                  (record['grpId'],)).fetchone() == (record['name'], record['artId'])
                        for record, _, _ in prepared)
    display_path=PACK/'art-display.json'
    # Regenerate from source metadata so future imports retain the repair.
    overrides_path=ROOT/'client-card-extension/art-display-overrides.json'
    overrides=read_json(overrides_path) if overrides_path.exists() else []
    rules=[]
    for record,metadata,fingerprint in prepared:
        playtest='playtest' in (metadata.get('promo_types') or [])
        rule={'artId':record['artId'],'playtest':playtest}
        if playtest and metadata.get('illustration_id') is None:
            rule['placeholder']=True
        else:
            for override in overrides:
                if override['sourceCardId']==metadata.get('id') and override['sourceArtSha256']==fingerprint['artSha256']:
                    rule.update(override['crop'])
        if playtest or len(rule)>2:
            rules.append(rule)
    write_json(display_path,{'cards':rules})
    display_table=PACK/'art-display.tsv'
    subprocess.run(['python', str(ROOT/'tools/export_rulebreakers.py')], cwd=ROOT, check=True)
    rulebreaker_table=PACK/'rulebreaker-decks.tsv'
    playtest_names=PACK/'playtest-names.txt'
    playtest_names.write_text('\n'.join(sorted({record['name'] for record,metadata,_ in prepared
        if 'playtest' in (metadata.get('promo_types') or [])}))+'\n',encoding='utf8')
    display_table.write_text('\n'.join('\t'.join(str(value) for value in (
        rule['artId'],int(rule.get('placeholder',False)),rule.get('left',0),rule.get('top',0),
        rule.get('width',1),rule.get('height',1),int(rule.get('playtest',False)))) for rule in rules)+'\n',encoding='utf8')
    if display_path.exists():
        display=read_json(display_path)
        allowed_art={record['artId'] for record,_,_ in prepared}
        for rule in display.get('cards',[]):
            if rule.get('artId') not in allowed_art:
                raise ValueError('Artwork display rule references an unprepared card')
            if not rule.get('placeholder') and not (0 <= rule.get('left',0) < 1 and 0 <= rule.get('top',0) < 1
                and 0 < rule.get('width',1) <= 1-rule.get('left',0)
                and 0 < rule.get('height',1) <= 1-rule.get('top',0)):
                raise ValueError('Artwork display crop is outside the source image')
    report.update(manifest=str(manifest_path), preparedAtUtc=datetime.now(timezone.utc).isoformat(),
                  tokenSourceSha256=digest_json([digest_file(ROOT/'data/custom-arena-cards/tokens.json'),digest_file(ROOT/'tools/custom_tokens.py')]) if (ROOT/'data/custom-arena-cards/tokens.json').exists() else None,
                  artDisplaySha256=digest_file(display_path) if display_path.exists() else None,
                  rendererSourceSha256=digest_json([digest_file(ROOT/'client-card-extension/LocalCardArt.cs'),
                      digest_file(ROOT/'client-card-extension/ArtDisplayRules.cs'),
                      digest_file(ROOT/'client-card-extension/PlaytestSearch.cs'),
                      digest_file(ROOT/'client-card-extension/RulebreakerDecks.cs'),digest_file(ROOT/'client-card-extension/LoyaltyDisplay.cs')]),
                  rulebreakerSha256=digest_file(rulebreaker_table))
    write_json(report_path, report)
    blockers = len(records) - len(prepared)
    print(f"Card-pack preflight: {len(prepared)} ready, {blockers} blocked. Report: {report_path}")
    if report['invalidAllocations']:
        print('No installed files changed. Correct invalid/colliding allocations in the report; they cannot be skipped.')
        return 2
    if blockers and not args.skip_unsupported:
        print('No installed files changed. Correct reported inputs or use --skip-unsupported for eligible records.')
        return 2
    if args.prepare_only:
        print('Preparation complete. No installed Arena files changed.')
        return 0
    if not prepared:
        print('No eligible printings to install. No installed files changed.')
        return 0
    core_digest = digest_file(core)
    installed_art = installation / 'MTGA_Data/StreamingAssets/ArcaneForgeCards'
    if not pending and installed and helper.exists() and previous.get('database') == str(database) and previous.get('patchedCoreSha256') == core_digest and previous.get('packVersion') == PACK_VERSION and previous.get('contentSha256') == report['contentSha256'] and previous.get('artDisplaySha256') == report['artDisplaySha256'] and previous.get('tokenSourceSha256') == report['tokenSourceSha256'] and previous.get('rendererSourceSha256') == report['rendererSourceSha256'] and all((installed_art / (str(record['artId']) + '.jpg')).is_file() for record, _, _ in prepared):
        print('Local', len(prepared), 'card Arena pack is already installed with matching content.')
        return 0
    if arena_is_running():
        raise RuntimeError('Close Arena before installing/updating the pack; the preflight report is ready')
    generation = previous if previous.get('patchedCoreSha256') == core_digest else pending if core_digest in (
        pending.get('originalCoreSha256'), pending.get('patchedCoreSha256')) else {}
    backup = Path(generation['backup']) if generation.get('backup') else PACK / 'backups' / core_digest[:16]
    backup.mkdir(parents=True, exist_ok=True)
    if not (backup / 'Core.dll').exists():
        shutil.copy2(core, backup / 'Core.dll')
    if not (backup / database.name).exists():
        with closing(sqlite3.connect(database.as_uri() + '?mode=ro', uri=True)) as source, closing(sqlite3.connect(backup / database.name)) as target:
            source.backup(target)
    staged = pack / 'staged.mtga'
    stage_database(database, staged, prepared)
    if (ROOT/'data/custom-arena-cards/tokens.json').exists():
        report['tokenSourceSha256']=digest_json([digest_file(ROOT/'data/custom-arena-cards/tokens.json'),digest_file(ROOT/'tools/custom_tokens.py')])
    ready = ROOT / 'client-card-extension/bin/ready'
    subprocess.run(['dotnet', 'build', str(ROOT / 'client-card-extension'), '-o', str(ready), '--nologo'], check=True)
    shared=managed/'SharedClientCore.dll'
    if not (backup/shared.name).exists():shutil.copy2(shared,backup/shared.name)
    staged_shared=pack/'SharedClientCore.patched.dll'
    subprocess.run(['dotnet','run','--project',str(ROOT/'tools/card-art-patcher'),'--','--loyalty-display',
                    str(backup/shared.name),str(ready/'ArcaneForge.CardArt.dll'),str(staged_shared),str(managed)],check=True)
    staged_core = pack / 'Core.patched.dll'
    subprocess.run(['dotnet', 'run', '--project', str(ROOT / 'tools/card-art-patcher'), '--', str(core),
                    str(ready / 'ArcaneForge.CardArt.dll'), str(staged_core)], check=True)
    validation=managed/'Wizards.Arena.DeckValidation.Core.dll'
    staged_validation=pack/'DeckValidation.patched.dll'
    if not (backup/validation.name).exists():
        shutil.copy2(validation,backup/validation.name)
    subprocess.run(['dotnet', 'run', '--project', str(ROOT/'tools/card-art-patcher'), '--', '--validation',
                    str(backup/validation.name),str(ready/'ArcaneForge.CardArt.dll'),str(staged_validation),str(managed)],check=True)
    validation_client=managed/'Wizards.Arena.DeckValidation.Client.dll'
    staged_validation_client=pack/'DeckValidation.Client.patched.dll'
    if not (backup/validation_client.name).exists():
        shutil.copy2(validation_client,backup/validation_client.name)
    subprocess.run(['dotnet','run','--project',str(ROOT/'tools/card-art-patcher'),'--','--validation-client',
                    str(backup/validation_client.name),str(ready/'ArcaneForge.CardArt.dll'),str(staged_validation_client),str(managed)],check=True)
    if arena_is_running():
        raise RuntimeError('Arena opened during preparation. Close it and rerun; installed files were not changed')
    all_records = dict(known_records)
    all_records.update({record['grpId']: record for record, _, _ in prepared})
    pending_report = dict(report, cards=list(all_records.values()), backup=str(backup), database=str(database),
                          originalCoreSha256=generation.get('originalCoreSha256', core_digest),
                          patchedCoreSha256=digest_file(staged_core), phase='installing')
    # Record ownership before installed writes so a failed copy can resume
    # without renumbering decks or treating our partial installation as foreign.
    write_json(pending_path, pending_report)
    art_dir = installation / 'MTGA_Data/StreamingAssets/ArcaneForgeCards'
    art_dir.mkdir(exist_ok=True)
    if __package__:
        from .custom_tokens import install_art as install_token_art
    else:
        from custom_tokens import install_art as install_token_art
    install_token_art(art_dir)
    for record, _, fingerprint in prepared:
        destination = art_dir / (str(record['artId']) + '.jpg')
        if not destination.exists() or digest_file(destination) != fingerprint['artSha256']:
            replace_copy(resolve_asset(record['art']), destination)
    if display_path.exists():
        replace_copy(display_path,art_dir/'art-display.json')
        replace_copy(display_table,art_dir/'art-display.tsv')
        replace_copy(playtest_names,art_dir/'playtest-names.txt')
        replace_copy(rulebreaker_table,art_dir/'rulebreaker-decks.tsv')
    replace_copy(ready / 'ArcaneForge.CardArt.dll', helper)
    replace_copy(staged_shared,shared)
    replace_copy(staged_core, core)
    replace_copy(staged_validation,validation)
    replace_copy(staged_validation_client,validation_client)
    replace_copy(staged, database)
    # Omitting a printing is not removal; decks can still reference its IDs.
    installed_report = dict(pending_report, phase='installed',
                            patchedCoreSha256=digest_file(core), installedAtUtc=datetime.now(timezone.utc).isoformat())
    write_json(installed_path, installed_report)
    pending_path.unlink(missing_ok=True)
    print('Installed', len(prepared), 'local card printings. Existing allocations and original-file backups were preserved.')
    return 0


def main(argv=None):
    with installation_lock():
        return run(argv)


if __name__ == '__main__':
    raise SystemExit(main())
