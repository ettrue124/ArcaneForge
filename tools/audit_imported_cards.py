"""Compare installed imported card display fields with their cached Oracle data."""
import argparse
from collections import Counter
from contextlib import closing
import json
from pathlib import Path
import re
import sqlite3

ROOT=Path(__file__).resolve().parents[1]

def decode_cost(value):
    return re.sub(r'o(\([^)]*\)|\d+|[A-Z]+)',lambda m:'{'+m[1].strip('()')+'}',value or '')

def decode_text(value):
    return re.sub(r'\{(o[^}]*)\}',lambda m:decode_cost(m[1]),value or '')

def audit(database):
    records=json.loads((ROOT/'data/custom-arena-cards/manifest.json').read_text(encoding='utf8'))
    issues=[];counts=Counter()
    with closing(sqlite3.connect(database.resolve().as_uri()+'?mode=ro',uri=True)) as db:
        db.row_factory=sqlite3.Row
        cards={r['GrpId']:dict(r) for r in db.execute('SELECT * FROM Cards')}
        locs={r[0]:r[1] for r in db.execute('SELECT LocId,Loc FROM Localizations_enUS ORDER BY Formatted DESC')}
        enums={}
        for kind,value,locid in db.execute('SELECT Type,Value,LocId FROM Enums'):
            enums.setdefault(kind,{})[value]=locs.get(locid,'')
        abilities={r[0]:locs.get(r[1],'') for r in db.execute('SELECT Id,TextId FROM Abilities')}
        loyalty_costs=dict(db.execute('SELECT Id,LoyaltyCost FROM Abilities'))
        def labels(kind,value):
            return [enums[kind].get(int(v),v) for v in value.split(',') if v]
        for record in records:
            source=json.loads((ROOT/record['metadata']).read_text(encoding='utf8'))
            card=cards.get(record['grpId']);bad=[]
            if card is None:
                bad.append('missing printing')
            else:
                actual={'name':locs.get(card['TitleId'],''),'mana':decode_cost(card['OldSchoolManaText']),
                        'power':card['Power'],'toughness':card['Toughness']}
                expected={'name':record['name'],'mana':source['mana_cost'],
                          'power':source.get('power',''),'toughness':source.get('loyalty','') if 'Planeswalker' in source['type_line'].split(' — ')[0].split() else source.get('toughness','')}
                bad.extend(k for k in actual if str(actual[k] or '')!=str(expected[k] or ''))
                colors={'W':'White','U':'Blue','B':'Black','R':'Red','G':'Green'}
                if sorted(labels('CardColor',card['Colors']))!=sorted(colors[c] for c in source['colors']):bad.append('colors')
                primary,_,sub=source['type_line'].partition(' — ')
                if sorted(labels('SuperType',card['Supertypes'])+labels('CardType',card['Types']))!=sorted(primary.split()):bad.append('types')
                if sorted(labels('SubType',card['Subtypes']))!=sorted(sub.split()):bad.append('subtypes')
                oracle=source['oracle_text'].replace(source['name'],record['name'])
                if source['name']!=record['name'] and ',' in source['name']:
                    oracle=oracle.replace(source['name'].split(',')[0],record['name'].split(',')[0])
                text='\n'.join((str(loyalty_costs[int(v.split(':')[0])])+': ' if loyalty_costs[int(v.split(':')[0])] else '')
                    +decode_text(abilities[int(v.split(':')[0])]) for v in card['AbilityIds'].split(',') if v)
                if text.replace('\u2212','-')!=oracle.replace('\u2212','-'):bad.append('rules text/symbols')
                if card['ArtId']!=record['artId']:bad.append('art link')
            if bad:
                issues.append({'name':record['name'],'grpId':record['grpId'],'fields':bad});counts.update(bad)
    return {'checkedPrintings':len(records),'mismatchedPrintings':len(issues),'fieldCounts':dict(counts),'issues':issues,
            'scope':'Card display fields versus cached source data; not every gameplay interaction or a visual artwork comparison.'}

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--database',type=Path)
    parser.add_argument('--output',type=Path,default=ROOT/'analysis/imported-card-audit/verified.json')
    args=parser.parse_args()
    if args.database is None:
        config=json.loads((ROOT/'data/local-client/configuration-state.json').read_text(encoding='utf-8-sig'))
        paths=list((Path(config['installation'])/'MTGA_Data/Downloads/Raw').glob('Raw_CardDatabase_*.mtga'))
        if len(paths)!=1:raise ValueError('Expected one Arena card database')
        args.database=paths[0]
    report=audit(args.database)
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding='utf8')
    print(json.dumps({k:v for k,v in report.items() if k!='issues'}))
    return 1 if report['issues'] else 0

if __name__=='__main__':raise SystemExit(main())
