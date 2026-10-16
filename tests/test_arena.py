import concurrent.futures
from pathlib import Path
import tempfile
import unittest
import uuid

from server.arena import Arena
from server.storage import Store, StorageError


class ArenaTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.path = Path(self.temp.name) / 'arena.sqlite3'
        self.store = Store(self.path)
        catalogue = [{'grpId': 1, 'name': 'Island', 'cardLimit': 0, 'arenaMetadata': {'ExpansionCode': 'MIR'}},
                     {'grpId': 2, 'name': 'Test Spell', 'cardLimit': 4, 'arenaMetadata': {'ExpansionCode': 'MIR'}},
                     {'grpId': 3, 'name': 'Test Spell', 'cardLimit': 4},
                     {'grpId': 4, 'name': 'Token', 'cardLimit': 4, 'arenaMetadata': {'IsToken': 1}},
                     {'grpId': 5, 'name': 'Back Face', 'cardLimit': 4, 'arenaMetadata': {'IsPrimaryCard': 0}}]
        self.store.import_cards(catalogue)
        self.user = self.store.register('Alice', 'password-alice')
        self.token = self.store.login('Alice', 'password-alice')['token']
        self.other = self.store.register('Bob', 'password-bob')
        self.other_token = self.store.login('Bob', 'password-bob')['token']
        self.arena = Arena(self.store, catalogue)

    def tearDown(self):
        self.temp.cleanup()

    def request(self, name, payload=None, token=None):
        return self.arena.execute(token or self.token, name, payload or {})

    def upsert(self, name='My Deck', guid=None, main=None, sideboard=None):
        payload = {'Summary': {'DeckId': guid or str(uuid.uuid4()), 'Name': name,
            'Attributes': [{'name': 'Format', 'value': 'Historic'}], 'PreferredCosmetics': {'Sleeve': 'local-sleeve'}},
            'Deck': {'MainDeck': main if main is not None else [{'cardId': 1, 'quantity': 60}],
                     'Sideboard': sideboard or [], 'CommandZone': [], 'Companions': [], 'CardSkins': []}, 'ActionType': 0}
        return self.request('DeckUpsertDeckV3', payload), payload

    def test_crud_roundtrip_owned_store_and_persistence(self):
        summary, payload = self.upsert(sideboard=[{'cardId': 2, 'quantity': 3}])
        guid = summary['DeckId']
        body = self.request('DeckGetDeck', {'DeckId': guid})
        self.assertEqual(body['Sideboard'], [{'cardId': 2, 'quantity': 3}])
        self.assertEqual(self.store.list_decks(self.user['id'])[0]['cards'], [{'grpId': 1, 'quantity': 60}])
        self.assertEqual(self.request('DeckGetDeckSummariesV3')['Summaries'][0]['DeckId'], guid)
        reloaded = Arena(Store(self.path))
        self.assertEqual(reloaded.execute(self.token, 'DeckGetDeck', {'DeckId': guid}), body)
        payload['Summary']['Name'] = 'Renamed'
        self.assertEqual(self.request('DeckUpsertDeckV3', payload)['Name'], 'Renamed')
        self.assertEqual(self.store.list_decks(self.user['id'])[0]['name'], 'Renamed')
        self.assertEqual(self.request('DeckDeleteDeck', {'DeckId': guid}), 'success')
        self.assertEqual(self.request('DeckGetDeckSummariesV3'), {'Summaries': []})
        self.assertEqual(self.store.list_decks(self.user['id']), [])

    def test_cross_account_guids_read_write_delete_rejected(self):
        summary, payload = self.upsert()
        for command, data in (('DeckGetDeck', {'DeckId': summary['DeckId']}),
                             ('DeckDeleteDeck', {'DeckId': summary['DeckId']}),
                             ('DeckUpsertDeckV3', payload)):
            with self.subTest(command=command), self.assertRaises(StorageError) as caught:
                self.request(command, data, self.other_token)
            self.assertEqual(caught.exception.status, 404)
        self.assertEqual(self.request('DeckGetDeckSummariesV3', token=self.other_token), {'Summaries': []})

    def test_main_sideboard_printings_share_copy_validation(self):
        with self.assertRaises(StorageError):
            self.upsert(main=[{'cardId': 2, 'quantity': 3}], sideboard=[{'cardId': 3, 'quantity': 2}])
        with self.assertRaises(StorageError):
            self.upsert(main=[{'cardId': 999, 'quantity': 1}])
        self.assertEqual(self.store.list_decks(self.user['id']), [])
        self.assertEqual(self.request('DeckGetDeckSummariesV3'), {'Summaries': []})

    def test_empty_builder_draft_can_be_saved_filled_and_emptied(self):
        summary, payload = self.upsert(main=[])
        guid = summary['DeckId']
        self.assertEqual(self.store.list_decks(self.user['id']), [])
        self.assertEqual(self.request('DeckGetDeck', {'DeckId': guid})['MainDeck'], [])
        payload['Deck']['MainDeck'] = [{'cardId': 1, 'quantity': 60}]
        self.request('DeckUpsertDeckV3', payload)
        self.assertEqual(len(self.store.list_decks(self.user['id'])), 1)
        payload['Deck']['MainDeck'] = []
        self.request('DeckUpsertDeckV3', payload)
        self.assertEqual(len(self.request('DeckGetDeckSummariesV3')['Summaries']), 1)
        self.assertEqual(self.store.list_decks(self.user['id']), [])

    def test_local_control_panel_decks_get_stable_guid_and_shared_edits(self):
        deck = self.store.save_deck(self.user['id'], 'Web Deck', [{'grpId': 1, 'quantity': 60}])
        first = self.request('DeckGetDeckSummariesV3')['Summaries'][0]
        self.store.save_deck(self.user['id'], 'Edited Web', [{'grpId': 1, 'quantity': 80}], deck['id'])
        second = self.request('DeckGetDeckSummariesV3')['Summaries'][0]
        self.assertEqual(first['DeckId'], second['DeckId'])
        self.assertEqual(second['Name'], 'Edited Web')
        self.assertEqual(self.request('DeckGetDeck', {'DeckId': second['DeckId']})['MainDeck'][0]['quantity'], 80)
        self.store.delete_deck(self.user['id'], deck['id'])
        self.assertEqual(self.request('DeckGetDeckSummariesV3'), {'Summaries': []})

    def test_sandbox_collection_is_primary_non_token_and_cache_stable(self):
        cards = self.request('CardGetAllCards', {'CacheVersion': 0})
        self.assertEqual(self.request('CardGetAllCards', {'CacheVersion': -1}), cards)
        self.assertEqual(cards['cards'], {'1': 250, '2': 4, '3': 4})
        self.assertEqual(self.request('CardGetAllCards', {'CacheVersion': cards['cacheVersion']}), cards)
        self.assertGreater(cards['cacheVersion'], 0)
        self.assertEqual(self.arena.dispatch({'name': 'CardGetAllCards', 'payload': {}}, self.token)['result'], cards)

    def test_card_set_dictionary_contract_and_native_enum_action(self):
        self.assertEqual(self.request('CardGetCardSet', {'SetCode': 'MIR'}), {'1': 250, '2': 4})
        self.assertEqual(self.request('CardGetCardSet', {'SetCode': 'UNKNOWN'}), {})
        with self.assertRaises(StorageError):
            self.request('CardGetCardSet', {'SetCode': 1})
        summary, payload = self.upsert()
        payload['ActionType'] = 'Imported'
        self.assertEqual(self.request('DeckUpsertDeckV3', payload)['DeckId'], summary['DeckId'])
        payload['ActionType'] = 'unsupported'
        with self.assertRaises(StorageError):
            self.request('DeckUpsertDeckV3', payload)

    def test_invalid_requests_and_concurrent_same_guid_no_duplicates(self):
        with self.assertRaises(StorageError):
            self.arena.execute('unknown-token', 'DeckGetDeckSummariesV3', {})
        with self.assertRaises(StorageError):
            self.request('UnknownCommand')
        with self.assertRaises(StorageError):
            self.request('DeckGetDeck', {'DeckId': 'invalid'})
        summary, payload = self.upsert()
        payload['Deck']['MainDeck'] = {}
        with self.assertRaises(StorageError):
            self.request('DeckUpsertDeckV3', payload)
        self.request('DeckDeleteDeck', {'DeckId': summary['DeckId']})
        guid = str(uuid.uuid4())
        def upsert(_):
            return self.upsert(guid=guid)[0]['DeckId']
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            self.assertEqual(list(pool.map(upsert, range(2))), [guid, guid])
        self.assertEqual(len(self.store.list_decks(self.user['id'])), 1)
        self.assertEqual(len(self.request('DeckGetDeckSummariesV3')['Summaries']), 1)


if __name__ == '__main__':
    unittest.main()
