"""Export exact source-to-token links and token printings from local Arena data."""
import json
import argparse
from pathlib import Path
import re
import sqlite3

ROOT=Path(__file__).resolve().parents[1]

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--database',type=Path,default=ROOT/'analysis/decompiled-rules/card-database.mtga')
    parser.add_argument('--output',type=Path,default=ROOT/'data/arena-generated-cards.json')
    args=parser.parse_args()
    database=args.database.resolve()
    with sqlite3.connect(database.as_uri()+'?mode=ro',uri=True) as db:
        names=dict(db.execute('SELECT LocId,Loc FROM Localizations_enUS'))
        ability_text={aid:names.get(tid,'') for aid,tid in db.execute('SELECT Id,TextId FROM Abilities')}
        color_names={str(value):names.get(loc,'') for value,loc in db.execute("SELECT Value,LocId FROM Enums WHERE Type='CardColor'")}
        type_names={str(value):names.get(loc,'') for value,loc in db.execute("SELECT Value,LocId FROM Enums WHERE Type='CardType'")}
        tokens={str(gid):{'name':names.get(title,''),'power':power,'toughness':toughness,
            'signature': '|'.join(map(str,(title,power,toughness,colors,types,subtypes,abilities)))}
            for gid,title,power,toughness,colors,types,subtypes,abilities in db.execute(
                'SELECT GrpId,TitleId,Power,Toughness,Colors,Types,Subtypes,AbilityIds FROM Cards WHERE IsToken=1')}
        for token in tokens.values():
            parts=token['signature'].split('|')
            token['colors']=[color_names.get(v,'') for v in parts[3].split(',') if v]
            token['types']=[type_names.get(v,'') for v in parts[4].split(',') if v]
            token['rules']=[ability_text.get(int(v.split(':')[0]),'') for v in parts[6].split(',') if v]
        # Treasure's two native wordings have identical rules. Keep all other
        # token signatures strict, including tokens with different abilities.
        treasure_abilities={}
        for aid,textid in db.execute('SELECT Id,TextId FROM Abilities'):
            text=names.get(textid,'').replace('Sacrifice CARDNAME:', 'Sacrifice this artifact:')
            if text=='{oT}, Sacrifice this artifact: Add one mana of any color.':
                treasure_abilities[aid]=textid
        if treasure_abilities:
            canonical=min(treasure_abilities)
            for token in tokens.values():
                if token['name']!='Treasure':continue
                parts=token['signature'].split('|')
                if len(parts)==7 and re.fullmatch(r'\d+:\d+',parts[6]):
                    aid=int(parts[6].split(':')[0])
                    if aid in treasure_abilities:
                        parts[6]=f'{canonical}:{treasure_abilities[canonical]}'
                        token['signature']='|'.join(parts)
        # Ability IDs can differ between reprints with identical rules.
        # Compare the exact rules text rather than the allocation of those IDs.
        for token in tokens.values():
            parts=token['signature'].split('|')
            parts[6]='\x1f'.join(token['rules'])
            if token['name']=='Treasure':
                parts[6]=parts[6].replace('Sacrifice CARDNAME:', 'Sacrifice this artifact:')
            token['signature']='|'.join(parts)
        links={str(gid):sorted({int(token) for _,token in re.findall(r'(\d+):(\d+)',raw) if token in tokens})
            for gid,raw in db.execute("SELECT GrpId,AbilityIdToLinkedTokenGrpId FROM Cards WHERE AbilityIdToLinkedTokenGrpId<>''")}
    keys={}
    custom=ROOT/'data/custom-arena-cards/tokens.json'
    if custom.exists():
        for row in json.loads(custom.read_text(encoding='utf8'))['tokens']:
            if str(row['grpId']) in tokens:
                key=row['key'].replace('\u2014','-').replace('\u2013','-').replace('\ufffd','-')
                keys[key]=row['grpId']
    output=args.output
    output.write_text(json.dumps({'tokens':tokens,'links':links,'keys':keys},ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf8')
    print(f'Exported {len(tokens)} token printings and {len(links)} source links')

if __name__=='__main__':main()
