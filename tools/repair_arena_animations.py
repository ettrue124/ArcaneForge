"""Rebuild known local adapters, exercise gameplay, and report remaining gaps.

Run from any directory. Uses isolated test accounts/matches. No live-server
restart, game installation changes, or requests to official game servers.
"""
from contextlib import contextmanager
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
MANAGED = Path('C:/Program Files/Wizards of the Coast/MTGA/MTGA_Data/Managed')
OUTPUT = ROOT / 'analysis/server-checks'
HOST = ROOT / 'protocol-host/bin/TargetAudit/ProtocolHost.dll'
GENERATED = ['arena-card-metadata.json', 'arena-ability-text.json',
             'arena-activated-abilities.json', 'arena-linked-faces.json', 'arena-generated-cards.json']


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


@contextmanager
def run_lock():
    """An OS lock releases on exit/crash; no stale PID guessing or process kills."""
    OUTPUT.mkdir(parents=True, exist_ok=True)
    with (OUTPUT / 'runner.lock').open('a+b', buffering=0) as stream:
        stream.seek(0)
        if not stream.read(1):
            stream.write(b'0')
            stream.flush()
        stream.seek(0)
        if os.name == 'nt':
            import msvcrt
            msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
        else:
            import fcntl
            fcntl.flock(stream.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        try:
            yield
        finally:
            stream.seek(0)
            if os.name == 'nt':
                msvcrt.locking(stream.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(stream.fileno(), fcntl.LOCK_UN)


def stop_child(process):
    if process.poll() is not None:
        return
    # Terminate only this runner's child tree, including its isolated Java engine.
    if os.name == 'nt':
        subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=20)
    else:
        import signal
        os.killpg(process.pid, signal.SIGKILL)
    try:
        process.wait(timeout=10)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=10)


class Audit:
    def __init__(self):
        stamp = datetime.now().strftime('%Y%m%d-%H%M%S')
        self.directory = OUTPUT / f'{stamp}-{os.getpid()}'
        self.directory.mkdir(parents=True)
        self.results = []
        self.coverage = None
        self.finished = False
        self.started = datetime.now(timezone.utc).isoformat()
        self.save()

    def record(self, name, status, detail='', log=None, seconds=0):
        self.results.append(dict(name=name, status=status, detail=detail, log=log, seconds=round(seconds, 1)))
        print(f'[{status.upper()}] {name}' + (f': {detail}' if detail else ''), flush=True)
        self.save()
        return status == 'passed'

    def run(self, name, command, timeout=600, reject_skips=False):
        logfile = self.directory / f'{len(self.results)+1:02d}-{re.sub("[^a-z0-9]+", "-", name.lower()).strip("-")}.log'
        print(f'\n{name}...', flush=True)
        start = time.monotonic()
        process = None
        status, detail = 'failed', ''
        try:
            with logfile.open('w', encoding='utf8') as log:
                options = {'creationflags': subprocess.CREATE_NEW_PROCESS_GROUP} if os.name == 'nt' else {'start_new_session': True}
                process = subprocess.Popen([str(v) for v in command], cwd=ROOT, stdout=log, stderr=subprocess.STDOUT,
                                           env={**os.environ, 'PYTHONUNBUFFERED': '1'}, **options)
                while True:
                    remaining = timeout - (time.monotonic() - start)
                    if remaining <= 0:
                        stop_child(process)
                        detail = f'Timed out after {timeout}s. Its test processes were stopped.'
                        break
                    try:
                        code = process.wait(timeout=min(30, remaining))
                        status = 'passed' if code == 0 else 'failed'
                        detail = '' if code == 0 else f'Exit code {code}; see {logfile.name}'
                        break
                    except subprocess.TimeoutExpired:
                        print(f'  Still running ({int(time.monotonic()-start)}s); log: {logfile.name}', flush=True)
        except KeyboardInterrupt:
            if process:
                stop_child(process)
            self.record(name, 'interrupted', 'Stopped by the user.', logfile.name, time.monotonic()-start)
            raise
        except (OSError, subprocess.SubprocessError) as error:
            if process:
                stop_child(process)
            detail = str(error)
        if status == 'passed' and reject_skips:
            output = logfile.read_text(encoding='utf8', errors='replace')
            if re.search(r'\bskipped=[1-9]|\.\.\. skipped ', output):
                status, detail = 'incomplete', 'Some tests were skipped; see log for prerequisites.'
        return self.record(name, status, detail, logfile.name, time.monotonic()-start)

    def save(self):
        failures = [r for r in self.results if r['status'] != 'passed' and not (
            isinstance(r.get('resolved_by'),int) and 0<=r['resolved_by']<len(self.results)
            and self.results[r['resolved_by']]['status']=='passed')]
        status = 'running' if not self.finished else 'checks_need_attention' if failures else 'checks_passed_visual_review_needed'
        report = dict(started_utc=self.started, status=status, results=self.results, coverage=self.coverage,
                      visual_validation='Not performed. Headless checks cannot prove every Unity animation renders correctly.',
                      activation='Restart the local server after checking the results to load rebuilt components.')
        path = self.directory / 'report.json'
        temporary = path.with_suffix('.tmp')
        temporary.write_text(json.dumps(report, indent=2), encoding='utf8')
        temporary.replace(path)
        lines = ['# Arena repair and check report', '', f'Status: **{status.replace("_", " ")}**', '',
                 'This checks the implemented adapters and available gameplay scenarios. It does not claim that every Arena animation is implemented or visually verified.', '',
                 '| Check | Result | Details |', '| --- | --- | --- |']
        for row in self.results:
            info = row['detail'].replace('|', '/')
            if isinstance(row.get('resolved_by'),int):info += ' Resolved by recorded recheck.'
            if row['log']:
                info += f' [Log]({row["log"]})'
            lines.append(f'| {row["name"]} | {row["status"]} | {info} |')
        if self.coverage is not None:
            lines += ['', '## Animation inventory', '',
                      f'{len(self.coverage["explicit"])} annotation kinds have explicit emission sites; '
                      f'{len(self.coverage["unclaimed"])} have no explicit emission site found.', '',
                      'A source reference is not a successful animation test. Some effects are generated from board changes without explicit annotations.', '',
                      '| Annotation | Source inventory |', '| --- | --- |']
            for label, values in [('Explicit emission found', self.coverage['explicit']), ('Needs review; no explicit emission found', self.coverage['unclaimed'])]:
                lines.extend(f'| {name} | {label} |' for name in values)
        lines += ['', '## Next steps', '',
                  'Fix failed checks before using this build. Restart the local server to load rebuilt components; the running server was not restarted.', '',
                  'Play a local match to check actual visuals: cast a targeted spell, attack and block, equip and move Equipment, create and sacrifice a token, change counters, and finish the match. Local gameplay traces are saved under `analysis/arena-traces`.', '',
                  'Known remaining work includes specialized replacement/prevention visuals, unmatched ability identities, specialized choice browsers, and timing in the actual Arena scene. Missing adapters require individual implementation; this script cannot generate them from asset names.', '']
        (self.directory / 'report.md').write_text('\n'.join(lines), encoding='utf8')
        (OUTPUT / 'latest-report.txt').write_text(str(self.directory / 'report.md')+'\n', encoding='utf8')


def prerequisites(audit):
    missing = [f'Tool: {name}' for name in ('dotnet', 'java', 'javac') if not shutil.which(name)]
    required = ['engine-bridge/launch.json', 'client-inspection/version', '.tools/ilspycmd.exe']
    missing.extend(f'File: {name}' for name in required if not (ROOT/name).is_file())
    for name in ['SharedClientCore.dll', 'Core.dll']:
        if not (MANAGED/name).is_file():
            missing.append(f'Installed client assembly: {MANAGED/name}')
    mismatches = []
    project = (ROOT/'protocol-host/ProtocolHost.csproj').read_text(encoding='utf8')
    for name in re.findall(r'client-inspection/([^<]+)', project):
        extracted, installed = ROOT/'client-inspection'/name, MANAGED/name
        if not extracted.is_file() or not installed.is_file():
            missing.append(f'Protocol assembly: {name}')
        elif digest(extracted) != digest(installed):
            mismatches.append(name)
    if missing:
        audit.record('Prerequisites and client version', 'failed', '; '.join(missing))
        return None
    installed_root = MANAGED.parent.parent
    old_version = json.loads((ROOT/'client-inspection/version').read_text(encoding='utf-8-sig'))['Versions']
    new_version = json.loads((installed_root/'version').read_text(encoding='utf-8-sig'))['Versions']
    if old_version != new_version:
        if not audit.run('Back up and refresh outdated client inputs',
                         [sys.executable, ROOT/'scripts/refresh_client.py', installed_root]):
            return None
        mismatches = [name for name in re.findall(r'client-inspection/([^<]+)', project)
                      if digest(ROOT/'client-inspection'/name) != digest(MANAGED/name)]
    if mismatches:
        audit.record('Prerequisites and client version', 'failed',
                     f'Assembly copies differ within the same client version: {", ".join(mismatches)}. Inspect the installation/extraction before replacing them.')
        return None
    # Every metadata exporter must read the same card database as the imported client.
    cached = ROOT/'analysis/decompiled-rules/card-database.mtga'
    imported = ROOT/'client-inspection/cards.mtga'
    if not imported.is_file():
        audit.record('Card database', 'failed', 'Imported cards.mtga is missing; refresh client extraction.')
        return None
    if not cached.is_file() or digest(cached) != digest(imported):
        cached.parent.mkdir(parents=True, exist_ok=True)
        if cached.is_file():
            shutil.copy2(cached, audit.directory/'card-database.before.mtga')
        staged = cached.with_suffix('.refresh-tmp')
        shutil.copy2(imported, staged)
        staged.replace(cached)
        audit.record('Refresh cached card database', 'passed', 'Exporters now use the imported client database; previous cache backed up if present.')
    if not (ROOT/'data/catalogue.json').is_file():
        audit.record('Card catalogue', 'failed', 'data/catalogue.json is missing; run scripts/prepare_client.py --catalogue-only.')
        return None
    config = json.loads((ROOT/'engine-bridge/launch.json').read_text(encoding='utf-8-sig'))
    command = config['command']
    cp = command[command.index('-cp')+1]
    absent = [p for p in cp.split(os.pathsep) if not Path(p).exists()]
    if Path(config['cwd']).resolve() != ROOT or absent:
        audit.record('Prerequisites and client version', 'failed', 'Forge launch paths are stale or dependencies missing. Run Setup-Forge.ps1 -SkipBuild, then rerun this script.')
        return None
    audit.record('Prerequisites and client version', 'passed', 'Installed and extracted protocol assemblies match.')
    return cp


def repair_metadata(audit):
    backup = audit.directory/'metadata-backup'
    backup.mkdir()
    for name in GENERATED:
        path = ROOT/'data'/name
        if path.exists():
            shutil.copy2(path, backup/name)
    def restore():
        for name in GENERATED:
            target = ROOT/'data'/name
            if (backup/name).exists():
                shutil.copy2(backup/name, target)
            else:
                target.unlink(missing_ok=True)
        audit.record('Metadata recovery', 'passed', 'Restored previous generated metadata after an incomplete export.')
    try:
        for exporter in ['engine-bridge/export_arena_metadata.py', 'tools/export_linked_faces.py', 'tools/export_generated_cards.py']:
            if not audit.run('Refresh '+Path(exporter).stem, [sys.executable, ROOT/exporter]):
                restore()
                return False
    except (Exception, KeyboardInterrupt):
        restore()
        raise
    audit.record('Metadata backup', 'passed', 'Previous generated data is saved in metadata-backup/.')
    return True


def perform(audit):
    cp = prerequisites(audit)
    if cp is None:
        audit.record('Builds and gameplay checks', 'blocked', 'Repair the reported prerequisites and rerun.')
        return
    metadata_ok = repair_metadata(audit)
    # Compile in a separate directory first so compilation failure cannot partially overwrite the bridge.
    classes = audit.directory/'bridge-classes'
    classes.mkdir()
    sources = sorted(p for p in (ROOT/'engine-bridge').glob('*.java') if not p.stem.endswith('Test'))
    sources += sorted((ROOT/'engine-bridge/overrides').rglob('*.java'))
    bridge_ok = audit.run('Build Forge bridge', ['javac', '-encoding', 'UTF-8', '-cp', cp, '-d', classes, *sources])
    if bridge_ok:
        for source in classes.rglob('*.class'):
            target = ROOT/'engine-bridge/build'/source.relative_to(classes)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
    host_ok = audit.run('Build Arena protocol host', ['dotnet', 'build', ROOT/'protocol-host', '-o', HOST.parent])
    if host_ok and metadata_ok:
        audit.run('Native Arena parsers and animation lifecycle', ['dotnet', HOST, '--self-test'])
    else:
        audit.record('Native Arena parsers and animation lifecycle', 'blocked', 'Build or metadata refresh failed.')
    if bridge_ok and metadata_ok:
        mapping_steps = [
            ('Refresh Forge card compatibility inventory', 'forge-coverage.json', 'scripts/forge_coverage.py'),
            ('Commander eligibility and two or four player games', 'commander-eligible.json', 'tools/check_commander_forge.py')]
        for title, filename, script in mapping_steps:
            target = ROOT/'data'/filename
            backup = audit.directory/(filename+'.before')
            existed = target.exists()
            if existed:
                shutil.copy2(target, backup)
            completed = False
            try:
                completed = audit.run(title, [sys.executable, ROOT/script], timeout=900)
            finally:
                if not completed:
                    if existed:
                        shutil.copy2(backup, target)
                    else:
                        target.unlink(missing_ok=True)
    else:
        audit.record('Card compatibility and Commander checks', 'blocked', 'Forge build or metadata refresh failed.')
    audit.run('Accounts decks rules and protocol integration', [sys.executable, '-m', 'unittest', 'discover', '-s', 'tests', '-v'],
              timeout=900, reject_skips=True)
    if bridge_ok and host_ok and metadata_ok:
        battle_ok = False
        scenarios = sorted((ROOT/'engine-bridge').glob('test_*.py'))
        for scenario in scenarios:
            args = ['--native'] if scenario.name in ('test_battle.py', 'test_fetch_land.py') else []
            passed = audit.run('Gameplay '+scenario.stem.removeprefix('test_').replace('_', ' '),
                               [sys.executable, scenario, *args], timeout=900)
            if scenario.name == 'test_battle.py':
                battle_ok = passed
            if scenario.name == 'test_animation_outcomes.py':
                for mode in ('replacements','regeneration','facedown','copies','multistep','designations','ability-changes','colors','stats','phasing','illegal-attachments','blocking-restrictions'):
                    audit.run('Gameplay animation '+mode,[sys.executable,scenario,'--'+mode],timeout=180)
        if battle_ok:
            audit.run('Replay freshly captured Forge events through Arena', ['dotnet', HOST, '--playback-self-test', ROOT/'analysis/playback-check.json'])
        else:
            audit.record('Replay freshly captured Forge events through Arena', 'blocked', 'The battle scenario did not produce a successful fresh trace.')
    else:
        audit.record('Forge gameplay scenarios', 'blocked', 'Build or metadata refresh failed; stale binaries were not used.')
    inventory_ok = audit.run('Refresh installed animation inventory', [sys.executable, ROOT/'tools/map_arena_animations.py'], timeout=1800)
    if inventory_ok:
        inventory = json.loads((ROOT/'analysis/animation-map/animation-map.json').read_text(encoding='utf8'))
        emitted = {m['annotation'] for m in inventory['mappings'] if m['bridge_emits_annotation']}
        all_types = {m['annotation'] for m in inventory['mappings']}
        audit.coverage = dict(explicit=sorted(emitted), unclaimed=sorted(all_types-emitted))
        audit.save()
    audit.run('Refresh native gameplay request inventory', [sys.executable, ROOT/'tools/extract_arena_actions.py'], timeout=1800)


def main():
    audit = None
    try:
        with run_lock():
            audit = Audit()
            print(f'Report: {audit.directory / "report.md"}', flush=True)
            try:
                perform(audit)
            except KeyboardInterrupt:
                audit.record('Check session', 'interrupted', 'Remaining checks were not run.')
            except Exception as error:
                audit.record('Check session', 'failed', str(error))
            finally:
                audit.finished = True
                audit.save()
            print(f'\nReport: {audit.directory / "report.md"}', flush=True)
            return 1 if any(r['status'] != 'passed' for r in audit.results) else 0
    except OSError as error:
        print(f'Could not start checks (another check may already be running): {error}', flush=True)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
