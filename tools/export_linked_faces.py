"""Resolve only database-linked Arena faces; never guess IDs from card names."""
import json
from pathlib import Path
import re
import sqlite3
import argparse

ROOT=Path(__file__).resolve().parents[1]

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--database',type=Path,default=ROOT/'analysis/decompiled-rules/card-database.mtga')
    parser.add_argument('--output',type=Path,default=ROOT/'data/arena-linked-faces.json')
    args=parser.parse_args()
    with sqlite3.connect(args.database.resolve().as_uri()+'?mode=ro',uri=True) as db:
        rows={row[0]:row[1:] for row in db.execute('SELECT GrpId,TitleId,LinkedFaceGrpIds FROM Cards')}
        names=dict(db.execute('SELECT LocId,Loc FROM Localizations_enUS'))
        result={}
        for original,(title,links) in rows.items():
            if not links:continue
            candidates={}
            for face in [original]+[int(n) for n in re.findall(r'\d+',links)]:
                if face not in rows:raise ValueError('Missing linked printing '+str(face))
                name=names.get(rows[face][0])
                if name:
                    candidates.setdefault(name,set()).add(face)
            # Prototype printings can share a title. Keep the submitted face
            # rather than guess between two same-name printings.
            faces={name:original if original in ids else next(iter(ids))
                for name,ids in candidates.items() if original in ids or len(ids)==1}
            if faces:result[str(original)]=faces
    path=args.output
    path.write_text(json.dumps(result,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf8')
    print(f'Wrote {len(result)} linked-face maps to {path}')

if __name__=='__main__':main()
