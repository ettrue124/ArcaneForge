"""Real FrontDoor auth through the Python backend; no persistent test accounts."""
import json
import shutil
import subprocess
import tempfile
import threading
import unittest
from pathlib import Path

from server.engine import Engine
from server.arena import Arena
from server.http import ROOT, make_server


@unittest.skipUnless(shutil.which('dotnet') and (ROOT / 'client-inspection' / 'Google.Protobuf.dll').exists(),
                     'Requires .NET SDK and extracted client protocol DLLs')
class ProtocolIntegrationTests(unittest.TestCase):
    def test_actual_backend_token_to_frontdoor_session(self):
        engine_config = ROOT / 'engine-bridge/launch.json'
        engine = Engine.from_config(engine_config) if engine_config.exists() else None
        with tempfile.TemporaryDirectory() as directory:
            backend = make_server(Path(directory) / 'test.sqlite', 0, engine)
            catalogue_path = ROOT / 'data' / 'catalogue.json'
            if catalogue_path.exists():
                cards = json.loads(catalogue_path.read_text(encoding='utf-8'))
                backend.store.import_cards(cards)
                backend.catalogue = {c['grpId']: c for c in cards}
                backend.arena = Arena(backend.store, cards)
            worker = threading.Thread(target=backend.serve_forever, daemon=True)
            worker.start()
            try:
                result = subprocess.run([
                    'dotnet', 'run', '--project', 'protocol-host', '--',
                    '--backend-self-test', f'http://127.0.0.1:{backend.server_port}'
                ], cwd=ROOT, capture_output=True, text=True, timeout=90)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                self.assertIn('PASS: real backend', result.stdout)
            finally:
                backend.shutdown()
                backend.server_close()
                worker.join(timeout=5)
                if engine:
                    engine.close()


if __name__ == '__main__':
    unittest.main()
