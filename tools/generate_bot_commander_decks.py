"""Generate and validate 5 curated 100-card Commander decks for the AI bot."""
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from server import commander

catalogue = json.load(open(ROOT / "data/catalogue.json", encoding="utf-8"))
cat_dict = {c["grpId"]: c for c in catalogue}
bans = set(json.load(open(ROOT / "data/commander-bans.json", encoding="utf-8"))["bannedNames"])

paper = {}
for c in catalogue:
    meta = c.get("arenaMetadata", {})
    if meta.get("IsPrimaryCard") == 1 and not any(meta.get(k) for k in ("IsToken", "IsDigitalOnly", "IsRebalanced")):
        name = c["name"]
        if name not in bans:
            paper.setdefault(name, c)

OUT_DIR = ROOT / "data/bot-commander-decks"
OUT_DIR.mkdir(parents=True, exist_ok=True)

# 1. Bristly Bill (from data/sample-commander.json)
bill_data = json.loads((ROOT / "data/sample-commander.json").read_text(encoding="utf-8"))
(OUT_DIR / "bristly-bill.json").write_text(json.dumps(bill_data, indent=2), encoding="utf-8")

# 2. Krenko, Mob Boss (Red Goblins)
krenko_cards = [
    {"name": "Mountain", "quantity": 36},
    {"name": "Skirk Prospector", "quantity": 1},
    {"name": "Goblin Bushwhacker", "quantity": 1},
    {"name": "Foundry Street Denizen", "quantity": 1},
    {"name": "Goblin Instigator", "quantity": 1},
    {"name": "Muxus, Goblin Grandee", "quantity": 1},
    {"name": "Siege-Gang Commander", "quantity": 1},
    {"name": "Goblin Chieftain", "quantity": 1},
    {"name": "Goblin Warchief", "quantity": 1},
    {"name": "Goblin Ringleader", "quantity": 1},
    {"name": "Goblin Matron", "quantity": 1},
    {"name": "Goblin Trashmaster", "quantity": 1},
    {"name": "Pashalik Mons", "quantity": 1},
    {"name": "Legion Warboss", "quantity": 1},
    {"name": "Krenko, Tin Street Kingpin", "quantity": 1},
    {"name": "Goblin Chainwhirler", "quantity": 1},
    {"name": "Conspicuous Snoop", "quantity": 1},
    {"name": "Rundvelt Hordemaster", "quantity": 1},
    {"name": "Battle Cry Goblin", "quantity": 1},
    {"name": "Goblin Piledriver", "quantity": 1},
    {"name": "Fanatical Firebrand", "quantity": 1},
    {"name": "Goblin Cratermaker", "quantity": 1},
    {"name": "Tin Street Dodger", "quantity": 1},
    {"name": "Goro-Goro, Disciple of Ryusei", "quantity": 1},
    {"name": "Squee, Dubious Monarch", "quantity": 1},
    {"name": "Squee, the Immortal", "quantity": 1},
    {"name": "Moria Marauder", "quantity": 1},
    {"name": "Guttersnipe", "quantity": 1},
    {"name": "Torbran, Thane of Red Fell", "quantity": 1},
    {"name": "Purphoros, God of the Forge", "quantity": 1},
    {"name": "Ogre Battledriver", "quantity": 1},
    {"name": "Beetleback Chief", "quantity": 1},
    {"name": "Dark-Dweller Oracle", "quantity": 1},
    {"name": "Gempalm Incinerator", "quantity": 1},
    {"name": "Goblin Rabblemaster", "quantity": 1},
    {"name": "Krenko, Baron of Tin Street", "quantity": 1},
    {"name": "Reckless Bushwhacker", "quantity": 1},
    {"name": "Hobgoblin Bandit Lord", "quantity": 1},
    {"name": "Professional Face-Breaker", "quantity": 1},
    {"name": "Atsushi, the Blazing Sky", "quantity": 1},
    {"name": "Bonecrusher Giant", "quantity": 1},
    {"name": "Arcane Signet", "quantity": 1},
    {"name": "Mind Stone", "quantity": 1},
    {"name": "Heraldic Banner", "quantity": 1},
    {"name": "Swiftfoot Boots", "quantity": 1},
    {"name": "Lightning Greaves", "quantity": 1},
    {"name": "Vanquisher's Banner", "quantity": 1},
    {"name": "Icon of Ancestry", "quantity": 1},
    {"name": "Dragon Fodder", "quantity": 1},
    {"name": "Krenko's Command", "quantity": 1},
    {"name": "Hordeling Outburst", "quantity": 1},
    {"name": "You See a Pair of Goblins", "quantity": 1},
    {"name": "Shock", "quantity": 1},
    {"name": "Lightning Strike", "quantity": 1},
    {"name": "Chaos Warp", "quantity": 1},
    {"name": "Abrade", "quantity": 1},
    {"name": "Blasphemous Act", "quantity": 1},
    {"name": "Thrill of Possibility", "quantity": 1},
    {"name": "Faithless Looting", "quantity": 1},
    {"name": "Big Score", "quantity": 1},
    {"name": "Unexpected Windfall", "quantity": 1},
    {"name": "Light Up the Stage", "quantity": 1},
    {"name": "Impact Tremors", "quantity": 1},
    {"name": "Shared Animosity", "quantity": 1}
]

krenko_data = {
    "key": "bot-commander-krenko-v1",
    "name": "Krenko's Goblin Mob",
    "format": "Commander",
    "commander": "Krenko, Mob Boss",
    "cards": krenko_cards
}
(OUT_DIR / "krenko-goblins.json").write_text(json.dumps(krenko_data, indent=2), encoding="utf-8")

# 3. Ayara, First of Locthwain (Black Aristocrats)
ayara_cards = [
    {"name": "Swamp", "quantity": 36},
    {"name": "Blood Artist", "quantity": 1},
    {"name": "Zulaport Cutthroat", "quantity": 1},
    {"name": "Morbid Opportunist", "quantity": 1},
    {"name": "Midnight Reaper", "quantity": 1},
    {"name": "Grim Haruspex", "quantity": 1},
    {"name": "Gray Merchant of Asphodel", "quantity": 1},
    {"name": "Gutterbones", "quantity": 1},
    {"name": "Jadar, Ghoulcaller of Nephalia", "quantity": 1},
    {"name": "Cryptbreaker", "quantity": 1},
    {"name": "Lord of the Accursed", "quantity": 1},
    {"name": "Death Baron", "quantity": 1},
    {"name": "Undead Augur", "quantity": 1},
    {"name": "Murderous Rider", "quantity": 1},
    {"name": "Noxious Gearhulk", "quantity": 1},
    {"name": "Ravenous Chupacabra", "quantity": 1},
    {"name": "Plaguecrafter", "quantity": 1},
    {"name": "Demon's Disciple", "quantity": 1},
    {"name": "Fleshbag Marauder", "quantity": 1},
    {"name": "Gix, Yawgmoth Praetor", "quantity": 1},
    {"name": "Vito, Thorn of the Dusk Rose", "quantity": 1},
    {"name": "Woe Strider", "quantity": 1},
    {"name": "Yahenni, Undying Partisan", "quantity": 1},
    {"name": "Tenacious Underdog", "quantity": 1},
    {"name": "Persistent Specimen", "quantity": 1},
    {"name": "Silversmote Ghoul", "quantity": 1},
    {"name": "Dread Presence", "quantity": 1},
    {"name": "Spawn of Mayhem", "quantity": 1},
    {"name": "Gonti, Lord of Luxury", "quantity": 1},
    {"name": "Rankle, Master of Pranks", "quantity": 1},
    {"name": "Massacre Wurm", "quantity": 1},
    {"name": "Sheoldred, Whispering One", "quantity": 1},
    {"name": "Braids, Arisen Nightmare", "quantity": 1},
    {"name": "Dreadhorde Invasion", "quantity": 1},
    {"name": "Bastion of Remembrance", "quantity": 1},
    {"name": "Phyrexian Arena", "quantity": 1},
    {"name": "Black Market Connections", "quantity": 1},
    {"name": "Bolas's Citadel", "quantity": 1},
    {"name": "Whip of Erebos", "quantity": 1},
    {"name": "Village Rites", "quantity": 1},
    {"name": "Corrupted Conviction", "quantity": 1},
    {"name": "Deadly Dispute", "quantity": 1},
    {"name": "Altar's Reap", "quantity": 1},
    {"name": "Costly Plunder", "quantity": 1},
    {"name": "Sign in Blood", "quantity": 1},
    {"name": "Night's Whisper", "quantity": 1},
    {"name": "Read the Bones", "quantity": 1},
    {"name": "Feed the Swarm", "quantity": 1},
    {"name": "Infernal Grasp", "quantity": 1},
    {"name": "Heartless Act", "quantity": 1},
    {"name": "Cast Down", "quantity": 1},
    {"name": "Doom Blade", "quantity": 1},
    {"name": "Hero's Downfall", "quantity": 1},
    {"name": "Go for the Throat", "quantity": 1},
    {"name": "Tragic Slip", "quantity": 1},
    {"name": "Bitter Triumph", "quantity": 1},
    {"name": "Damnation", "quantity": 1},
    {"name": "Toxic Deluge", "quantity": 1},
    {"name": "Crux of Fate", "quantity": 1},
    {"name": "Blood on the Snow", "quantity": 1},
    {"name": "Reanimate", "quantity": 1},
    {"name": "Victimize", "quantity": 1},
    {"name": "Bontu's Monument", "quantity": 1},
    {"name": "Arcane Signet", "quantity": 1}
]

ayara_data = {
    "key": "bot-commander-ayara-v1",
    "name": "Ayara's Undead Feast",
    "format": "Commander",
    "commander": "Ayara, First of Locthwain",
    "cards": ayara_cards
}
(OUT_DIR / "ayara-aristocrats.json").write_text(json.dumps(ayara_data, indent=2), encoding="utf-8")

# 4. Talrand, Sky Summoner (Blue Spellslinger)
talrand_cards = [
    {"name": "Island", "quantity": 36},
    {"name": "Murmuring Mystic", "quantity": 1},
    {"name": "Archmage Emeritus", "quantity": 1},
    {"name": "Baral, Chief of Compliance", "quantity": 1},
    {"name": "Haughty Djinn", "quantity": 1},
    {"name": "Wavebreak Hippocamp", "quantity": 1},
    {"name": "Hullbreaker Horror", "quantity": 1},
    {"name": "Nezahal, Primal Tide", "quantity": 1},
    {"name": "Docent of Perfection", "quantity": 1},
    {"name": "Lier, Disciple of the Drowned", "quantity": 1},
    {"name": "Faerie Mastermind", "quantity": 1},
    {"name": "Ledger Shredder", "quantity": 1},
    {"name": "Opt", "quantity": 1},
    {"name": "Consider", "quantity": 1},
    {"name": "Brainstorm", "quantity": 1},
    {"name": "Ponder", "quantity": 1},
    {"name": "Preordain", "quantity": 1},
    {"name": "Sleight of Hand", "quantity": 1},
    {"name": "Curate", "quantity": 1},
    {"name": "Think Twice", "quantity": 1},
    {"name": "Thirst for Discovery", "quantity": 1},
    {"name": "Thirst for Knowledge", "quantity": 1},
    {"name": "Chemister's Insight", "quantity": 1},
    {"name": "Chart a Course", "quantity": 1},
    {"name": "Winged Words", "quantity": 1},
    {"name": "Hieroglyphic Illumination", "quantity": 1},
    {"name": "Frantic Search", "quantity": 1},
    {"name": "Behold the Multiverse", "quantity": 1},
    {"name": "Memory Deluge", "quantity": 1},
    {"name": "Dig Through Time", "quantity": 1},
    {"name": "Treasure Cruise", "quantity": 1},
    {"name": "Counterspell", "quantity": 1},
    {"name": "Mana Leak", "quantity": 1},
    {"name": "Negate", "quantity": 1},
    {"name": "Essence Scatter", "quantity": 1},
    {"name": "Disdainful Stroke", "quantity": 1},
    {"name": "Saw It Coming", "quantity": 1},
    {"name": "Sinister Sabotage", "quantity": 1},
    {"name": "Supreme Will", "quantity": 1},
    {"name": "Archmage's Charm", "quantity": 1},
    {"name": "Cryptic Command", "quantity": 1},
    {"name": "Mystic Confluence", "quantity": 1},
    {"name": "Sublime Epiphany", "quantity": 1},
    {"name": "Wash Away", "quantity": 1},
    {"name": "Make Disappear", "quantity": 1},
    {"name": "Cancel", "quantity": 1},
    {"name": "Spell Pierce", "quantity": 1},
    {"name": "Unsummon", "quantity": 1},
    {"name": "Into the Roil", "quantity": 1},
    {"name": "Blink of an Eye", "quantity": 1},
    {"name": "Fading Hope", "quantity": 1},
    {"name": "Slip Out the Back", "quantity": 1},
    {"name": "Run Away Together", "quantity": 1},
    {"name": "Resculpt", "quantity": 1},
    {"name": "Pongify", "quantity": 1},
    {"name": "Rapid Hybridization", "quantity": 1},
    {"name": "Aetherize", "quantity": 1},
    {"name": "River's Rebuke", "quantity": 1},
    {"name": "Whelming Wave", "quantity": 1},
    {"name": "Shark Typhoon", "quantity": 1},
    {"name": "Midnight Clock", "quantity": 1},
    {"name": "Arcane Signet", "quantity": 1},
    {"name": "Mind Stone", "quantity": 1},
    {"name": "Swiftfoot Boots", "quantity": 1}
]

talrand_data = {
    "key": "bot-commander-talrand-v1",
    "name": "Talrand's Drake Flock",
    "format": "Commander",
    "commander": "Talrand, Sky Summoner",
    "cards": talrand_cards
}
(OUT_DIR / "talrand-drakes.json").write_text(json.dumps(talrand_data, indent=2), encoding="utf-8")

# 5. Heliod, Sun-Crowned (White Lifegain)
heliod_cards = [
    {"name": "Plains", "quantity": 36},
    {"name": "Soul Warden", "quantity": 1},
    {"name": "Lunarch Veteran", "quantity": 1},
    {"name": "Impassioned Orator", "quantity": 1},
    {"name": "Kor Celebrant", "quantity": 1},
    {"name": "Leonin Vanguard", "quantity": 1},
    {"name": "Healer's Hawk", "quantity": 1},
    {"name": "Speaker of the Heavens", "quantity": 1},
    {"name": "Daxos, Blessed by the Sun", "quantity": 1},
    {"name": "Voice of the Blessed", "quantity": 1},
    {"name": "Ajani's Pridemate", "quantity": 1},
    {"name": "Luminarch Aspirant", "quantity": 1},
    {"name": "Twinblade Paladin", "quantity": 1},
    {"name": "Angel of Vitality", "quantity": 1},
    {"name": "Resplendent Angel", "quantity": 1},
    {"name": "Righteous Valkyrie", "quantity": 1},
    {"name": "Adeline, Resplendent Cathar", "quantity": 1},
    {"name": "Welcoming Vampire", "quantity": 1},
    {"name": "Tocatli Honor Guard", "quantity": 1},
    {"name": "Serra Ascendant", "quantity": 1},
    {"name": "Ranger-Captain of Eos", "quantity": 1},
    {"name": "Esper Sentinel", "quantity": 1},
    {"name": "Mentor of the Meek", "quantity": 1},
    {"name": "Loran of the Third Path", "quantity": 1},
    {"name": "Skyclave Apparition", "quantity": 1},
    {"name": "Extraction Specialist", "quantity": 1},
    {"name": "Lyra Dawnbringer", "quantity": 1},
    {"name": "Sephara, Sky's Blade", "quantity": 1},
    {"name": "Sun Titan", "quantity": 1},
    {"name": "Baneslayer Angel", "quantity": 1},
    {"name": "Archangel of Thune", "quantity": 1},
    {"name": "Serra Paragon", "quantity": 1},
    {"name": "Alseid of Life's Bounty", "quantity": 1},
    {"name": "Selfless Savior", "quantity": 1},
    {"name": "Swords to Plowshares", "quantity": 1},
    {"name": "Path to Exile", "quantity": 1},
    {"name": "Fateful Absence", "quantity": 1},
    {"name": "Get Lost", "quantity": 1},
    {"name": "Stroke of Midnight", "quantity": 1},
    {"name": "Declaration in Stone", "quantity": 1},
    {"name": "Destroy Evil", "quantity": 1},
    {"name": "Disenchant", "quantity": 1},
    {"name": "Revitalize", "quantity": 1},
    {"name": "Wrath of God", "quantity": 1},
    {"name": "Day of Judgment", "quantity": 1},
    {"name": "Depopulate", "quantity": 1},
    {"name": "Sunfall", "quantity": 1},
    {"name": "Farewell", "quantity": 1},
    {"name": "Cleansing Nova", "quantity": 1},
    {"name": "Vanquish the Horde", "quantity": 1},
    {"name": "Authority of the Consuls", "quantity": 1},
    {"name": "Blind Obedience", "quantity": 1},
    {"name": "Dawn of Hope", "quantity": 1},
    {"name": "Cathars' Crusade", "quantity": 1},
    {"name": "Felidar Retreat", "quantity": 1},
    {"name": "Cleric Class", "quantity": 1},
    {"name": "Arcane Signet", "quantity": 1},
    {"name": "Mind Stone", "quantity": 1},
    {"name": "Swiftfoot Boots", "quantity": 1},
    {"name": "Shadowspear", "quantity": 1},
    {"name": "The Book of Exalted Deeds", "quantity": 1},
    {"name": "The Wandering Emperor", "quantity": 1},
    {"name": "Ajani, Strength of the Pride", "quantity": 1},
    {"name": "Cosmic Rebirth", "quantity": 1}
]

# Note: Cosmic Rebirth has Green in color identity! Check if mono white:
# Wait, Cosmic Rebirth is GW! Let's replace Cosmic Rebirth with another mono-white card:
# e.g. "Elspeth Resplendent" or "Unbreakable Formation"
heliod_cards = [c for c in heliod_cards if c["name"] != "Cosmic Rebirth"]
heliod_cards.append({"name": "Unbreakable Formation", "quantity": 1})

heliod_data = {
    "key": "bot-commander-heliod-v1",
    "name": "Heliod's Sunlit Host",
    "format": "Commander",
    "commander": "Heliod, Sun-Crowned",
    "cards": heliod_cards
}
(OUT_DIR / "heliod-lifegain.json").write_text(json.dumps(heliod_data, indent=2), encoding="utf-8")

# Validate all 5 decks
all_decks = [bill_data, krenko_data, ayara_data, talrand_data, heliod_data]
for d in all_decks:
    print(f"Validating {d['name']} (Commander: {d['commander']})...")
    body = {
        "MainDeck": [{"cardId": paper[c["name"]]["grpId"], "quantity": c["quantity"]} for c in d["cards"]],
        "CommandZone": [{"cardId": paper[d["commander"]]["grpId"], "quantity": 1}],
        "Sideboard": [],
        "Companions": []
    }
    main_total = sum(c["quantity"] for c in d["cards"])
    assert main_total == 99, f"Maindeck has {main_total} cards, expected 99"
    commander.validate(body, cat_dict, complete=True)
    print(f"  OK: 100 cards verified legal and ready!")

print("\nAll 5 Commander decks created and validated successfully!")
