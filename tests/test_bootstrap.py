import base64
import concurrent.futures
import hashlib
import hmac
import json
from pathlib import Path
import tempfile
import unittest
from urllib.parse import urlencode

from server.bootstrap import Bootstrap, BootstrapError, installed_manifests
from server.storage import Store, StorageError


class BootstrapTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.store = Store(self.root / 'local.sqlite3')
        self.user = self.store.register('Local Player', 'password-local')
        self.basic = 'Basic ' + base64.b64encode(b'arena-local:local-only').decode()
        self.manifests = [{'priority': 50, 'category': '', 'hash': '1' * 32},
                          {'priority': 50, 'category': 'Audio', 'hash': '2' * 32}]
        self.bootstrap = Bootstrap(self.store, manifests=self.manifests)

    def tearDown(self):
        self.temp.cleanup()

    def login(self):
        return self.bootstrap.oauth({'grant_type': 'password', 'username': 'Local Player',
                                     'password': 'password-local'}, self.basic)

    def test_was_form_login_and_profile_contract(self):
        body = urlencode({'grant_type': 'password', 'username': 'Local Player', 'password': 'password-local'}).encode()
        status, login = self.bootstrap.dispatch('POST', '/auth/oauth/token',
            {'Authorization': self.basic, 'Content-Type': 'application/x-www-form-urlencoded; charset=utf-8'}, body)
        self.assertEqual(status, 200)
        self.assertEqual(set(login), {'access_token', 'refresh_token', 'expires_in', 'token_type',
            'client_id', 'game_id', 'domain_id', 'persona_id', 'account_id', 'display_name', 'conflictingPersonaID'})
        token = login['access_token']
        self.assertLessEqual(len(token), 512)
        head, payload, signature = token.split('.')
        decode = lambda s: base64.urlsafe_b64decode(s + '=' * (-len(s) % 4))
        self.assertEqual(json.loads(decode(head))['alg'], 'HS256')
        claims = json.loads(decode(payload))
        self.assertEqual(claims['wotc-rols'], ['ArenaLocal'])
        self.assertEqual(claims['wotc-flgs'], 0)
        self.assertEqual(claims['sub'], login['persona_id'])
        expected = hmac.new(self.bootstrap._key, (head + '.' + payload).encode(), hashlib.sha256).digest()
        self.assertEqual(decode(signature), expected)
        self.assertEqual(self.store.authenticate(token)['id'], self.user['id'])
        status, profile = self.bootstrap.dispatch('GET', '/profile', {'Authorization': 'Bearer ' + token})
        self.assertEqual(status, 200)
        self.assertEqual(profile['personaID'], login['persona_id'])
        self.assertEqual(profile['presenceSettings'], {'socialMode': 'PRIVATE'})
        self.assertEqual(set(profile), {'accountID', 'personaID', 'displayName', 'gameID',
                                      'externalID', 'email', 'dataOptIn', 'presenceSettings', 'countryCode'})

    def test_refresh_rotation_and_persistent_issued_credentials(self):
        login = self.login()
        reloaded = Bootstrap(Store(self.root / 'local.sqlite3'), manifests=self.manifests)
        self.assertEqual(reloaded.identity(self.user), self.bootstrap.identity(self.user))
        self.assertEqual(reloaded.profile(login['access_token'])['displayName'], 'Local Player')
        with self.assertRaises(StorageError):
            self.store.authenticate(login['refresh_token'])
        form = {'grant_type': 'refresh_token', 'refresh_token': login['refresh_token']}
        renewed = reloaded.oauth(form, self.basic)
        self.assertNotEqual(renewed['access_token'], login['access_token'])
        self.assertEqual(renewed['persona_id'], login['persona_id'])
        with self.assertRaises(StorageError):
            self.store.authenticate(login['access_token'])
        with self.assertRaises(BootstrapError):
            reloaded.oauth(form, self.basic)

    def test_refresh_is_atomic_under_concurrent_reuse(self):
        login = self.login()
        def refresh(_):
            try:
                self.bootstrap.oauth({'grant_type': 'refresh_token', 'refresh_token': login['refresh_token']}, self.basic)
                return 200
            except BootstrapError as exc:
                return exc.status
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            self.assertEqual(sorted(pool.map(refresh, range(2))), [200, 401])

    def test_unknown_forged_expired_and_logged_out_tokens_rejected(self):
        login = self.login()
        token = login['access_token']
        forged = token.rsplit('.', 1)[0] + '.not-issued'
        for candidate in (forged, 'official-looking.jwt.token', ''):
            status, error = self.bootstrap.dispatch('GET', '/profile', {'Authorization': 'Bearer ' + candidate})
            self.assertEqual(status, 401)
            self.assertEqual(error['error'], 'INVALID ACCESS TOKEN')
        with self.store._db(write=True) as db:
            db.execute('UPDATE sessions SET expires_at=0')
        with self.assertRaises(BootstrapError):
            self.bootstrap.profile(token)

    def test_logout_revokes_access_and_refresh_together(self):
        login = self.login()
        self.bootstrap.revoke(login['access_token'])
        with self.assertRaises(BootstrapError):
            self.bootstrap.profile(login['access_token'])
        with self.assertRaises(BootstrapError):
            self.bootstrap.oauth({'grant_type': 'refresh_token', 'refresh_token': login['refresh_token']}, self.basic)
        token = self.login()['access_token']
        self.store.logout(token)
        with self.assertRaises(BootstrapError):
            self.bootstrap.profile(token)

    def test_authentication_and_request_errors_use_was_envelope(self):
        for body, auth, content_type, expected in (
            (b'grant_type=password&username=Local+Player&password=wrong', self.basic, 'application/x-www-form-urlencoded', 401),
            (b'grant_type=password', '', 'application/x-www-form-urlencoded', 401),
            (b'grant_type=password', 'Basic !!!', 'application/x-www-form-urlencoded', 401),
            (b'grant_type=password&grant_type=password', self.basic, 'application/x-www-form-urlencoded', 400),
            (b'{}', self.basic, 'application/json', 415),
            (b'x' * 20000, self.basic, 'application/x-www-form-urlencoded', 413)):
            status, error = self.bootstrap.dispatch('POST', '/auth/oauth/token',
                {'Authorization': auth, 'Content-Type': content_type}, body)
            self.assertEqual(status, expected)
            self.assertEqual(error['code'], expected)
            self.assertIsInstance(error['error'], str)
        self.assertIsNone(self.bootstrap.dispatch('GET', '/unrelated', {}))

    def test_actual_manifest_filename_recovery_and_ring_contracts(self):
        for filename in ('Manifest_' + '1' * 32 + '.mtga', 'Manifest_Audio_' + '2' * 32 + '.mtga', 'Manifest_bad.mtga'):
            (self.root / filename).write_text('fixture')
        recovered = installed_manifests(self.root)
        self.assertEqual(recovered, self.manifests)
        old = {'playerId': 'local', 'clientVersion': '0.1.14403', 'environmentKey': 'Local', 'platformKey': 'Windows'}
        reply = self.bootstrap.ring(old)
        self.assertEqual(reply['fdURI'], 'tcp://127.0.0.1:9443')
        self.assertEqual(reply['contentHash'], '1' * 32)
        self.assertEqual(reply['preloadContentHashes'], [])
        new = {'code': 'local', 'environment': 'Local', 'clientVersion': '0.1.14403', 'platform': 'Windows', 'playerId': 'local'}
        status, reply = self.bootstrap.dispatch('POST', '/api/v2/ring', {'Content-Type': 'application/json'}, json.dumps(new).encode())
        self.assertEqual(status, 200)
        self.assertEqual(reply['BundleManifests'], self.manifests)
        with self.assertRaises(BootstrapError):
            Bootstrap(self.store).ring(old)
        with self.assertRaises(ValueError):
            Bootstrap(self.store, fd_uri='tcp://external.example:443')


if __name__ == '__main__':
    unittest.main()
