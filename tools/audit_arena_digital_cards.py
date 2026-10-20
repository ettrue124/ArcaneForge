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
    names=sorted({row['name'] for row in rows})
    resolutions={}
    with Engine.from_config(ROOT/'engine-bridge/launch.json') as engine:
        for start in range(0,len(names),200):
            reply=engine.cards(names=names[start:start+200],timeout=180)
            resolutions.update({row['name']:row for row in reply['cards']})
            print('Checked',min(start+200,len(names)),'of',len(names),'digital/rebalanced names',flush=True)
    for row in rows:
        found=resolutions[row['name']]
        exact=found.get('supported') and found.get('forgeName')==row['name']
        row.update(installed=row['name'] in installed,exactRulesName=bool(exact),
                   forgeName=found.get('forgeName'),
                   status='requires_rules_verification' if exact else 'missing_exact_rules')
    report.update(checkedAt=datetime.now(timezone.utc).isoformat(),verifiedAllCards=False,
        summary={'cards':len(rows),'installed':sum(row['installed'] for row in rows),
                 'exactRulesNames':sum(row['exactRulesName'] for row in rows),
                 'missingExactRules':sum(not row['exactRulesName'] for row in rows)})
    path.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    print(json.dumps(report['summary']))


if __name__=='__main__':main()
