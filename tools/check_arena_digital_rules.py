"""Instantiate the complete digital inventory and check focused digital interactions.

Run after Setup-Forge and tools/audit_arena_digital_cards.py. Uses an isolated
Forge game, never the running Arena match. This does not certify all gameplay.
"""
import json
import os
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]


def main():
    config = json.loads((ROOT / 'engine-bridge/launch.json').read_text(encoding='utf-8-sig'))
    command = config['command']
    main_index = command.index('ForgeEngineMain')
    prefix = command[:main_index]
    cp_index = prefix.index('-cp') + 1
    compiler = Path(command[0]).with_name('javac.exe' if os.name == 'nt' else 'javac')
    log_path = ROOT / 'data/arena-digital-checks.log'
    log_path.parent.mkdir(parents=True, exist_ok=True)
    environment = {**os.environ, **config.get('env', {})}
    with tempfile.TemporaryDirectory(prefix='arcane-digital-checks-') as build:
        compiled = subprocess.run([str(compiler), '-encoding', 'UTF-8', '-cp', prefix[cp_index],
            '-d', build, str(ROOT / 'tools/checks/ArenaDigitalRulesCheck.java')],
            cwd=ROOT, env=environment, capture_output=True, text=True, encoding='utf8', timeout=120)
        if compiled.returncode:
            print(compiled.stderr)
            return 1
        prefix[cp_index] = build + os.pathsep + prefix[cp_index]
        result = subprocess.run(prefix + ['ArenaDigitalRulesCheck', command[main_index + 1]],
            cwd=ROOT, env=environment, capture_output=True, text=True, encoding='utf8', timeout=180)
        log = result.stdout + '\n' + result.stderr
        log_path.write_text(log, encoding='utf8')
        success = result.returncode == 0 and 'PASS: full digital rule checks completed.' in result.stdout
        print('\n'.join(line for line in result.stdout.splitlines() if line.startswith('PASS:')))
        if not success:
            print('\n'.join(log.splitlines()[-35:]))
        print('Full diagnostic: ' + str(log_path))
        return 0 if success else 1


if __name__ == '__main__':
    raise SystemExit(main())
