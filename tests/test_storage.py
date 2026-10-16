import concurrent.futures
import json
import sqlite3
import tempfile
import unittest
from pathlib import Path

from server.storage import StorageError, Store


class StorageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.path = Path(self.temp.name) / 'store.sqlite3'
        self.store = Store(self.path)
        self.store.import_cards([
            {'grpId': 1, 'name': 'Basic Island', 'cardLimit': 0},
            {'grpId': 2, 'name': 'Test Spell', 'cardLimit': 4},
        ])
        self.a = self.store.register('Alice', 'password-1')['id']
        self.b = self.store.register('Bob', 'password-2')['id']
        self.deck_a = self.store.save_deck(self.a, 'A', [{'grpId': 1, 'quantity': 60}])
        self.deck_b = self.store.save_deck(self.b, 'B', [{'grpId': 1, 'quantity': 60}])

    def tearDown(self):
        self.temp.cleanup()

    def assert_error(self, status, function, *args):
        with self.assertRaises(StorageError) as caught:
            function(*args)
        self.assertEqual(caught.exception.status, status)

    def test_sessions_hashed_expire_and_logout(self):
        login = self.store.login('ALICE', 'password-1')
        self.assertEqual(self.store.authenticate(login['token'])['id'], self.a)
        with sqlite3.connect(self.path) as db:
            token_hash = db.execute('SELECT token_hash FROM sessions').fetchone()[0]
            self.assertNotEqual(token_hash, login['token'])
            password_hash = db.execute('SELECT password_hash FROM users WHERE id=?', (self.a,)).fetchone()[0]
            self.assertNotEqual(password_hash, b'password-1')
            db.execute('UPDATE sessions SET expires_at=0')
        db.close()
        self.assert_error(401, self.store.authenticate, login['token'])
        login = self.store.login('Alice', 'password-1')
        self.store.logout(login['token'])
        self.assert_error(401, self.store.authenticate, login['token'])
        self.assert_error(401, self.store.login, 'Alice', 'wrong-password')
        self.assert_error(409, self.store.register, 'alice', 'another-password')

    def test_deck_ownership_and_copy_aggregation(self):
        self.assert_error(404, self.store.get_deck, self.b, self.deck_a['id'])
        self.assert_error(404, self.store.save_deck, self.b, 'stolen', [{'grpId': 1, 'quantity': 60}], self.deck_a['id'])
        self.assert_error(404, self.store.delete_deck, self.b, self.deck_a['id'])
        self.assert_error(400, self.store.save_deck, self.a, 'bad', [{'grpId': 999, 'quantity': 1}])
        self.assert_error(400, self.store.save_deck, self.a, 'bad', [{'grpId': 2, 'quantity': 3}, {'grpId': 2, 'quantity': 2}])
        self.assert_error(400, self.store.save_deck, self.a, 'bad', [{'grpId': True, 'quantity': 1}])
        deck = self.store.save_deck(self.a, 'ok', [{'grpId': 2, 'quantity': 2}, {'grpId': 2, 'quantity': 2}])
        self.assertEqual(deck['cards'], [{'grpId': 2, 'quantity': 4}])
        self.assert_error(400, self.store.queue, self.a, deck['id'])

    def test_catalogue_import_is_atomic(self):
        self.assert_error(400, self.store.import_cards, [{'grpId': 3, 'name': 'Valid'}, {'grpId': 4, 'name': ''}])
        self.assertEqual(len(self.store.list_cards()), 2)
        self.assertEqual(self.store.list_cards('island')[0]['grpId'], 1)

    def test_catalogue_replacement_preserves_accounts_and_decks(self):
        self.store.import_cards([{'grpId': 1, 'name': 'Updated Island', 'cardLimit': 0}], replace=True)
        self.assertEqual(self.store.list_cards(), [{'grpId': 1, 'name': 'Updated Island', 'cardLimit': 0}])
        self.assertEqual(self.store.get_deck(self.a, self.deck_a['id'])['cards'], [{'grpId': 1, 'quantity': 60}])
        self.assertEqual(self.store.login('Alice', 'password-1')['user']['id'], self.a)

    def test_copy_limits_across_alternate_printings(self):
        self.store.import_cards([{'grpId': 3, 'name': 'Test Spell', 'cardLimit': 4}])
        self.assert_error(400, self.store.save_deck, self.a, 'bad printings',
                          [{'grpId': 2, 'quantity': 3}, {'grpId': 3, 'quantity': 2}])
        good = self.store.save_deck(self.a, 'valid printings',
                                   [{'grpId': 2, 'quantity': 2}, {'grpId': 3, 'quantity': 2}])
        self.assertEqual(sum(c['quantity'] for c in good['cards']), 4)

    def test_string_ids_and_invalid_id_types(self):
        self.assert_error(400, self.store.challenge, self.a, str(self.a), self.deck_a['id'])
        challenge = self.store.challenge(self.a, str(self.b), str(self.deck_a['id']))
        match = self.store.accept_challenge(self.b, str(challenge['id']), str(self.deck_b['id']))
        self.assertEqual(self.store.get_match(self.a, str(match['id']))['id'], match['id'])
        for bad in (None, {}, [], True, '1.0', '9999999999999999999999999'):
            self.assert_error(400, self.store.challenge, self.a, bad, self.deck_a['id'])

    def test_queue_discards_invalid_decks_after_catalogue_edit(self):
        self.store.queue(self.a, self.deck_a['id'])
        self.store.import_cards([{'grpId': 1, 'name': 'Basic Island', 'cardLimit': 4}])
        self.store.import_cards([{'grpId': 4, 'name': 'Basic Mountain', 'cardLimit': 0}])
        deck_b = self.store.save_deck(self.b, 'legal', [{'grpId': 4, 'quantity': 60}])
        self.assertEqual(self.store.queue(self.b, deck_b['id'])['status'], 'queued')
        self.assertEqual(self.store.list_matches(self.b), [])

    def test_queue_atomic_pairing_and_durable_snapshots(self):
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            results = list(pool.map(lambda p: self.store.queue(*p), [(self.a, self.deck_a['id']), (self.b, self.deck_b['id'])]))
        self.assertEqual(sorted(r['status'] for r in results), ['matched', 'queued'])
        match = self.store.list_matches(self.a)[0]
        self.assertEqual(match['status'], 'pending_engine')
        self.assertEqual(len(self.store.list_matches(self.b)), 1)
        outsider = self.store.register('Eve', 'password-3')['id']
        self.assert_error(404, self.store.get_match, outsider, match['id'])
        self.store.delete_deck(self.a, self.deck_a['id'])
        reloaded = Store(self.path).get_match(self.b, match['id'])
        self.assertEqual(reloaded, match)
        self.assertEqual(reloaded['decks'][0]['cards'], [{'grpId': 1, 'quantity': 60}])

    def test_challenge_acceptance_is_authorized_and_once(self):
        challenge = self.store.challenge(self.a, self.b, self.deck_a['id'])
        self.assert_error(404, self.store.accept_challenge, self.a, challenge['id'], self.deck_a['id'])
        self.assert_error(409, self.store.challenge, self.a, self.b, self.deck_a['id'])
        match = self.store.accept_challenge(self.b, challenge['id'], self.deck_b['id'])
        self.assertEqual(match['players'], [self.a, self.b])
        self.assert_error(404, self.store.accept_challenge, self.b, challenge['id'], self.deck_b['id'])
        self.assertEqual(self.store.list_challenges(self.a), [])

    def test_challenge_concurrent_acceptance(self):
        challenge = self.store.challenge(self.a, self.b, self.deck_a['id'])
        def accept(_):
            try:
                return self.store.accept_challenge(self.b, challenge['id'], self.deck_b['id'])['id']
            except StorageError as exc:
                return exc.status
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            results = list(pool.map(accept, range(2)))
        self.assertIn(404, results)
        self.assertEqual(len(self.store.list_matches(self.a)), 1)

    def test_delete_cascades_lobby_references(self):
        self.store.queue(self.a, self.deck_a['id'])
        self.store.challenge(self.a, self.b, self.deck_a['id'])
        self.store.delete_deck(self.a, self.deck_a['id'])
        self.assertEqual(self.store.list_challenges(self.b), [])
        self.assertEqual(self.store.queue(self.b, self.deck_b['id'])['status'], 'queued')


if __name__ == '__main__':
    unittest.main()
