"""Inventory exact digital-card rules matches without assuming paper substitutions work."""
import json
from pathlib import Path
import sys
from datetime import datetime,timezone

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from server.engine import Engine


def main():
    path=ROOT/'data/arena-digital-support.json'
    report=json.loads(path.read_text(encoding='utf8'))
    rows=report['cards']
    catalogue=json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'))
    installed={card['name'] for card in catalogue}
    # Oracle names join faces with //; native/Forge identities use the front.
    # Specialized forms must remain linked to their base card, not deck entries.
    names=sorted({name for row in rows for name in (row['name'],row['name'].split(' // ')[0])})
    resolutions={}
    with Engine.from_config(ROOT/'engine-bridge/launch.json') as engine:
        for start in range(0,len(names),200):
            reply=engine.cards(names=names[start:start+200],timeout=180)
            resolutions.update({row['name']:row for row in reply['cards']})
            print('Checked',min(start+200,len(names)),'of',len(names),'digital/rebalanced names',flush=True)
    faces={face:result['forgeName'] for result in resolutions.values()
           if result.get('supported') for face in result.get('rulesFaces',[])}
    for row in rows:
        found=resolutions[row['name']]
        exact=found.get('supported') and found.get('forgeName')==row['name']
        front=row['name'].split(' // ')[0]
        linked=faces.get(row['name'])
        front_found=resolutions.get(front,{})
        multiface=(' // ' in row['name'] and front_found.get('supported') and
                   all(face in front_found.get('rulesFaces',[]) for face in row['name'].split(' // ')))
        parent=front if multiface else linked
        available=bool(exact or parent)
        row.update(installed=row['name'] in installed or bool(parent and parent in installed),
                   exactRulesName=bool(exact),rulesAvailable=available,linkedRulesName=parent,
                   forgeName=found.get('forgeName'),
                   status='requires_rules_verification' if available else 'missing_exact_rules')
    report.update(checkedAt=datetime.now(timezone.utc).isoformat(),verifiedAllCards=False,
        summary={'cards':len(rows),'installed':sum(row['installed'] for row in rows),
                 'exactRulesNames':sum(row['exactRulesName'] for row in rows),
                 'linkedRulesNames':sum(bool(row['linkedRulesName']) and not row['exactRulesName'] for row in rows),
                 'rulesAvailable':sum(row['rulesAvailable'] for row in rows),
                 'missingRules':sum(not row['rulesAvailable'] for row in rows),
                 'missingExactRules':sum(not row['exactRulesName'] for row in rows)})
    path.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    print(json.dumps(report['summary']))


if __name__=='__main__':main()
