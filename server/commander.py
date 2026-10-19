"""Paper Commander policy, shared by deck saves and match creation."""
import json
from collections import Counter
from functools import lru_cache
from pathlib import Path
from .storage import StorageError
from . import rulebreakers

FORMATS = ('Commander', 'Commander4', 'CommanderUnbanned')
ROOT = Path(__file__).resolve().parents[1]


@lru_cache(maxsize=1)
def bans():
    return json.loads((ROOT / 'data/commander-bans.json').read_text(encoding='utf8'))


def deck_format(summary):
    return next((a['value'] for a in summary.get('Attributes', []) if a['name'] == 'Format'), 'Historic')


@lru_cache(maxsize=1)
def eligible_commanders():
    return set(json.loads((ROOT / 'data/commander-eligible.json').read_text(encoding='utf8'))['names'])


def validate(body, catalogue, complete=False, format_name='Commander'):
    policy = bans()
    unbanned = format_name == 'CommanderUnbanned'
    prohibited = set() if unbanned else set(policy['bannedNames'])
    companion_bans = set() if unbanned else set(policy['bannedAsCompanion'])
    counts = Counter()
    for pile in ('MainDeck', 'CommandZone', 'Sideboard', 'Companions'):
        for entry in body.get(pile, []):
            card = catalogue.get(entry['cardId'])
            if not card:
                raise StorageError('Unknown Commander card: ' + str(entry['cardId']))
            name = card['name']
            if name in prohibited:
                raise StorageError(name + ' is banned in Commander')
            if pile == 'Companions' and name in companion_bans:
                raise StorageError(name + ' is banned as a Commander companion')
            metadata = card.get('arenaMetadata', {})
            if metadata.get('IsToken') or not unbanned and (metadata.get('IsDigitalOnly') or metadata.get('IsRebalanced')):
                raise StorageError(name + ' is not a paper Commander card')
            if pile != 'Companions':
                counts[name] += entry['quantity']
                limit = card.get('cardLimit', 4)
                limit = 10000 if limit == 0 else max(1, limit) if limit != 4 else 1
                if counts[name] > limit:
                    raise StorageError('Commander copy limit exceeded: ' + name)
    commanders = body.get('CommandZone', [])
    command_count = sum(c['quantity'] for c in commanders)
    if command_count > 2 or any(c['quantity'] != 1 for c in commanders):
        raise StorageError('Commander needs one commander or a legal pair of two commanders')
    for entry in commanders:
        if catalogue[entry['cardId']]['name'] not in eligible_commanders():
            raise StorageError(catalogue[entry['cardId']]['name'] + ' cannot be a Commander')
    companions = body.get('Companions', [])
    if len(companions) > 1 or any(c['quantity'] != 1 for c in companions):
        raise StorageError('Only one companion is permitted')
    if body.get('Sideboard', []) != companions:
        raise StorageError('Commander has no sideboard except the designated companion')
    if not complete:
        return
    total = sum(c['quantity'] for c in body.get('MainDeck', [])) + command_count
    rules = rulebreakers.active(commanders, catalogue)
    if command_count not in (1, 2) or total < 100 or total != 100 and not rulebreakers.unlimited(rules):
        raise StorageError('Commander requires exactly 100 cards including one commander or a legal commander pair')
    identity = set()
    for entry in commanders:
        card = catalogue[entry['cardId']]
        identity.update(filter(None, card.get('arenaMetadata', {}).get('ColorIdentity', '').split(',')))
    chosen = rulebreakers.additional_colors([catalogue[e['cardId']] for pile in ('MainDeck', 'Sideboard') for e in body.get(pile, [])], identity, rules)
    for pile in ('MainDeck', 'Sideboard'):
        for entry in body.get(pile, []):
            card = catalogue[entry['cardId']]
            colors = set(filter(None, card.get('arenaMetadata', {}).get('ColorIdentity', '').split(',')))
            if not rulebreakers.allows(card, identity, rules, chosen):
                raise StorageError(card['name'] + " is outside the commander's color identity")
    # Forge performs the final commander eligibility, partner/background and
    # card-specific deck-rule checks before it starts the match.
