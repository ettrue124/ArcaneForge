"""Authenticated local sandbox collection and recovered Arena deck CRUD DTOs.

The native deck DTO is preserved, including non-main piles and cosmetics. Main
decks with cards are shared with Store; empty builder drafts have an Arena-only
record. Commander drafts enforce the paper ban and copy policy; complete match
decks are also validated by Forge. HTTP exposes this on the authenticated loopback API.
"""
from datetime import datetime, timezone
import hashlib
import json
import time
import uuid
from . import commander

from .storage import StorageError


class Arena:
    COMMANDS = frozenset(('DeckGetDeckSummariesV3', 'DeckGetDeck', 'DeckUpsertDeckV3',
                          'DeckDeleteDeck', 'CardGetAllCards', 'CardGetCardSet'))
    PILES = ('MainDeck', 'Sideboard', 'CommandZone', 'Companions')

    def __init__(self, store, catalogue=None):
        self.store = store
        self.catalogue = {c['grpId']: c for c in (catalogue or [])}
        self._cached_collection = None
        with store._db(write=True) as db:
            db.execute('''CREATE TABLE IF NOT EXISTS arena_decks(
                arena_id TEXT PRIMARY KEY,user_id INTEGER NOT NULL REFERENCES users(id),
                deck_id INTEGER UNIQUE REFERENCES decks(id) ON DELETE CASCADE,
                summary_json TEXT NOT NULL,body_json TEXT NOT NULL)''')

    @staticmethod
    def _guid(value):
        if not isinstance(value, str) or len(value) > 40:
            raise StorageError('Expected a deck GUID')
        try:
            return str(uuid.UUID(value))
        except (ValueError, AttributeError):
            raise StorageError('Expected a deck GUID') from None

    @staticmethod
    def _text(value, field, limit=500, nullable=False):
        if nullable and value is None:
            return None
        if not isinstance(value, str) or len(value) > limit:
            raise StorageError('Invalid deck ' + field)
        return value

    def _summary(self, raw):
        if not isinstance(raw, dict):
            raise StorageError('Deck Summary must be an object')
        gid = self._guid(raw.get('DeckId'))
        if gid == str(uuid.UUID(int=0)):
            gid = str(uuid.uuid4())
        name = self._text(raw.get('Name'), 'Name', 100)
        if not name.strip():
            raise StorageError('Deck Name cannot be empty')
        attributes = raw.get('Attributes', [])
        if attributes is None:
            attributes = []
        if not isinstance(attributes, list) or len(attributes) > 30:
            raise StorageError('Invalid deck Attributes')
        seen, clean = set(), []
        for attribute in attributes:
            if not isinstance(attribute, dict):
                raise StorageError('Invalid deck attribute')
            key = self._text(attribute.get('name'), 'attribute name', 100)
            value = self._text(attribute.get('value'), 'attribute value', 2000)
            if not key or key in seen:
                raise StorageError('Duplicate or empty deck attribute name')
            seen.add(key)
            clean.append({'name': key, 'value': value})
        if 'Version' not in seen:
            clean.append({'name': 'Version', 'value': '11'})
        if 'Format' not in seen:
            clean.append({'name': 'Format', 'value': 'Historic'})
        cosmetics = raw.get('PreferredCosmetics', {})
        if cosmetics is None:
            cosmetics = {}
        if not isinstance(cosmetics, dict):
            raise StorageError('Invalid deck PreferredCosmetics')
        emotes = cosmetics.get('Emotes', [])
        if emotes is None:
            emotes = []
        if not isinstance(emotes, list) or len(emotes) > 50:
            raise StorageError('Invalid deck Emotes')
        preferred = {key: self._text(cosmetics.get(key) or '', key, 200) for key in ('Sleeve', 'Avatar', 'Pet', 'Title')}
        preferred['Emotes'] = [self._text(v, 'Emote', 200) for v in emotes]
        tile, art = raw.get('DeckTileId'), raw.get('DeckArtId')
        for value in (tile, art):
            if value is not None and (type(value) is not int or not 0 <= value <= 4294967295):
                raise StorageError('Invalid deck art ID')
        for field in ('IsCompanionValid', 'IsNetDeck', 'checkInbox'):
            if field in raw and type(raw[field]) is not bool:
                raise StorageError('Invalid deck ' + field)
        folder = raw.get('NetDeckFolderId')
        if folder is not None:
            folder = self._guid(folder)
        return {'DeckId': gid, 'Mana': self._text(raw.get('Mana') or '', 'Mana', 30),
                'Name': name.strip(), 'Attributes': clean,
                'Description': self._text(raw.get('Description') or '', 'Description', 4000),
                'DeckTileId': tile, 'DeckArtId': art,
                'IsCompanionValid': raw.get('IsCompanionValid', False),
                'PreferredCosmetics': preferred, 'NetDeckFolderId': folder,
                'IsNetDeck': raw.get('IsNetDeck', False), 'checkInbox': raw.get('checkInbox', False)}

    def _body(self, db, raw):
        if not isinstance(raw, dict):
            raise StorageError('Deck must be an object')
        result = {}
        all_cards = []
        for pile in self.PILES:
            entries = raw.get(pile, [])
            if entries is None:
                entries = []
            if not isinstance(entries, list) or len(entries) > 10000:
                raise StorageError('Invalid deck ' + pile)
            totals = {}
            for entry in entries:
                if not isinstance(entry, dict):
                    raise StorageError('Invalid deck card')
                card_id, quantity = entry.get('cardId'), entry.get('quantity')
                if type(card_id) is not int or not 1 <= card_id <= 4294967295 or type(quantity) is not int or not 1 <= quantity <= 10000:
                    raise StorageError('Invalid deck card ID or quantity')
                totals[card_id] = totals.get(card_id, 0) + quantity
            normalized = [{'grpId': gid, 'quantity': qty} for gid, qty in totals.items()]
            if normalized:
                normalized = self.store._validate_cards(db, normalized)
            result[pile] = [{'cardId': c['grpId'], 'quantity': c['quantity']} for c in normalized]
            # Companion is a separate reference to a sideboard card, not an
            # additional owned copy. Validate its cards, but do not double-count.
            if pile != 'Companions':
                all_cards.extend(normalized)
        if all_cards:
            self.store._validate_cards(db, all_cards)
        skins = raw.get('CardSkins', [])
        if skins is None:
            skins = []
        if not isinstance(skins, list) or len(skins) > 250:
            raise StorageError('Invalid deck CardSkins')
        clean_skins = []
        for skin in skins:
            if not isinstance(skin, dict) or type(skin.get('GrpId')) is not int or not 1 <= skin['GrpId'] <= 4294967295:
                raise StorageError('Invalid deck CardSkin')
            if not db.execute('SELECT grp_id FROM cards WHERE grp_id=?', (skin['GrpId'],)).fetchone():
                raise StorageError('Unknown card skin ID')
            clean_skins.append({'GrpId': skin['GrpId'], 'CCV': self._text(skin.get('CCV') or '', 'CCV', 200)})
        result['CardSkins'] = clean_skins
        return result

    def _get(self, db, uid, guid):
        guid = self._guid(guid)
        row = db.execute('SELECT * FROM arena_decks WHERE arena_id=? AND user_id=?', (guid, uid)).fetchone()
        if not row:
            raise StorageError('Deck not found', 404)
        if row['deck_id'] is not None:
            self.store._get_deck(db, uid, row['deck_id'])
        return row

    def _sync(self, db, uid):
        """Expose existing local control-panel decks to the same Arena account."""
        rows = db.execute('SELECT d.* FROM decks d LEFT JOIN arena_decks a ON a.deck_id=d.id WHERE d.user_id=? AND a.arena_id IS NULL', (uid,)).fetchall()
        for row in rows:
            deck = self.store._deck_dict(row)
            guid = str(uuid.uuid4())
            first = deck['cards'][0]['grpId'] if deck['cards'] else None
            timestamp = datetime.fromtimestamp(deck['updated_at'], timezone.utc).isoformat()
            summary = self._summary({'DeckId': guid, 'Name': deck['name'], 'DeckTileId': first,
                'DeckArtId': None, 'Attributes': [
                    {'name': 'Version', 'value': '11'}, {'name': 'Format', 'value': 'Historic'},
                    {'name': 'TileID', 'value': json.dumps(first or 0)},
                    {'name': 'IsFavorite', 'value': 'false'},
                    {'name': 'LastUpdated', 'value': json.dumps(timestamp)},
                    {'name': 'LastPlayed', 'value': json.dumps('0001-01-01T00:00:00Z')}]})
            body = {pile: [] for pile in self.PILES}
            body['MainDeck'] = [{'cardId': c['grpId'], 'quantity': c['quantity']} for c in deck['cards']]
            body['CardSkins'] = []
            db.execute('INSERT INTO arena_decks VALUES (?,?,?,?,?)', (guid, uid, deck['id'], json.dumps(summary), json.dumps(body)))
        # Web-side edits to the shared main deck remain visible in Arena.
        for row in db.execute('SELECT a.*,d.name,d.cards_json FROM arena_decks a JOIN decks d ON d.id=a.deck_id WHERE a.user_id=?', (uid,)).fetchall():
            summary, body = json.loads(row['summary_json']), json.loads(row['body_json'])
            summary['Name'] = row['name']
            body['MainDeck'] = [{'cardId': c['grpId'], 'quantity': c['quantity']} for c in json.loads(row['cards_json'])]
            db.execute('UPDATE arena_decks SET summary_json=?,body_json=? WHERE arena_id=?', (json.dumps(summary), json.dumps(body), row['arena_id']))

    def _collection(self, db):
        if self._cached_collection is not None:
            return self._cached_collection
        owned = {}
        for row in db.execute('SELECT grp_id,card_limit FROM cards ORDER BY grp_id'):
            metadata = self.catalogue.get(row['grp_id'], {}).get('arenaMetadata') or {}
            if metadata.get('IsToken') in (1, True, '1') or metadata.get('IsPrimaryCard', 1) in (0, False, '0'):
                continue
            owned[str(row['grp_id'])] = 250 if row['card_limit'] == 0 else 4
        version = (int(hashlib.sha256(json.dumps(owned, sort_keys=True).encode()).hexdigest()[:8], 16) & 0x7fffffff) or 1
        self._cached_collection = {'cacheVersion': version, 'cards': owned}
        return self._cached_collection

    def execute(self, token, name, payload):
        user = self.store.authenticate(token)
        uid = user['id']
        if not isinstance(name, str) or name not in self.COMMANDS:
            raise StorageError('Arena command is not implemented', 501)
        if not isinstance(payload, dict):
            raise StorageError('Arena command payload must be an object')
        with self.store._db(write=True) as db:
            if name in ('CardGetAllCards', 'CardGetCardSet'):
                if name == 'CardGetCardSet':
                    set_code = payload.get('SetCode')
                    if not isinstance(set_code, str) or not 1 <= len(set_code) <= 30:
                        raise StorageError('Invalid collection set code')
                    if not self.catalogue:
                        raise StorageError('Local set metadata is unavailable', 503)
                    owned = self._collection(db)['cards']
                    return {gid: quantity for gid, quantity in owned.items()
                            if self.catalogue.get(int(gid), {}).get('arenaMetadata', {}).get('ExpansionCode') == set_code}
                cache = payload.get('CacheVersion', 0)
                if type(cache) is not int or not -2147483648 <= cache <= 2147483647:
                    raise StorageError('Invalid collection cache version')
                return self._collection(db)
            self._sync(db, uid)
            if name == 'DeckGetDeckSummariesV3':
                return {'Summaries': [json.loads(r['summary_json']) for r in db.execute('SELECT summary_json FROM arena_decks WHERE user_id=? ORDER BY arena_id', (uid,))]}
            if name == 'DeckGetDeck':
                return json.loads(self._get(db, uid, payload.get('DeckId'))['body_json'])
            if name == 'DeckDeleteDeck':
                guid = self._guid(payload.get('DeckId'))
                row = db.execute('SELECT * FROM arena_decks WHERE arena_id=? AND user_id=?', (guid, uid)).fetchone()
                # Arena can retain a summary after an earlier deletion or retry
                # the same request. The requested final state already exists.
                if row is None:
                    return 'success'
                if row['deck_id'] is not None:
                    db.execute('DELETE FROM decks WHERE id=? AND user_id=?', (row['deck_id'], uid))
                db.execute('DELETE FROM arena_decks WHERE arena_id=? AND user_id=?', (row['arena_id'], uid))
                return 'success'
            summary = self._summary(payload.get('Summary'))
            body = self._body(db, payload.get('Deck'))
            if commander.deck_format(summary) in commander.FORMATS:
                commander.validate(body, self.catalogue, format_name=commander.deck_format(summary))
            action = payload.get('ActionType', 0)
            if isinstance(action, str):
                action_names = ('None', 'CreatedNew', 'Imported', 'Cloned', 'CopyLimited', 'PreconCopied', 'Exported', 'Updated')
                if action not in action_names:
                    raise StorageError('Invalid deck ActionType')
                action = action_names.index(action) - 1
            if type(action) is not int or not -1 <= action <= 6:
                raise StorageError('Invalid deck ActionType')
            existing = db.execute('SELECT * FROM arena_decks WHERE arena_id=?', (summary['DeckId'],)).fetchone()
            if existing and existing['user_id'] != uid:
                raise StorageError('Deck not found', 404)
            deck_id = existing['deck_id'] if existing else None
            if deck_id is not None:
                self.store._get_deck(db, uid, deck_id)
            main = [{'grpId': c['cardId'], 'quantity': c['quantity']} for c in body['MainDeck']]
            if main:
                main = self.store._validate_cards(db, main)
                if deck_id is None:
                    deck_id = db.execute('INSERT INTO decks(user_id,name,cards_json,updated_at) VALUES (?,?,?,?)',
                        (uid, summary['Name'], json.dumps(main), int(time.time()))).lastrowid
                else:
                    db.execute('UPDATE decks SET name=?,cards_json=?,updated_at=? WHERE id=? AND user_id=?',
                        (summary['Name'], json.dumps(main), int(time.time()), deck_id, uid))
            elif deck_id is not None:
                # Detach before deleting the main deck, retaining the empty draft.
                db.execute('UPDATE arena_decks SET deck_id=NULL WHERE arena_id=?', (summary['DeckId'],))
                db.execute('DELETE FROM decks WHERE id=? AND user_id=?', (deck_id, uid))
                deck_id = None
            db.execute('INSERT INTO arena_decks VALUES (?,?,?,?,?) ON CONFLICT(arena_id) DO UPDATE SET deck_id=excluded.deck_id,summary_json=excluded.summary_json,body_json=excluded.body_json',
                (summary['DeckId'], uid, deck_id, json.dumps(summary), json.dumps(body)))
            return summary

    def dispatch(self, body, bearer):
        if not isinstance(body, dict):
            raise StorageError('Arena command body must be an object')
        return {'result': self.execute(bearer, body.get('name'), body.get('payload', {}))}
