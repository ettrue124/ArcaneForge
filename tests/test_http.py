import json
import base64
import uuid
import tempfile
import threading
import unittest
from pathlib import Path
from urllib.error import HTTPError
from urllib.request import Request, urlopen

from server.http import make_server
from server.bootstrap import Bootstrap


class HttpTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.TemporaryDirectory()
        cls.server = make_server(Path(cls.tmp.name) / 'test.sqlite', 0)
        cls.server.store.import_cards([{'grpId': 1, 'name': 'Plains', 'cardLimit': 0}])
        cls.thread = threading.Thread(target=cls.server.serve_forever, daemon=True)
        cls.thread.start()
        cls.base = f'http://127.0.0.1:{cls.server.server_port}'

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()
        cls.server.server_close()
        cls.thread.join(timeout=5)
        cls.tmp.cleanup()

    def request(self, path, method='GET', data=None, token=None, origin=None):
        headers = {'Content-Type': 'application/json'}
        if token:
            headers['Authorization'] = 'Bearer ' + token
        if origin:
            headers['Origin'] = origin
        req = Request(self.base + path, method=method, headers=headers,
                      data=json.dumps(data).encode() if data is not None else None)
        try:
            response = urlopen(req, timeout=10)
        except HTTPError as exc:
            response = exc
        with response:
            return response.status, json.loads(response.read())

    def account(self, name):
        code, _ = self.request('/api/register', 'POST', {'username': name, 'password': 'local-password'})
        self.assertEqual(code, 201)
        code, result = self.request('/api/login', 'POST', {'username': name, 'password': 'local-password'})
        self.assertEqual(code, 200)
        return result['token'], result['user']['id']

    def test_status_is_honest(self):
        code, data = self.request('/api/status')
        self.assertEqual(code, 200)
        self.assertFalse(data['playable'])
        self.assertFalse(data['arenaClientCompatible'])

    def test_was_login_identity_and_revocation_through_http(self):
        self.account('was-http-player')
        self.server.bootstrap = Bootstrap(self.server.store)
        try:
            basic = base64.b64encode(b'arena-local:local-only').decode()
            req = Request(self.base + '/auth/oauth/token',
                data=b'grant_type=password&username=was-http-player&password=local-password',
                headers={'Content-Type': 'application/x-www-form-urlencoded', 'Authorization': 'Basic ' + basic})
            with urlopen(req, timeout=10) as response:
                login = json.loads(response.read())
            token = login['access_token']
            code, profile = self.request('/profile', token=token)
            self.assertEqual(code, 200)
            self.assertEqual(profile['personaID'], login['persona_id'])
            code, local_identity = self.request('/api/me', token=token)
            self.assertEqual(code, 200)
            self.assertEqual(local_identity['persona_id'], login['persona_id'])
            self.assertEqual(self.request('/api/logout', 'POST', {}, token)[0], 200)
            self.assertEqual(self.request('/profile', token=token)[0], 401)
            refresh = Request(self.base + '/auth/oauth/token',
                data=('grant_type=refresh_token&refresh_token=' + login['refresh_token']).encode(),
                headers={'Content-Type': 'application/x-www-form-urlencoded', 'Authorization': 'Basic ' + basic})
            with self.assertRaises(HTTPError) as denied:
                urlopen(refresh, timeout=10)
            self.assertEqual(denied.exception.code, 401)
        finally:
            self.server.bootstrap = None

    def test_native_arena_deck_commands_over_authenticated_http(self):
        token, _ = self.account('arena-http-owner')
        other, _ = self.account('arena-http-other')
        guid = str(uuid.uuid4())
        payload = {'Summary': {'DeckId': guid, 'Name': 'Native Builder Deck', 'Attributes': []},
                   'Deck': {'MainDeck': [{'cardId': 1, 'quantity': 60}], 'Sideboard': [],
                            'CommandZone': [], 'Companions': [], 'CardSkins': []}, 'ActionType': 0}
        code, result = self.request('/api/arena/command', 'POST',
            {'name': 'DeckUpsertDeckV3', 'payload': payload}, token)
        self.assertEqual(code, 200)
        self.assertEqual(result['result']['DeckId'], guid)
        code, result = self.request('/api/arena/command', 'POST',
            {'name': 'DeckGetDeck', 'payload': {'DeckId': guid}}, token)
        self.assertEqual(code, 200)
        self.assertEqual(result['result']['MainDeck'], [{'cardId': 1, 'quantity': 60}])
        code, _ = self.request('/api/arena/command', 'POST',
            {'name': 'DeckDeleteDeck', 'payload': {'DeckId': guid}}, other)
        self.assertEqual(code, 404)
        code, result = self.request('/api/arena/command', 'POST',
            {'name': 'DeckDeleteDeck', 'payload': {'DeckId': guid}}, token)
        self.assertEqual((code, result['result']), (200, 'success'))

    def test_unauthorized_and_cross_origin(self):
        self.assertEqual(self.request('/api/decks')[0], 401)
        self.assertEqual(self.request('/api/register', 'POST', {}, origin='https://foreign.invalid')[0], 403)
        self.assertEqual(self.request('/api/register', 'POST', [1, 2])[0], 400)

    def test_owned_decks_and_queue_pairing(self):
        first, uid1 = self.account('http-player1')
        second, uid2 = self.account('http-player2')
        payload = {'name': 'Basic deck', 'cards': [{'grpId': 1, 'quantity': 60}]}
        _, deck1 = self.request('/api/decks', 'POST', payload, first)
        _, deck2 = self.request('/api/decks', 'POST', payload, second)
        self.assertEqual(self.request(f"/api/decks/{deck1['id']}", token=second)[0], 404)
        _, q1 = self.request('/api/queue', 'POST', {'deckId': deck1['id']}, first)
        self.assertEqual(q1['status'], 'queued')
        _, q2 = self.request('/api/queue', 'POST', {'deckId': deck2['id']}, second)
        self.assertEqual(q2['status'], 'matched')
        self.assertEqual(q2['match']['status'], 'pending_engine')
        _, matches = self.request('/api/matches', token=first)
        self.assertEqual(len(matches), 1)
        self.assertEqual(set(matches[0]['players']), {uid1, uid2})

    def test_logout_revokes_token(self):
        token, _ = self.account('logout-player')
        self.assertEqual(self.request('/api/me', token=token)[0], 200)
        self.assertEqual(self.request('/api/logout', 'POST', {}, token)[0], 200)
        self.assertEqual(self.request('/api/me', token=token)[0], 401)

    def test_direct_challenge_over_http(self):
        first, _ = self.account('challenge-http1')
        second, uid2 = self.account('challenge-http2')
        payload = {'name': 'Challenge deck', 'cards': [{'grpId': 1, 'quantity': 60}]}
        _, d1 = self.request('/api/decks', 'POST', payload, first)
        _, d2 = self.request('/api/decks', 'POST', payload, second)
        code, challenge = self.request('/api/challenges', 'POST', {'deckId': d1['id'], 'opponentId': str(uid2)}, first)
        self.assertEqual(code, 201)
        code, match = self.request(f"/api/challenges/{challenge['id']}/accept", 'POST', {'deckId': d2['id']}, second)
        self.assertEqual(code, 200)
        self.assertEqual(match['status'], 'pending_engine')


if __name__ == '__main__':
    unittest.main()
