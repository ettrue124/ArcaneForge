"""Real Java rules engine through the authenticated match simulation API."""
import json
import tempfile
import threading
import unittest
from pathlib import Path
from urllib.request import Request, urlopen

from server.engine import Engine
from server.http import ROOT, make_server


@unittest.skipUnless((ROOT / 'engine-bridge/launch.json').exists(), 'Forge bridge has not been built')
class ForgeIntegrationTests(unittest.TestCase):
    def test_real_forge_match_simulation_via_http(self):
        engine = Engine.from_config(ROOT / 'engine-bridge/launch.json')
        with tempfile.TemporaryDirectory() as tmp:
            server = make_server(Path(tmp) / 'test.sqlite', 0, engine)
            catalogue = json.loads((ROOT / 'data/catalogue.json').read_text(encoding='utf-8'))
            server.catalogue = {c['grpId']: c for c in catalogue}
            selected = {}
            for card in catalogue:
                if card.get('isBasicLand') and card['name'] in ('Plains', 'Forest'):
                    selected.setdefault(card['name'], card)
            server.store.import_cards(list(selected.values()))
            first = server.store.register('forge-test1', 'local-test-password')
            second = server.store.register('forge-test2', 'local-test-password')
            token = server.store.login('forge-test1', 'local-test-password')['token']
            deck1 = server.store.save_deck(first['id'], 'White fixture', [{'grpId': selected['Plains']['grpId'], 'quantity': 60}])
            deck2 = server.store.save_deck(second['id'], 'Green fixture', [{'grpId': selected['Forest']['grpId'], 'quantity': 60}])
            server.store.queue(first['id'], deck1['id'])
            match = server.store.queue(second['id'], deck2['id'])['match']
            worker = threading.Thread(target=server.serve_forever, daemon=True)
            worker.start()
            try:
                req = Request(f'http://127.0.0.1:{server.server_port}/api/matches/{match["id"]}/simulate',
                    method='POST', data=b'{"seed":42,"maxSeconds":30}',
                    headers={'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json'})
                with urlopen(req, timeout=220) as response:
                    result = json.loads(response.read())
                self.assertEqual(result['engine'], 'Forge')
                self.assertEqual(result['mode'], 'ai-vs-ai')
                self.assertFalse(result['timedOut'])
                self.assertFalse(result['arenaPlayable'])
                self.assertGreater(result['turns'], 1)
                self.assertEqual(len(result['initialSnapshot']['cards']), 120)
                for card in result['initialSnapshot']['cards']:
                    if card['zone'] == 'Library' or (card['zone'] == 'Hand' and card['ownerSeat'] != 1):
                        self.assertNotIn('grpId', card)
            finally:
                server.shutdown()
                server.server_close()
                worker.join(timeout=5)
                engine.close()


if __name__ == '__main__':
    unittest.main()
