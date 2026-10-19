"""Durable local accounts, decks and lobbies. No Arena gameplay engine is claimed.

Every public operation opens its own SQLite connection. Write operations acquire
BEGIN IMMEDIATE so queue pairing and challenge acceptance are atomic. HTTP callers
must obtain user_id from authenticate(token), never from a submitted request body.
Decks use [{"grpId": int, "quantity": int}]. Matches snapshot both decks and await
an engine; client protocol adapters can consume those records when implemented.
"""
import hashlib
import hmac
import json
import secrets
import sqlite3
import time
import unicodedata
from contextlib import contextmanager
from pathlib import Path


class StorageError(Exception):
    def __init__(self, message, status=400):
        super().__init__(message)
        self.status = status


class Store:
    SESSION_SECONDS = 24 * 60 * 60

    def __init__(self, db_path):
        self.db_path = str(db_path)
        Path(self.db_path).parent.mkdir(parents=True, exist_ok=True)
        with self._db(write=True) as db:
            db.executescript("""
                CREATE TABLE IF NOT EXISTS users (
                  id INTEGER PRIMARY KEY, username TEXT NOT NULL,
                  name_key TEXT NOT NULL UNIQUE, salt BLOB NOT NULL,
                  password_hash BLOB NOT NULL, created_at INTEGER NOT NULL);
                CREATE TABLE IF NOT EXISTS sessions (
                  token_hash TEXT PRIMARY KEY, user_id INTEGER NOT NULL REFERENCES users(id),
                  expires_at INTEGER NOT NULL);
                CREATE TABLE IF NOT EXISTS cards (
                  grp_id INTEGER PRIMARY KEY, name TEXT NOT NULL, card_limit INTEGER NOT NULL);
                CREATE TABLE IF NOT EXISTS decks (
                  id INTEGER PRIMARY KEY, user_id INTEGER NOT NULL REFERENCES users(id),
                  name TEXT NOT NULL, cards_json TEXT NOT NULL, updated_at INTEGER NOT NULL);
                CREATE TABLE IF NOT EXISTS queue (
                  user_id INTEGER PRIMARY KEY REFERENCES users(id),
                  deck_id INTEGER NOT NULL REFERENCES decks(id) ON DELETE CASCADE,
                  joined_at INTEGER NOT NULL);
                CREATE TABLE IF NOT EXISTS challenges (
                  id INTEGER PRIMARY KEY, challenger_id INTEGER NOT NULL REFERENCES users(id),
                  opponent_id INTEGER NOT NULL REFERENCES users(id),
                  deck_id INTEGER NOT NULL REFERENCES decks(id) ON DELETE CASCADE,
                  status TEXT NOT NULL DEFAULT 'open', created_at INTEGER NOT NULL);
                CREATE TABLE IF NOT EXISTS matches (
                  id INTEGER PRIMARY KEY, player1 INTEGER NOT NULL REFERENCES users(id),
                  player2 INTEGER NOT NULL REFERENCES users(id),
                  deck1_json TEXT NOT NULL, deck2_json TEXT NOT NULL,
                  status TEXT NOT NULL DEFAULT 'pending_engine', created_at INTEGER NOT NULL);
                CREATE INDEX IF NOT EXISTS matches_players ON matches(player1, player2);
            """)

    @contextmanager
    def _db(self, write=False):
        db = sqlite3.connect(self.db_path, timeout=15)
        db.row_factory = sqlite3.Row
        db.execute('PRAGMA foreign_keys=ON')
        try:
            if write:
                db.execute('BEGIN IMMEDIATE')
            yield db
            db.commit()
        except BaseException:
            db.rollback()
            raise
        finally:
            db.close()

    @staticmethod
    def _password(password, salt):
        return hashlib.scrypt(password.encode('utf-8'), salt=salt, n=16384, r=8, p=1, dklen=32)

    def register(self, username, password):
        if not isinstance(username, str) or not 1 <= len(username.strip()) <= 40:
            raise StorageError('Username must contain 1 to 40 characters')
        username = username.strip()
        if any(ord(c) < 32 for c in username):
            raise StorageError('Username contains control characters')
        if not isinstance(password, str) or not 8 <= len(password) <= 1024:
            raise StorageError('Password must contain 8 to 1024 characters')
        salt = secrets.token_bytes(16)
        digest = self._password(password, salt)
        with self._db(write=True) as db:
            try:
                cursor = db.execute('INSERT INTO users(username,name_key,salt,password_hash,created_at) VALUES (?,?,?,?,?)',
                                    (username, username.casefold(), salt, digest, int(time.time())))
            except sqlite3.IntegrityError:
                raise StorageError('Username already exists', 409) from None
            return {'id': cursor.lastrowid, 'username': username}

    def login(self, username, password):
        if not isinstance(username, str) or not isinstance(password, str) or len(password) > 1024:
            raise StorageError('Invalid credentials', 401)
        with self._db() as db:
            user = db.execute('SELECT * FROM users WHERE name_key=?', (username.strip().casefold(),)).fetchone()
        salt = user['salt'] if user else b'local-invalid-user'
        digest = self._password(password, salt)
        if not user or not hmac.compare_digest(digest, user['password_hash']):
            raise StorageError('Invalid credentials', 401)
        token = secrets.token_urlsafe(32)
        expires = int(time.time()) + self.SESSION_SECONDS
        with self._db(write=True) as db:
            db.execute('DELETE FROM sessions WHERE expires_at<=?', (int(time.time()),))
            db.execute('INSERT INTO sessions VALUES (?,?,?)', (self._token_hash(token), user['id'], expires))
        return {'token': token, 'user': {'id': user['id'], 'username': user['username']}, 'expires_at': expires}

    @staticmethod
    def _token_hash(token):
        if not isinstance(token, str) or len(token) > 512:
            raise StorageError('Invalid session', 401)
        return hashlib.sha256(token.encode()).hexdigest()

    def authenticate(self, token):
        with self._db() as db:
            row = db.execute('SELECT u.id,u.username FROM sessions s JOIN users u ON u.id=s.user_id WHERE s.token_hash=? AND s.expires_at>?',
                             (self._token_hash(token), int(time.time()))).fetchone()
        if not row:
            raise StorageError('Session missing or expired', 401)
        return dict(row)

    def logout(self, token):
        with self._db(write=True) as db:
            db.execute('DELETE FROM sessions WHERE token_hash=?', (self._token_hash(token),))

    def list_users(self, query='', limit=100):
        with self._db() as db:
            return [dict(r) for r in db.execute('SELECT id,username FROM users WHERE instr(lower(username),lower(?))>0 ORDER BY username LIMIT ?',
                                               (query, min(100, max(1, int(limit)))))]

    def import_cards(self, cards, replace=False):
        """Atomic trusted catalogue import. cardLimit=0 means unlimited copies."""
        if not isinstance(cards, list) or len(cards) > 100000:
            raise StorageError('Catalogue must be a list of at most 100000 cards')
        rows = []
        seen = set()
        for card in cards:
            if not isinstance(card, dict):
                raise StorageError('Invalid card record')
            gid, name, cap = card.get('grpId'), card.get('name'), card.get('cardLimit', 4)
            if type(gid) is not int or not 1 <= gid <= 9223372036854775807 or gid in seen or not isinstance(name, str) or not 1 <= len(name.strip()) <= 300 or type(cap) is not int or not 0 <= cap <= 250:
                raise StorageError('Invalid or duplicate catalogue card')
            seen.add(gid)
            rows.append((gid, name, cap))
        with self._db(write=True) as db:
            db.executemany('INSERT INTO cards VALUES (?,?,?) ON CONFLICT(grp_id) DO UPDATE SET name=excluded.name,card_limit=excluded.card_limit', rows)
            if replace:
                db.execute('CREATE TEMP TABLE current_catalogue_ids(grp_id INTEGER PRIMARY KEY)')
                db.executemany('INSERT INTO current_catalogue_ids VALUES (?)', [(r[0],) for r in rows])
                db.execute('DELETE FROM cards WHERE grp_id NOT IN (SELECT grp_id FROM current_catalogue_ids)')
        return {'imported': len(rows)}

    def list_cards(self, query='', limit=100):
        with self._db() as db:
            return [{'grpId': r['grp_id'], 'name': r['name'], 'cardLimit': r['card_limit']} for r in db.execute(
                'SELECT * FROM cards WHERE instr(lower(name),lower(?))>0 ORDER BY name LIMIT ?',
                (query, min(1000, max(1, int(limit)))))]

    @staticmethod
    def _deck_dict(row):
        return {'id': row['id'], 'user_id': row['user_id'], 'name': row['name'],
                'cards': json.loads(row['cards_json']), 'updated_at': row['updated_at']}

    @staticmethod
    def _id(value):
        """Accept positive integer IDs from JSON or numeric URL/UI strings."""
        if isinstance(value, str) and value.isascii() and value.isdigit() and len(value) <= 19:
            value = int(value)
        if type(value) is not int or not 1 <= value <= 9223372036854775807:
            raise StorageError('Expected a positive integer ID')
        return value

    def _get_deck(self, db, user_id, deck_id):
        deck_id = self._id(deck_id)
        row = db.execute('SELECT * FROM decks WHERE id=? AND user_id=?', (deck_id, user_id)).fetchone()
        if not row:
            raise StorageError('Deck not found', 404)
        return self._deck_dict(row)

    def _validate_cards(self, db, cards):
        if not isinstance(cards, list) or not 1 <= len(cards) <= 10000:
            raise StorageError('Deck requires 1 to 10000 card entries')
        totals = {}
        for card in cards:
            if not isinstance(card, dict) or type(card.get('grpId')) is not int or not 1 <= card['grpId'] <= 9223372036854775807 or type(card.get('quantity')) is not int or not 1 <= card['quantity'] <= 10000:
                raise StorageError('Cards require integer grpId and quantity from 1 to 10000')
            totals[card['grpId']] = totals.get(card['grpId'], 0) + card['quantity']
        if sum(totals.values()) > 10000:
            raise StorageError('Deck exceeds the local engine resource limit of 10000 cards')
        canonical_totals = {}
        for gid, quantity in totals.items():
            row = db.execute('SELECT name,card_limit FROM cards WHERE grp_id=?', (gid,)).fetchone()
            if not row:
                raise StorageError('Unknown card grpId: ' + str(gid))
            if row['card_limit'] and quantity > row['card_limit']:
                raise StorageError('Card copy limit exceeded for grpId: ' + str(gid))
            key = unicodedata.normalize('NFKC', row['name']).strip().casefold()
            aggregate = canonical_totals.setdefault(key, {'quantity': 0, 'limits': [], 'name': row['name']})
            aggregate['quantity'] += quantity
            if row['card_limit']:
                aggregate['limits'].append(row['card_limit'])
        for aggregate in canonical_totals.values():
            if aggregate['limits'] and aggregate['quantity'] > min(aggregate['limits']):
                raise StorageError('Card copy limit exceeded across printings: ' + aggregate['name'])
        return [{'grpId': gid, 'quantity': totals[gid]} for gid in sorted(totals)]

    def save_deck(self, user_id, name, cards, deck_id=None):
        if not isinstance(name, str) or not 1 <= len(name.strip()) <= 100:
            raise StorageError('Deck name must contain 1 to 100 characters')
        with self._db(write=True) as db:
            normalized = self._validate_cards(db, cards)
            if deck_id is None:
                deck_id = db.execute('INSERT INTO decks(user_id,name,cards_json,updated_at) VALUES (?,?,?,?)',
                                     (user_id, name.strip(), json.dumps(normalized), int(time.time()))).lastrowid
            else:
                self._get_deck(db, user_id, deck_id)
                db.execute('UPDATE decks SET name=?,cards_json=?,updated_at=? WHERE id=?',
                           (name.strip(), json.dumps(normalized), int(time.time()), deck_id))
            return self._get_deck(db, user_id, deck_id)

    def list_decks(self, user_id):
        with self._db() as db:
            return [self._deck_dict(r) for r in db.execute('SELECT * FROM decks WHERE user_id=? ORDER BY id', (user_id,))]

    def get_deck(self, user_id, deck_id):
        with self._db() as db:
            return self._get_deck(db, user_id, deck_id)

    def delete_deck(self, user_id, deck_id):
        deck_id = self._id(deck_id)
        with self._db(write=True) as db:
            self._get_deck(db, user_id, deck_id)
            db.execute('DELETE FROM decks WHERE id=?', (deck_id,))

    def _ready_deck(self, db, user_id, deck_id):
        deck = self._get_deck(db, user_id, deck_id)
        self._validate_cards(db, deck['cards'])
        if sum(c['quantity'] for c in deck['cards']) < 60:
            raise StorageError('Lobby matches require at least 60 cards (constructed baseline)')
        return deck

    def _create_match(self, db, first, second, deck1, deck2):
        match_id = db.execute('INSERT INTO matches(player1,player2,deck1_json,deck2_json,created_at) VALUES (?,?,?,?,?)',
                             (first, second, json.dumps(deck1), json.dumps(deck2), int(time.time()))).lastrowid
        db.execute('DELETE FROM queue WHERE user_id IN (?,?)', (first, second))
        return self._match_dict(db.execute('SELECT * FROM matches WHERE id=?', (match_id,)).fetchone())

    @staticmethod
    def _match_dict(row):
        return {'id': row['id'], 'players': [row['player1'], row['player2']],
                'decks': [json.loads(row['deck1_json']), json.loads(row['deck2_json'])],
                'status': row['status'], 'created_at': row['created_at']}

    def queue(self, user_id, deck_id):
        deck_id = self._id(deck_id)
        with self._db(write=True) as db:
            deck = self._ready_deck(db, user_id, deck_id)
            # Drop stale/invalid queued decks following catalogue or deck edits.
            candidates = db.execute('SELECT * FROM queue WHERE user_id<>? ORDER BY joined_at,user_id', (user_id,)).fetchall()
            for candidate in candidates:
                try:
                    other_deck = self._ready_deck(db, candidate['user_id'], candidate['deck_id'])
                except StorageError:
                    db.execute('DELETE FROM queue WHERE user_id=?', (candidate['user_id'],))
                    continue
                return {'status': 'matched', 'match': self._create_match(db, candidate['user_id'], user_id, other_deck, deck)}
            db.execute('INSERT INTO queue VALUES (?,?,?) ON CONFLICT(user_id) DO UPDATE SET deck_id=excluded.deck_id',
                       (user_id, deck_id, int(time.time())))
            return {'status': 'queued'}

    def cancel_queue(self, user_id):
        with self._db(write=True) as db:
            db.execute('DELETE FROM queue WHERE user_id=?', (user_id,))

    def challenge(self, user_id, opponent_id, deck_id):
        opponent_id = self._id(opponent_id)
        deck_id = self._id(deck_id)
        if user_id == opponent_id:
            raise StorageError('Cannot challenge yourself')
        with self._db(write=True) as db:
            self._ready_deck(db, user_id, deck_id)
            if not db.execute('SELECT id FROM users WHERE id=?', (opponent_id,)).fetchone():
                raise StorageError('Opponent not found', 404)
            existing = db.execute("SELECT * FROM challenges WHERE challenger_id=? AND opponent_id=? AND status='open'", (user_id, opponent_id)).fetchone()
            if existing:
                raise StorageError('An open challenge already exists', 409)
            cid = db.execute('INSERT INTO challenges(challenger_id,opponent_id,deck_id,created_at) VALUES (?,?,?,?)',
                             (user_id, opponent_id, deck_id, int(time.time()))).lastrowid
            return dict(db.execute('SELECT * FROM challenges WHERE id=?', (cid,)).fetchone())

    def list_challenges(self, user_id):
        with self._db() as db:
            return [dict(r) for r in db.execute("SELECT * FROM challenges WHERE (challenger_id=? OR opponent_id=?) AND status='open' ORDER BY id", (user_id, user_id))]

    def accept_challenge(self, user_id, challenge_id, deck_id):
        challenge_id = self._id(challenge_id)
        deck_id = self._id(deck_id)
        with self._db(write=True) as db:
            challenge = db.execute("SELECT * FROM challenges WHERE id=? AND opponent_id=? AND status='open'", (challenge_id, user_id)).fetchone()
            if not challenge:
                raise StorageError('Open challenge not found', 404)
            deck = self._ready_deck(db, user_id, deck_id)
            other = self._ready_deck(db, challenge['challenger_id'], challenge['deck_id'])
            db.execute("UPDATE challenges SET status='accepted' WHERE id=?", (challenge_id,))
            return self._create_match(db, challenge['challenger_id'], user_id, other, deck)

    def cancel_challenge(self, user_id, challenge_id):
        challenge_id = self._id(challenge_id)
        with self._db(write=True) as db:
            cursor = db.execute("UPDATE challenges SET status='cancelled' WHERE id=? AND (challenger_id=? OR opponent_id=?) AND status='open'", (challenge_id, user_id, user_id))
            if not cursor.rowcount:
                raise StorageError('Open challenge not found', 404)

    def list_matches(self, user_id):
        with self._db() as db:
            return [self._match_dict(r) for r in db.execute('SELECT * FROM matches WHERE player1=? OR player2=? ORDER BY id DESC LIMIT 100', (user_id, user_id))]

    def get_match(self, user_id, match_id):
        match_id = self._id(match_id)
        with self._db() as db:
            row = db.execute('SELECT * FROM matches WHERE id=? AND (player1=? OR player2=?)', (match_id, user_id, user_id)).fetchone()
            if not row:
                raise StorageError('Match not found', 404)
            return self._match_dict(row)
