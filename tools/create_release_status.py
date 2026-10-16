"""Summarize the completed audit without equating tests with full visual parity."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def main():
    audit_path = Path((ROOT/'analysis/server-checks/latest-report.txt').read_text(encoding='utf8').strip()).with_suffix('.json')
    audit = json.loads(audit_path.read_text(encoding='utf8'))
    completed = audit['status'] != 'running'
    checks = audit['results']
    failed = [c['name'] for c in checks if c['status'] != 'passed' and not (
        isinstance(c.get('resolved_by'),int) and 0<=c['resolved_by']<len(checks)
        and checks[c['resolved_by']]['status']=='passed')]
    pending = [
        'Partial non-keyword suppression and unmatched granted keyword definitions',
        'Source-attributed counter-transfer intermediate playback',
        'Remaining layered/perpetual changes and choice-result domains',
        'Remaining designation and illegal-attachment source identities',
        'Remaining multistep categories',
        'Recorded live visual/audio/timing acceptance for the supported client version',
    ]
    result = dict(release_claim='Full Arena animation parity', release_ready=False,
        automated_audit_completed=completed, automated_checks_passed=completed and not failed,
        automated_checks_total=len(checks), failed_checks=failed, open_release_gates=pending,
        audit_report=str(audit_path.with_suffix('.md')), acceptance_checklist=str(ROOT/'RELEASE-READINESS.md'))
    target=ROOT/'analysis/release-status.json'
    target.write_text(json.dumps(result,indent=2)+'\n',encoding='utf8')
    text=['# Animation release status','', '**Not ready for a full-parity publication.**', '',
        f'Automated audit: {"complete" if completed else "still running"}; {len(checks)-len(failed)}/{len(checks)} recorded checks passed.',
        '',f'[Audit report]({audit_path.with_suffix(".md").as_posix()})',
        '', '## Open release gates', '']+['- '+gate for gate in pending]
    if failed:text+=['','## Failed checks','']+['- '+name for name in failed]
    (ROOT/'analysis/release-status.md').write_text('\n'.join(text)+'\n',encoding='utf8')
    print(f'Release status: {target}; ready=false')
    return 0 if completed and not failed else 1

if __name__=='__main__':raise SystemExit(main())
