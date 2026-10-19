"""Export installed Arena printing metadata for full GRE state messages."""
import json
import argparse
import sqlite3
from pathlib import Path

root = Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--database',type=Path,default=root / 'analysis/decompiled-rules/card-database.mtga')
parser.add_argument('--output-dir',type=Path,default=root/'data')
args=parser.parse_args()
database=args.database.resolve()
output_dir=args.output_dir.resolve();output_dir.mkdir(parents=True,exist_ok=True)
connection = sqlite3.connect(database.as_uri() + '?mode=ro', uri=True)
connection.row_factory = sqlite3.Row
records = {}
def numbers(value):
    return [int(item) for item in (value or '').split(',') if item]
for row in connection.execute('SELECT GrpId,TitleId,Types,Subtypes,Supertypes,Colors,AbilityIds FROM Cards'):
    records[str(row['GrpId'])] = {
        'name': row['TitleId'], 'cardTypes': numbers(row['Types']),
        'subtypes': numbers(row['Subtypes']), 'superTypes': numbers(row['Supertypes']),
        'color': numbers(row['Colors']),
        'uniqueAbilities': [{'grpId': int(item.split(':')[0])}
                            for item in (row['AbilityIds'] or '').split(',') if item],
    }
abilities = {}
all_abilities = {}
for row in connection.execute('SELECT c.GrpId,c.AbilityIds FROM Cards c'):
    entries=[]; all_entries=[]
    for item in (row['AbilityIds'] or '').split(','):
        if not item: continue
        ability=int(item.split(':')[0])
        text=connection.execute('SELECT l.Loc,a.Category,a.LoyaltyCost FROM Abilities a JOIN Localizations_enUS l ON l.LocId=a.TextId WHERE a.Id=?',(ability,)).fetchone()
        if text:
            entry={'id':ability,'text':text['Loc']}
            if text['LoyaltyCost']: entry['loyalty']=text['LoyaltyCost']
            all_entries.append(entry)
            if text['Category'] in (1, 4, 7, 8): entries.append(entry)
    if entries: abilities[str(row['GrpId'])]=entries
    if all_entries: all_abilities[str(row['GrpId'])]=all_entries
    # Granted activations and modal child abilities have their own IDs, embedded in the parent ability.
    pending = [entry['id'] for entry in all_entries]
    seen = set(pending)
    while pending:
        parent = pending.pop()
        row_ab = connection.execute('SELECT EmbeddedActiveAbilityIds, ModalChildIds FROM Abilities WHERE Id=?', (parent,)).fetchone()
        if not row_ab: continue
        children = []
        for col in ('EmbeddedActiveAbilityIds', 'ModalChildIds'):
            for item in (row_ab[col] or '').split(','):
                if item:
                    children.append(int(item.split(':')[0]))
        for child in children:
            if child in seen: continue
            seen.add(child); pending.append(child)
            text = connection.execute('SELECT l.Loc, a.Category, a.LoyaltyCost FROM Abilities a JOIN Localizations_enUS l ON l.LocId=a.TextId WHERE a.Id=?', (child,)).fetchone()
            if text:
                entry={'id': child, 'text': text['Loc']}
                if text['LoyaltyCost']: entry['loyalty']=text['LoyaltyCost']
                all_entries.append(entry)
                if text['Category'] in (1, 4, 7, 8):
                    entries.append(entry)
    if entries: abilities[str(row['GrpId'])] = entries
    if all_entries: all_abilities[str(row['GrpId'])] = all_entries
definitions = [{'id':row['Id'], 'text':row['Loc'], 'loyalty':row['LoyaltyCost']}
    for row in connection.execute('SELECT a.Id,l.Loc,a.LoyaltyCost FROM Abilities a JOIN Localizations_enUS l ON l.LocId=a.TextId WHERE a.Category IN (1,2,3,4,7,8)')]
(output_dir / 'arena-granted-ability-definitions.json').write_text(json.dumps(definitions,separators=(',',':')),encoding='utf8')
connection.close()
(output_dir / 'arena-activated-abilities.json').write_text(json.dumps(abilities,separators=(',',':')),encoding='utf8')
(output_dir / 'arena-ability-text.json').write_text(json.dumps(all_abilities,separators=(',',':')),encoding='utf8')
(output_dir / 'arena-card-metadata.json').write_text(
    json.dumps(records, separators=(',', ':')), encoding='utf-8')
print(f'Exported {len(records)} Arena printing metadata records')
