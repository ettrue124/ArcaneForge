"""Export exact source-to-token links and token printings from local Arena data."""
import json
from pathlib import Path
import re
import sqlite3

ROOT=Path(__file__).resolve().parents[1]

def main():
    with sqlite3.connect(ROOT/'analysis/decompiled-rules/card-database.mtga') as db:
        names=dict(db.execute('SELECT LocId,Loc FROM Localizations_enUS'))
        tokens={str(gid):{'name':names.get(title,''),'power':power,'toughness':toughness,
            'signature': '|'.join(map(str,(title,power,toughness,colors,types,subtypes,abilities)))}
            for gid,title,power,toughness,colors,types,subtypes,abilities in db.execute(
                'SELECT GrpId,TitleId,Power,Toughness,Colors,Types,Subtypes,AbilityIds FROM Cards WHERE IsToken=1')}
        links={str(gid):sorted({int(token) for _,token in re.findall(r'(\d+):(\d+)',raw) if token in tokens})
            for gid,raw in db.execute("SELECT GrpId,AbilityIdToLinkedTokenGrpId FROM Cards WHERE AbilityIdToLinkedTokenGrpId<>''")}
    output=ROOT/'data/arena-generated-cards.json'
    output.write_text(json.dumps({'tokens':tokens,'links':links},ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf8')
    print(f'Exported {len(tokens)} token printings and {len(links)} source links')

if __name__=='__main__':main()
