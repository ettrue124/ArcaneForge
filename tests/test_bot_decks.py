import json
import unittest
from pathlib import Path

from server import commander, bot_decks


class BotDecksTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cards = json.loads((commander.ROOT / 'data/catalogue.json').read_text(encoding='utf8'))
        cls.catalogue = {c['grpId']: c for c in cards}

    def test_all_five_bot_commander_decks_loaded_and_valid(self):
        decks = bot_decks.load_bot_commander_decks(self.catalogue)
        self.assertEqual(len(decks), 5, "Expected 5 curated bot commander decks")
        names = {d["name"] for d in decks}
        cmdrs = {d["commanders"][0]["name"] for d in decks}
        self.assertIn("Krenko, Mob Boss", cmdrs)
        self.assertIn("Ayara, First of Locthwain", cmdrs)
        self.assertIn("Talrand, Sky Summoner", cmdrs)
        self.assertIn("Heliod, Sun-Crowned", cmdrs)
        self.assertIn("Bristly Bill, Spine Sower", cmdrs)

        for d in decks:
            total = sum(c["quantity"] for c in d["cards"]) + len(d["commanders"])
            self.assertEqual(total, 100, f"{d['name']} must have exactly 100 cards")

    def test_random_bot_deck_excludes_player_deck(self):
        picked = bot_decks.get_random_bot_deck(self.catalogue, format_name="Commander", exclude_name="Krenko's Goblin Mob")
        self.assertIsNotNone(picked)
        self.assertNotEqual(picked["name"], "Krenko's Goblin Mob")
        self.assertNotEqual(picked["commanders"][0]["name"], "Krenko, Mob Boss")

    def test_multiplayer_selects_distinct_bot_decks(self):
        multi = bot_decks.get_multiple_random_bot_decks(self.catalogue, count=3, format_name="Commander", exclude_name="Krenko's Goblin Mob")
        self.assertEqual(len(multi), 3)
        cmdr_names = [d["commanders"][0]["name"] for d in multi]
        self.assertEqual(len(set(cmdr_names)), 3, "Multiplayer seats must have distinct commanders")
        self.assertNotIn("Krenko, Mob Boss", cmdr_names)

    def test_import_deck_text(self):
        text = """
Commander
1 Bristly Bill, Spine Sower

Deck
99 Forest
"""
        imported = bot_decks.import_deck_text(text, self.catalogue, deck_name="Test Bill")
        self.assertEqual(imported["commander"], "Bristly Bill, Spine Sower")
        self.assertEqual(sum(c["quantity"] for c in imported["cards"]), 99)
