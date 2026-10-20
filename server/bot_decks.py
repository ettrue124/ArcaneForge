"""Pre-built and dynamically pulled bot decks for AI opponents.

Provides curated, verified Commander and Constructed decks for Forge AI,
ensuring the bot does not use the player's deck and picks randomly.
Also supports importing online deck text or URLs.
"""
import json
import logging
import random
import re
import urllib.request
from pathlib import Path
from . import commander
from .storage import StorageError

ROOT = Path(__file__).resolve().parents[1]
BOT_COMMANDER_DIR = ROOT / 'data' / 'bot-commander-decks'


def _get_paper_catalogue(catalogue):
    paper = {}
    for card in catalogue.values() if isinstance(catalogue, dict) else catalogue:
        meta = card.get('arenaMetadata', {})
        if meta.get('IsPrimaryCard', True) and not any(meta.get(k) for k in ('IsToken', 'IsDigitalOnly', 'IsRebalanced')):
            paper.setdefault(card['name'], card)
    return paper


def load_bot_commander_decks(catalogue):
    """Load all valid bot commander decks from data/bot-commander-decks/."""
    paper = _get_paper_catalogue(catalogue)
    cat_dict = catalogue if isinstance(catalogue, dict) else {c['grpId']: c for c in catalogue}
    decks = []
    if not BOT_COMMANDER_DIR.exists():
        return decks

    for file_path in sorted(BOT_COMMANDER_DIR.glob('*.json')):
        try:
            data = json.loads(file_path.read_text(encoding='utf-8'))
            cmdr_name = data.get('commander')
            if not cmdr_name or cmdr_name not in paper:
                continue
            cmdr_grp = paper[cmdr_name]['grpId']
            cards = []
            for c in data.get('cards', []):
                name = c.get('name')
                if not name or name not in paper:
                    continue
                cards.append({'name': name, 'quantity': c.get('quantity', 1), 'grpId': paper[name]['grpId']})
            if sum(c['quantity'] for c in cards) != 99:
                continue

            # Validate against commander rules
            body = {
                'MainDeck': [{'cardId': c['grpId'], 'quantity': c['quantity']} for c in cards],
                'CommandZone': [{'cardId': cmdr_grp, 'quantity': 1}],
                'Sideboard': [],
                'Companions': []
            }
            commander.validate(body, cat_dict, complete=True)

            deck_obj = {
                'name': data.get('name', cmdr_name + ' Deck'),
                'format': 'Commander',
                'commanders': [{'name': cmdr_name, 'quantity': 1, 'grpId': cmdr_grp}],
                'companions': [],
                'cards': cards
            }
            decks.append(deck_obj)
        except Exception as e:
            logging.warning("Skipping bot deck %s: %s", file_path.name, e)
    return decks


def load_bot_constructed_decks(catalogue, format_name='NoBans60'):
    """Resolve complete curated decks; never silently drop unavailable cards."""
    paper = _get_paper_catalogue(catalogue)
    decks = []
    for path in sorted((ROOT / 'server/decks').glob('*.json')):
        data = json.loads(path.read_text(encoding='utf8'))
        piles = {}
        for pile in ('cards', 'sideboard'):
            entries = data.get(pile, [])
            if any(c['name'] not in paper for c in entries):
                break
            piles[pile] = [dict(c, grpId=paper[c['name']]['grpId']) for c in entries]
        if len(piles) != 2 or sum(c['quantity'] for c in piles['cards']) != 60:
            continue
        if sum(c['quantity'] for c in piles['sideboard']) > 15:
            continue
        decks.append(dict(name=data['name'], format=format_name, commanders=[], companions=[], **piles))
    return decks


def get_random_bot_deck(catalogue, format_name='Commander', exclude_name=None, exclude_deck=None):
    """Select a random bot deck matching format_name that is not the player's deck."""
    if format_name in commander.FORMATS:
        pool = load_bot_commander_decks(catalogue)
        if exclude_name:
            filtered = [d for d in pool if d['name'] != exclude_name and d['commanders'][0]['name'] != exclude_name]
            if filtered:
                pool = filtered
        if pool:
            return random.choice(pool)
    else:
        pool = load_bot_constructed_decks(catalogue, format_name)
        def signature(deck):
            counts = {}
            for c in deck.get('cards', []):
                counts[c['name']] = counts.get(c['name'], 0) + c['quantity']
            return counts
        pool = [d for d in pool if d['name'] != exclude_name
                and (exclude_deck is None or signature(d) != signature(exclude_deck))]
        if pool:
            return random.choice(pool)
    return None


def get_multiple_random_bot_decks(catalogue, count=3, format_name='Commander', exclude_name=None):
    """Return count distinct random bot decks for multiplayer matches."""
    if format_name in commander.FORMATS:
        pool = load_bot_commander_decks(catalogue)
        if exclude_name:
            filtered = [d for d in pool if d['name'] != exclude_name and d['commanders'][0]['name'] != exclude_name]
            if filtered:
                pool = filtered
        if pool:
            # If we have enough unique decks, sample without replacement
            if len(pool) >= count:
                return random.sample(pool, count)
            # Otherwise, cycle through what we have
            res = list(pool)
            while len(res) < count:
                res.append(random.choice(pool))
            return res[:count]
    return []


def import_deck_text(deck_text, catalogue, deck_name="Imported Online Deck", save=False, save_dir=None):
    """Parse text formatted deck (e.g. from Arena / Moxfield / Scryfall) into a Commander deck."""
    paper = _get_paper_catalogue(catalogue)
    cat_dict = catalogue if isinstance(catalogue, dict) else {c['grpId']: c for c in catalogue}

    commander_name = None
    cards = []
    current_section = "main"

    for line in deck_text.splitlines():
        line = line.strip()
        if not line or line.startswith("//") or line.startswith("#"):
            continue
        lower = line.lower()
        if lower.startswith("commander"):
            current_section = "commander"
            continue
        elif lower.startswith("deck") or lower.startswith("main"):
            current_section = "main"
            continue
        elif lower.startswith("sideboard") or lower.startswith("companion"):
            current_section = "other"
            continue

        match = re.match(r"^(\d+)?\s*x?\s*([^(]+?)(?:\s+\([A-Za-z0-9_]+\)\s+\d+)?$", line)
        if not match:
            continue
        qty = int(match.group(1)) if match.group(1) else 1
        card_name = match.group(2).strip()

        if card_name not in paper:
            # Try cleaning punctuation
            alt_name = card_name.replace("'", "'").strip()
            if alt_name in paper:
                card_name = alt_name
            else:
                raise StorageError(f"Card not available in Arena catalogue: {card_name}")

        if current_section == "commander":
            commander_name = card_name
        elif current_section == "main":
            cards.append({"name": card_name, "quantity": qty, "grpId": paper[card_name]["grpId"]})

    if not commander_name:
        raise StorageError("No commander specified in imported deck")

    total_main = sum(c["quantity"] for c in cards)
    if total_main != 99:
        raise StorageError(f"Commander main deck must contain exactly 99 cards (found {total_main})")

    body = {
        'MainDeck': [{'cardId': c['grpId'], 'quantity': c['quantity']} for c in cards],
        'CommandZone': [{'cardId': paper[commander_name]['grpId'], 'quantity': 1}],
        'Sideboard': [],
        'Companions': []
    }
    commander.validate(body, cat_dict, complete=True)

    key = re.sub(r'[^a-z0-9]+', '-', deck_name.lower()).strip('-')
    data = {
        "key": f"imported-{key}",
        "name": deck_name,
        "format": "Commander",
        "commander": commander_name,
        "cards": [{"name": c["name"], "quantity": c["quantity"]} for c in cards]
    }
    if save:
        target_dir = save_dir or BOT_COMMANDER_DIR
        target_dir.mkdir(parents=True, exist_ok=True)
        (target_dir / f"{key}.json").write_text(json.dumps(data, indent=2), encoding="utf-8")
    return data


def fetch_deck_from_url(url, catalogue, deck_name=None, save=False):
    """Fetch deck from a public URL (raw text or supported endpoints)."""
    req = urllib.request.Request(
        url,
        headers={'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)'}
    )
    with urllib.request.urlopen(req, timeout=10) as resp:
        content = resp.read().decode('utf-8')
    if not deck_name:
        deck_name = f"Online Deck ({url.split('/')[-1][:20]})"
    return import_deck_text(content, catalogue, deck_name=deck_name, save=save)
