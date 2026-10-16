"""Exercise the real Java JSON boundary under a legacy Windows stdout encoding."""
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from server.engine import Engine

config = json.loads((ROOT/'engine-bridge/launch.json').read_text(encoding='utf-8-sig'))
command = list(config['command'])
command.insert(1, '-Dstdout.encoding=windows-1252')
engine = Engine(command, Path(config['cwd']), default_timeout=180, env=config.get('env'))
try:
    # Unknown names are returned verbatim with supported=False. That gives an
    # exact round trip without requiring invented cards to exist in Forge.
    names = ['Forest', 'Encoding fixture — café 日本語']
    response = engine.request('cards', names=names)
    assert [card['name'] for card in response['cards']] == names, 'Java corrupted Unicode at the JSON boundary'
    assert response['cards'][0]['supported'] and not response['cards'][1]['supported']
    print('PASS: real Forge replies remain UTF-8 with Windows-1252 standard output; em dash, accents and Japanese survive.')
finally:
    engine.close()
