import copy
import json
import tempfile
import unittest
import uuid
from pathlib import Path

from server import commander
from server.arena import Arena
from server.http import resolve_deck, match_decks
from server.samples import add_commander_sample, commander_sample
from server.storage import Store, StorageError


class CommanderTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cards = json.loads((commander.ROOT / 'data/catalogue.json').read_text(encoding='utf8'))
        cls.catalogue = {c['grpId']: c for c in cards}
        cls.by_name = {}
        for card in cards:
            if not card['arenaMetadata'].get('IsRebalanced'):
                cls.by_name.setdefault(card['name'], card)

    def entry(self, name, quantity=1):
        return {'cardId': self.by_name[name]['grpId'], 'quantity': quantity}

    def deck(self):
        return {'MainDeck': [self.entry('Forest', 99)],
                'CommandZone': [self.entry('Bristly Bill, Spine Sower')], 'Sideboard': [], 'Companions': []}

    def test_current_bans_and_all_printings(self):
        policy = commander.bans()
        self.assertEqual(len(policy['categoryBans']['Conspiracy']), 25)
        self.assertEqual(len(policy['categoryBans']['Ante']), 9)
        for name in policy['bannedNames']:
            with self.subTest(name=name), self.assertRaisesRegex(StorageError, 'banned'):
                commander.validate({'MainDeck': [{'cardId': 1, 'quantity': 1}]}, {1: {'name': name}})
        for card in self.catalogue.values():
            if card['name'] in policy['bannedNames']:
                with self.subTest(printing=card['grpId']), self.assertRaisesRegex(StorageError, 'banned'):
                    commander.validate({'MainDeck': [{'cardId': card['grpId'], 'quantity': 1}]}, self.catalogue)
        for name in ('Biorhythm', 'Lutri, the Spellchaser', 'Gifts Ungiven', 'Sway of the Stars',
                     'Braids, Cabal Minion', 'Coalition Victory', 'Panoptic Mirror'):
            self.assertNotIn(name, policy['bannedNames'])

    def test_lutri_is_allowed_in_deck_and_command_but_not_companion(self):
        for pile in ('MainDeck', 'CommandZone'):
            commander.validate({pile: [self.entry('Lutri, the Spellchaser')]}, self.catalogue)
        with self.assertRaisesRegex(StorageError, 'companion'):
            commander.validate({'Companions': [self.entry('Lutri, the Spellchaser')]}, self.catalogue)

    def test_size_identity_singleton_and_drafts(self):
        commander.validate(self.deck(), self.catalogue, complete=True)
        commander.validate({'MainDeck': [], 'CommandZone': []}, self.catalogue)
        bad = self.deck()
        bad['MainDeck'][0]['quantity'] = 98
        with self.assertRaisesRegex(StorageError, '100'):
            commander.validate(bad, self.catalogue, complete=True)
        bad['MainDeck'].append(self.entry('Mountain'))
        with self.assertRaisesRegex(StorageError, 'color identity'):
            commander.validate(bad, self.catalogue, complete=True)
        with self.assertRaisesRegex(StorageError, 'copy limit'):
            commander.validate({'MainDeck': [self.entry('Shock', 2)]}, self.catalogue)
        with self.assertRaisesRegex(StorageError, 'cannot be a Commander'):
            commander.validate({'CommandZone': [self.entry('Forest')]}, self.catalogue)
        with self.assertRaisesRegex(StorageError, 'no sideboard'):
            commander.validate({'Sideboard': [self.entry('Forest')]}, self.catalogue)

    def test_native_basic_lands_and_total_deck_size(self):
        fixture = json.loads((commander.ROOT / 'analysis/deckbuilder-formats.json').read_text(encoding='utf8'))
        for fmt in fixture['GetFormatsResponse']['formats']:
            if fmt['name'] not in commander.FORMATS:
                continue
            for bound in ('min', 'max'):
                self.assertEqual(fmt['mainDeckQuota'][bound] + fmt['commandZoneQuota'][bound], 100)
            for name in ('Forest', 'Island', 'Mountain', 'Plains', 'Swamp', 'Wastes'):
                self.assertNotIn(self.by_name[name]['arenaMetadata']['TitleId'], fmt['bannedTitleIds'])
        digital_forest = next(c for c in self.catalogue.values() if c['name'] == 'Forest'
                              and c['arenaMetadata'].get('IsDigitalOnly'))
        with self.assertRaisesRegex(StorageError, 'paper Commander'):
            commander.validate({'MainDeck': [{'cardId': digital_forest['grpId'], 'quantity': 1}]}, self.catalogue)

    def test_sample_deck_and_both_queues_preserve_commander(self):
        sample, body = commander_sample(self.catalogue)
        commander.validate(body, self.catalogue, complete=True)
        self.assertEqual(sum(c['quantity'] for c in body['MainDeck']), 99)
        with tempfile.TemporaryDirectory() as tmp:
            store = Store(Path(tmp) / 'test.sqlite')
            user = store.register('sample-test', 'local-test-password')
            store.import_cards(list(self.catalogue.values()))
            arena = Arena(store, list(self.catalogue.values()))
            summary = add_commander_sample(arena, user['id'])
            self.assertEqual(add_commander_sample(arena, user['id']), summary)
            self.assertEqual(len(store.list_decks(user['id'])), 1)
            deck = resolve_deck(store, self.catalogue, user['id'], summary['DeckId'])
            for mode, count in (('commander', 2), ('commander4', 4)):
                decks = match_decks(deck, deck, mode)
                self.assertEqual(len(decks), count)
                self.assertTrue(all(d['commanders'] == deck['commanders'] for d in decks))
                self.assertTrue(all(d['cards'] == deck['cards'] for d in decks))
            with self.assertRaisesRegex(StorageError, 'Commander deck'):
                match_decks(dict(deck, format='Historic'), deck, 'commander4')
            with self.assertRaisesRegex(StorageError, 'game mode'):
                match_decks(deck, deck, 'unknown')

    def test_native_save_and_resolution_keep_selected_format_and_commander(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = Store(Path(tmp) / 'test.sqlite')
            user = store.register('commander-fixture', 'local-test-password')
            token = store.login('commander-fixture', 'local-test-password')['token']
            store.import_cards(list(self.catalogue.values()))
            arena = Arena(store, list(self.catalogue.values()))
            for format_name in commander.FORMATS:
                gid = str(uuid.uuid4())
                payload = {'Summary': {'DeckId': gid, 'Name': format_name,
                                      'Attributes': [{'name': 'Format', 'value': format_name}]},
                           'Deck': dict(self.deck(), CardSkins=[]), 'ActionType': 0}
                arena.execute(token, 'DeckUpsertDeckV3', payload)
                resolved = resolve_deck(store, self.catalogue, user['id'], gid)
                self.assertEqual(resolved['format'], format_name)
                self.assertEqual(resolved['commanders'][0]['name'], 'Bristly Bill, Spine Sower')
                self.assertEqual(resolved['cards'][0]['quantity'], 99)
                invalid = copy.deepcopy(payload)
                invalid['Deck']['MainDeck'] = [self.entry('Mana Crypt')]
                with self.assertRaisesRegex(StorageError, 'banned'):
                    arena.execute(token, 'DeckUpsertDeckV3', invalid)
                self.assertEqual(resolve_deck(store, self.catalogue, user['id'], gid), resolved)


if __name__ == '__main__':
    unittest.main()
