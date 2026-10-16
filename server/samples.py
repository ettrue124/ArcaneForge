"""Installed-card sample decks for the local Arena account."""
import json
import time
import uuid
from . import commander
from .storage import StorageError


def commander_sample(catalogue):
    sample = json.loads((commander.ROOT / 'data/sample-commander.json').read_text(encoding='utf8'))
    paper = {}
    for card in catalogue.values():
        metadata = card.get('arenaMetadata', {})
        if metadata.get('IsPrimaryCard', True) and not any(metadata.get(k) for k in ('IsToken', 'IsDigitalOnly', 'IsRebalanced')):
            paper.setdefault(card['name'], card)
    def entry(name, quantity):
        if name not in paper:
            raise StorageError('Sample deck card is not installed: ' + name)
        return {'cardId': paper[name]['grpId'], 'quantity': quantity}
    body = {'MainDeck': [entry(c['name'], c['quantity']) for c in sample['cards']],
            'CommandZone': [entry(sample['commander'], 1)], 'Sideboard': [], 'Companions': [], 'CardSkins': []}
    commander.validate(body, catalogue, complete=True)
    return sample, body


def add_commander_sample(arena, uid):
    """Idempotent local seed; never replace an existing or edited deck."""
    sample, body = commander_sample(arena.catalogue)
    guid = str(uuid.uuid5(uuid.NAMESPACE_URL, f'arena-lab/sample/{uid}/{sample["key"]}'))
    with arena.store._db(write=True) as db:
        existing = db.execute('SELECT summary_json FROM arena_decks WHERE arena_id=? AND user_id=?', (guid, uid)).fetchone()
        if existing:
            return json.loads(existing['summary_json'])
        if not db.execute('SELECT 1 FROM users WHERE id=?', (uid,)).fetchone():
            raise StorageError('Local account not found')
        tile = body['CommandZone'][0]['cardId']
        summary = arena._summary({'DeckId': guid, 'Name': sample['name'], 'DeckTileId': tile,
            'Mana': 'G', 'Attributes': [{'name': 'Format', 'value': 'Commander'},
                {'name': 'TileID', 'value': str(tile)}, {'name': 'IsFavorite', 'value': 'false'}]})
        main = arena.store._validate_cards(db, [{'grpId': c['cardId'], 'quantity': c['quantity']} for c in body['MainDeck']])
        deck_id = db.execute('INSERT INTO decks(user_id,name,cards_json,updated_at) VALUES (?,?,?,?)',
            (uid, sample['name'], json.dumps(main), int(time.time()))).lastrowid
        db.execute('INSERT INTO arena_decks VALUES (?,?,?,?,?)', (guid, uid, deck_id, json.dumps(summary), json.dumps(body)))
        return summary
