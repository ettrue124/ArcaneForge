"""Build the protocol host only when its sources or compiled files change."""
import json
from pathlib import Path
import subprocess
import sys

from prepare_startup import sources, stat, read

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'protocol-host'
OUTPUT = PROJECT / 'bin/live'
CACHE = ROOT / 'data/local-client/protocol-host-build.json'


def main():
    inputs = [p for p in PROJECT.rglob('*') if p.is_file()
              and not {'bin', 'obj'}.intersection(p.relative_to(PROJECT).parts)
              and p.suffix in ('.cs', '.csproj', '.props', '.targets')]
    inputs += list((ROOT / 'client-inspection').glob('*.dll'))
    inputs += [p for name in ('global.json', 'Directory.Build.props', 'Directory.Build.targets',
                             'Directory.Packages.props', 'NuGet.Config', 'nuget.config')
               if (p := ROOT / name).is_file()]
    inputs += [Path(__file__)]
    signature = sources(inputs)
    outputs = sorted(p for p in OUTPUT.glob('*') if p.is_file())
    previous = read(CACHE)
    required = ('ProtocolHost.dll', 'ProtocolHost.deps.json', 'ProtocolHost.runtimeconfig.json')
    if (all((OUTPUT / name).is_file() for name in required)
            and previous.get('inputs') == signature
            and previous.get('outputs') == [stat(p) for p in outputs]):
        print('Server code unchanged; using compiled host.', flush=True)
        return
    print('Preparing updated server code...', flush=True)
    command = ['dotnet', 'build', str(PROJECT), '-o', str(OUTPUT), '--nologo']
    result = subprocess.run(command, cwd=ROOT, stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT, text=True)
    if result.returncode:
        print(result.stdout, file=sys.stderr)
        raise SystemExit(result.returncode)
    if not all((OUTPUT / name).is_file() for name in required):
        raise RuntimeError('Protocol host build did not produce the required files')
    outputs = sorted(p for p in OUTPUT.glob('*') if p.is_file())
    CACHE.parent.mkdir(parents=True, exist_ok=True)
    temporary = CACHE.with_suffix('.tmp')
    temporary.write_text(json.dumps({'inputs': signature, 'outputs': [stat(p) for p in outputs]}, indent=2),
                         encoding='utf8')
    temporary.replace(CACHE)


if __name__ == '__main__':
    main()
