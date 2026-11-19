"""Prepare linked custom faces and report unusual cards missing from Arena.

Uses the cached Oracle export and exact Forge face names. Installation is a
separate operation through Restart-Arena.ps1; original metadata is retained.
"""
import gzip
import json
from collections import Counter
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from tools.import_magic_cards import (WORK, PACK, installed_inventory, allocate,
    resolve_names, download, write_json, import_lock)
from tools.install_custom_arena_cards import metadata_support_issues, native_type_key

FAMILIES = {'flip', 'transform', 'modal_dfc', 'adventure'}


def mana_value(cost):
    import re
    total = 0
    for symbol in re.findall(r'\{([^}]+)\}', cost):
        if symbol.isdigit(): total += int(symbol)
        elif symbol in ('X', 'Y', 'Z'): pass
        elif symbol.startswith('2/'): total += 2
        else: total += 1
    return total


def face_metadata(card, face, index):
    value = {k: v for k, v in card.items() if k not in (
        'card_faces', 'name', 'type_line', 'oracle_text', 'mana_cost', 'power',
        'toughness', 'loyalty', 'colors', 'color_indicator', 'layout')}
    value.update(face)
    value.update(layout='normal', sourceLayout=card['layout'])
    value['colors'] = face.get('colors', card.get('colors', []))
    value['color_identity'] = card['color_identity']
    value['mana_cost'] = face.get('mana_cost', '')
    if card['layout'] == 'flip':
        value['mana_cost'] = card['card_faces'][0].get('mana_cost', '')
    value['cmc'] = (card['cmc'] if card['layout'] in ('flip', 'transform')
                    else mana_value(value['mana_cost']))
    value['image_uris'] = face.get('image_uris', card.get('image_uris', {}))
    return value


def run():
    cached = max(WORK.glob('oracle-cards-*.jsonl.gz'), key=lambda p: p.stat().st_mtime)
    with gzip.open(cached, 'rt', encoding='utf-8') as stream:
        cards = [json.loads(line) for line in stream]
    native, active, enums, occupied = installed_inventory()
    known = set(native) | {r['name'] for r in active}
    missing = []
    for card in cards:
        faces = card.get('card_faces', [])
        if card['name'] in known or (faces and faces[0]['name'] in known): continue
        if card.get('layout') not in ('normal', 'token'):
            missing.append(card)
    candidates = [c for c in missing if c.get('layout') in FAMILIES
                  and len(c.get('card_faces', [])) == 2 and 'paper' in c.get('games', [])]
    names = [c['card_faces'][0]['name'] for c in candidates]
    if '--reuse-checked-rules' in sys.argv:
        rules = json.loads((WORK/'forge-names.json').read_text(encoding='utf-8'))['cards']
        if any(name not in rules for name in names): raise ValueError('Missing checked rule name')
    else:
        rules = resolve_names(names)
    registry = json.loads((WORK/'identities.json').read_text(encoding='utf-8'))
    outcomes = []
    additions = []
    for card in missing:
        row = {'name': card['name'], 'layout': card['layout'], 'status': 'needs_support', 'reasons': []}
        outcomes.append(row)
        if card not in candidates:
            row['reasons'] = ['Non-playable ancillary layout' if card['layout'] in (
                'art_series', 'double_faced_token', 'emblem') else 'Requires a dedicated native layout adapter']
            continue
        faces = card['card_faces']
        resolved = rules.get(faces[0]['name'], {})
        if not resolved.get('supported') or resolved.get('forgeName') != faces[0]['name']:
            row['reasons'].append('No exact installed Forge rules for front face')
        if not {f['name'] for f in faces}.issubset(set(resolved.get('rulesFaces', []))):
            row['reasons'].append('Forge does not expose both exact rules faces')
        metadata = [face_metadata(card, f, i) for i, f in enumerate(faces)]
        for value in metadata:
            issues = metadata_support_issues(value, enums)
            row['reasons'] += [value['name'] + ': ' + r for r in issues]
            if not issues and native_type_key(value, enums) not in occupied['templates'] and 'Planeswalker' not in value['type_line']:
                row['reasons'].append(value['name'] + ': No native type template')
            if not value.get('image_uris', {}).get('art_crop'):
                row['reasons'].append(value['name'] + ': No face artwork')
        if row['reasons']: continue
        pair = []
        for i, value in enumerate(metadata):
            ids = allocate(card['oracle_id'] + ':face:' + str(i), registry, occupied)
            mp = WORK/'cards'/f"linked-{ids['grpId']}.json"
            ap = WORK/'art'/f"linked-{ids['artId']}.jpg"
            write_json(mp, value)
            if not ap.exists(): download(value['image_uris']['art_crop'], ap, 10_000_000, image=True)
            pair.append(dict(ids, name=value['name'], metadata=mp.relative_to(ROOT).as_posix(),
                art=ap.relative_to(ROOT).as_posix(), source=card['scryfall_uri'], sourceLayout=card['layout']))
        for i, record in enumerate(pair):
            record['linkedFace'] = {'role': 'front' if i == 0 else 'back',
                'grpId': pair[1-i]['grpId'], 'family': card['layout']}
        additions += pair
        row.update(status='prepared', grpIds=[r['grpId'] for r in pair])
        print('Prepared ' + card['name'], flush=True)
        write_json(WORK/'identities.json', registry)
    write_json(PACK/'manifest.json', active + additions)
    report = {'source': cached.name, 'counts': dict(Counter(r['status'] for r in outcomes)),
        'byLayout': dict(Counter(r['layout'] for r in outcomes)), 'addedFaces': len(additions),
        'gameplayVerified': False, 'cards': outcomes}
    write_json(WORK/'unusual-layout-report.json', report)
    text = ['# Missing unusual card audit', '', f"Prepared {len(additions)//2} linked cards ({len(additions)} faces).", '',
        'Gameplay and native appearance have not been verified for every card.', '']
    text += [f"- {r['name']} ({r['layout']}): {r['status']}" +
             (': ' + '; '.join(r['reasons']) if r['reasons'] else '') for r in outcomes]
    (WORK/'unusual-layout-report.md').write_text('\n'.join(text)+'\n', encoding='utf-8')
    print(json.dumps(report['counts']), flush=True)


if __name__ == '__main__':
    with import_lock(): run()
