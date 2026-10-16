"""Rerun failed gameplay checks, preserving original failures and their logs."""
import json
import sys
from pathlib import Path
from repair_arena_animations import Audit,ROOT,run_lock

def main():
    with run_lock():
        report=Path((ROOT/'analysis/server-checks/latest-report.txt').read_text(encoding='utf8').strip()).with_suffix('.json')
        data=json.loads(report.read_text(encoding='utf8'))
        if data['status']=='running':raise RuntimeError('Wait for the existing audit to finish')
        audit=Audit.__new__(Audit)
        audit.directory=report.parent;audit.results=data['results'];audit.coverage=data.get('coverage')
        audit.started=data['started_utc'];audit.finished=False
        commands={'Commander eligibility and two or four player games':
            [sys.executable,ROOT/'tools/check_commander_forge.py']}
        for script in (ROOT/'engine-bridge').glob('test_*.py'):
            title='Gameplay '+script.stem.removeprefix('test_').replace('_',' ')
            commands[title]=[sys.executable,script,*(['--native'] if script.name in ('test_battle.py','test_fetch_land.py') else [])]
        for mode in ('replacements','regeneration','facedown','copies','multistep','designations','ability-changes','colors','stats','phasing','illegal-attachments','blocking-restrictions'):
            commands['Gameplay animation '+mode]=[sys.executable,ROOT/'engine-bridge/test_animation_outcomes.py','--'+mode]
        try:
            for index,row in list(enumerate(audit.results)):
                if row['status']=='passed' or 'resolved_by' in row:continue
                if row['name'] not in commands:continue # Build/data failures require the complete audit.
                recheck_index=len(audit.results)
                if audit.run('Recheck '+row['name'],commands[row['name']],timeout=900):row['resolved_by']=recheck_index
        finally:
            audit.finished=True;audit.save()
        unresolved=[r for r in audit.results if r['status']!='passed' and 'resolved_by' not in r]
        print('Report: '+str(report.with_suffix('.md')))
        return 1 if unresolved else 0

if __name__=='__main__':raise SystemExit(main())
