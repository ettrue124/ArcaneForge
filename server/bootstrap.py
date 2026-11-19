"""Dedicated loopback WAS and doorbell contracts recovered from this client.

JWT claims are descriptive only. Access is granted only when the exact issued
token hash exists in Store.sessions; refresh credentials have a separate table.
No external account system is contacted. The HTTP owner must bind to loopback,
bound request bodies and avoid logging request headers, bodies or token replies.
"""
import base64
import hashlib
import hmac
import ipaddress
import json
from pathlib import Path
import re
import secrets
import time
from urllib.parse import parse_qs, urlsplit
import uuid

from .storage import StorageError


class BootstrapError(StorageError):
    pass


# Patched PC clients sign in with this instead of a saved refresh token.
DEVICE_TOKEN = re.compile(r'local-device-[0-9a-f]{64}')


def installed_manifests(downloads):
    """Recover actual installed filenames; do not invent remote bundle hashes."""
    result = []
    for path in sorted(Path(downloads).glob('Manifest_*.mtga')):
        match = re.fullmatch(r'Manifest_(?:(.+)_)?([a-fA-F0-9]{32})\.mtga', path.name)
        if match:
            result.append({'priority': 50, 'category': match[1] or '', 'hash': match[2]})
    return result


class Bootstrap:
    ACCESS_SECONDS = 3600
    REFRESH_SECONDS = 30 * 24 * 3600
    MAX_BODY = 16384

    def __init__(self, store, fd_uri='tcp://127.0.0.1:9443', manifests=None,
                 client_id='arena-local', client_secret='local-only', ios_manifests=None):
        parsed = urlsplit(fd_uri)
        if parsed.scheme != 'tcp' or parsed.hostname not in ('127.0.0.1', 'localhost', '::1') or not parsed.port or parsed.path or parsed.username or parsed.password:
            raise ValueError('Bootstrap FrontDoor URI must name a local TCP listener')
        if not isinstance(client_id, str) or not client_id or ':' in client_id or not isinstance(client_secret, str) or not client_secret:
            raise ValueError('Dedicated local OAuth client identity required')
        self.store, self.fd_uri = store, fd_uri
        self.client_id, self.client_secret = client_id, client_secret
        self.manifests = self._manifests(manifests or [])
        # iPhone Arena needs Wizards' own iOS manifests: the PC manifests above
        # name Windows bundles (and local edits) that do not exist for iOS.
        self.ios_manifests = self._manifests(ios_manifests or [])
        with store._db(write=True) as db:
            db.executescript('''
                CREATE TABLE IF NOT EXISTS bootstrap_settings(key TEXT PRIMARY KEY,value TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS bootstrap_refresh(
                    token_hash TEXT PRIMARY KEY,user_id INTEGER NOT NULL REFERENCES users(id),
                    access_hash TEXT NOT NULL,expires_at INTEGER NOT NULL);
                CREATE TABLE IF NOT EXISTS bootstrap_devices(
                    device_hash TEXT PRIMARY KEY,user_id INTEGER NOT NULL REFERENCES users(id),
                    created_at INTEGER NOT NULL,last_seen INTEGER NOT NULL);
            ''')
            db.execute('INSERT OR IGNORE INTO bootstrap_settings VALUES (?,?)', ('jwt_key', secrets.token_hex(32)))
            db.execute('INSERT OR IGNORE INTO bootstrap_settings VALUES (?,?)', ('identity_namespace', str(uuid.uuid4())))
            settings = {row['key']: row['value'] for row in db.execute('SELECT * FROM bootstrap_settings')}
        self._key = bytes.fromhex(settings['jwt_key'])
        self._namespace = uuid.UUID(settings['identity_namespace'])

    @staticmethod
    def _manifests(manifests):
        if not isinstance(manifests, list):
            raise ValueError('Manifest configuration must be a list')
        result, seen = [], set()
        for item in manifests:
            if not isinstance(item, dict) or type(item.get('priority')) is not int or not isinstance(item.get('category'), str) or not isinstance(item.get('hash'), str) or not re.fullmatch(r'[a-fA-F0-9]{32}', item['hash']):
                raise ValueError('Invalid installed manifest metadata')
            if not re.fullmatch(r'[A-Za-z0-9_-]*', item['category']):
                raise ValueError('Invalid manifest category')
            key = (item['category'], item['hash'])
            if key in seen:
                raise ValueError('Duplicate manifest metadata')
            seen.add(key)
            result.append({k: item[k] for k in ('priority', 'category', 'hash')})
        return result

    def identity(self, user):
        uid = self.store._id(user['id'])
        return {'persona_id': str(uuid.uuid5(self._namespace, f'persona:{uid}')),
                'account_id': str(uuid.uuid5(self._namespace, f'account:{uid}'))}

    @staticmethod
    def _b64(value):
        return base64.urlsafe_b64encode(value).decode('ascii').rstrip('=')

    def _access(self, user, expires):
        now = int(time.time())
        claims = {'iss': 'arena-local', 'sub': self.identity(user)['persona_id'],
                  'iat': now, 'exp': expires, 'jti': secrets.token_hex(12),
                  'wotc-rols': ['ArenaLocal'], 'wotc-flgs': 0}
        header = self._b64(b'{"alg":"HS256","typ":"JWT"}')
        payload = self._b64(json.dumps(claims, separators=(',', ':')).encode())
        signed = header + '.' + payload
        token = signed + '.' + self._b64(hmac.new(self._key, signed.encode(), hashlib.sha256).digest())
        if len(token) > 512:
            raise RuntimeError('Local JWT exceeded transport token bound')
        return token

    def _issue(self, db, user):
        now = int(time.time())
        db.execute('DELETE FROM sessions WHERE expires_at<=?', (now,))
        db.execute('DELETE FROM bootstrap_refresh WHERE expires_at<=?', (now,))
        expires = now + self.ACCESS_SECONDS
        access = self._access(user, expires)
        refresh = 'local-refresh-' + secrets.token_urlsafe(32)
        access_hash = self.store._token_hash(access)
        db.execute('INSERT INTO sessions VALUES (?,?,?)', (access_hash, user['id'], expires))
        db.execute('INSERT INTO bootstrap_refresh VALUES (?,?,?,?)',
                   (self.store._token_hash(refresh), user['id'], access_hash, now + self.REFRESH_SECONDS))
        identity = self.identity(user)
        return {'access_token': access, 'refresh_token': refresh, 'expires_in': self.ACCESS_SECONDS,
                'token_type': 'Bearer', 'client_id': self.client_id, 'game_id': 'MTGA',
                'domain_id': 'arena-local', **identity, 'display_name': user['username'],
                'conflictingPersonaID': None}

    def _basic(self, authorization):
        if not isinstance(authorization, str) or len(authorization) > 1024:
            raise BootstrapError('INVALID CLIENT CREDENTIALS', 401)
        scheme, _, encoded = authorization.partition(' ')
        try:
            decoded = base64.b64decode(encoded, validate=True).decode('utf-8')
        except (ValueError, UnicodeError):
            raise BootstrapError('INVALID CLIENT CREDENTIALS', 401) from None
        expected = self.client_id + ':' + self.client_secret
        if scheme.casefold() != 'basic' or not hmac.compare_digest(decoded.encode(), expected.encode()):
            raise BootstrapError('INVALID CLIENT CREDENTIALS', 401)

    def oauth(self, form, basic_header):
        self._basic(basic_header)
        if not isinstance(form, dict) or any(not isinstance(k, str) or not isinstance(v, str) for k, v in form.items()):
            raise BootstrapError('INVALID REQUEST', 400)
        grant = form.get('grant_type')
        if grant == 'password':
            try:
                login = self.store.login(form.get('username', ''), form.get('password', ''))
            except StorageError:
                raise BootstrapError('INVALID ACCOUNT CREDENTIALS', 401) from None
            with self.store._db(write=True) as db:
                db.execute('DELETE FROM sessions WHERE token_hash=?', (self.store._token_hash(login['token']),))
                return self._issue(db, login['user'])
        if grant == 'refresh_token' and DEVICE_TOKEN.fullmatch(form.get('refresh_token', '')):
            return self._device_login(form['refresh_token'])
        if grant == 'refresh_token':
            token = form.get('refresh_token', '')
            if not isinstance(token, str) or not token.startswith('local-refresh-') or len(token) > 512:
                raise BootstrapError('INVALID REFRESH TOKEN', 401)
            with self.store._db(write=True) as db:
                digest = self.store._token_hash(token)
                row = db.execute('SELECT r.*,u.username FROM bootstrap_refresh r JOIN users u ON u.id=r.user_id WHERE r.token_hash=?', (digest,)).fetchone()
                if not row:
                    raise BootstrapError('INVALID REFRESH TOKEN', 401)
                if row['expires_at'] <= int(time.time()):
                    raise BootstrapError('REFRESH TOKEN EXPIRED', 401)
                db.execute('DELETE FROM bootstrap_refresh WHERE token_hash=?', (digest,))
                db.execute('DELETE FROM sessions WHERE token_hash=?', (row['access_hash'],))
                return self._issue(db, {'id': row['user_id'], 'username': row['username']})
        raise BootstrapError('UNSUPPORTED GRANT TYPE', 400)

    def _device_login(self, token):
        """Sign in a patched PC client by its device token (no password).
        The patched client sends 'local-device-' + SHA-256 of Unity's
        deviceUniqueIdentifier; only a hash of that token is stored. A new
        device gets its own account; tools/device_accounts.py can link a device
        to an existing account instead."""
        digest = self.store._token_hash(token)
        now = int(time.time())
        with self.store._db(write=True) as db:
            row = db.execute('SELECT d.user_id,u.username FROM bootstrap_devices d JOIN users u ON u.id=d.user_id WHERE d.device_hash=?',
                             (digest,)).fetchone()
            if row:
                db.execute('UPDATE bootstrap_devices SET last_seen=? WHERE device_hash=?', (now, digest))
                return self._issue(db, {'id': row['user_id'], 'username': row['username']})
        for attempt in range(20):
            name = 'Player ' + digest[:6].upper() + ('' if attempt == 0 else '-' + str(attempt + 1))
            try:
                user = self.store.register(name, secrets.token_urlsafe(32))
                break
            except StorageError as error:
                if getattr(error, 'status', None) != 409 and 'exists' not in str(error):
                    raise
        else:
            raise BootstrapError('DEVICE ACCOUNT UNAVAILABLE', 500)
        with self.store._db(write=True) as db:
            db.execute('INSERT OR IGNORE INTO bootstrap_devices VALUES (?,?,?,?)', (digest, user['id'], now, now))
            row = db.execute('SELECT d.user_id,u.username FROM bootstrap_devices d JOIN users u ON u.id=d.user_id WHERE d.device_hash=?',
                             (digest,)).fetchone()
            print('New device sign-in: created account ' + row['username'], flush=True)
            return self._issue(db, {'id': row['user_id'], 'username': row['username']})

    def profile(self, bearer):
        try:
            user = self.store.authenticate(bearer)
        except StorageError:
            raise BootstrapError('INVALID ACCESS TOKEN', 401) from None
        identity = self.identity(user)
        return {'accountID': identity['account_id'], 'personaID': identity['persona_id'],
                'displayName': user['username'], 'gameID': 'MTGA', 'externalID': identity['account_id'],
                'email': user['username'] if '@' in user['username'] else '', 'dataOptIn': False,
                'presenceSettings': {'socialMode': 'PRIVATE'}, 'countryCode': ''}

    def revoke(self, access_token):
        """Revoke an access token and its refresh credential together on logout."""
        digest = self.store._token_hash(access_token)
        with self.store._db(write=True) as db:
            db.execute('DELETE FROM sessions WHERE token_hash=?', (digest,))
            db.execute('DELETE FROM bootstrap_refresh WHERE access_hash=?', (digest,))

    def _front_door(self, host):
        """Point a client that reached us by a non-loopback IP (an iPhone over
        Tailscale) at that same IP; a hostname is ignored so DNS rebinding cannot
        redirect it. The TLS certificate must name the address."""
        try:
            address = ipaddress.ip_address(urlsplit('//' + (host or '')).hostname or '')
        except ValueError:
            return self.fd_uri
        if address.is_loopback:
            return self.fd_uri
        shown = f'[{address}]' if address.version == 6 else str(address)
        return f'tcp://{shown}:{urlsplit(self.fd_uri).port}'

    def ring(self, data, v2=False, host=None):
        if not isinstance(data, dict):
            raise BootstrapError('INVALID DOORBELL REQUEST', 400)
        expected = ('code', 'environment', 'clientVersion', 'platform', 'playerId') if v2 else (
            'playerId', 'clientVersion', 'environmentKey', 'platformKey')
        if any(not isinstance(data.get(key), str) or len(data[key]) > 512 for key in expected):
            raise BootstrapError('INVALID DOORBELL REQUEST', 400)
        platform = (data['platform'] if v2 else data['platformKey']).casefold()
        ios = any(name in platform for name in ('ios', 'iphone', 'ipad'))
        manifests = self.ios_manifests if ios else self.manifests
        if not manifests or not any(m['category'] == '' and m['priority'] == 50 for m in manifests):
            raise BootstrapError('LOCAL CONTENT MANIFESTS NOT CONFIGURED', 503)
        fd_uri = self._front_door(host)
        if v2:
            return {'FdURI': fd_uri, 'BundleManifests': [dict(m) for m in manifests]}
        general = next(m['hash'] for m in manifests if m['category'] == '' and m['priority'] == 50)
        # Legacy client constructs category-less filenames. Category-specific
        # manifests can only be expressed through the recovered V2 contract.
        future = [m['hash'] for m in manifests if m['category'] == '' and m['priority'] == 500]
        return {'fdURI': fd_uri, 'contentHash': general,
                'preloadContentHashes': future, 'configurationRoot': {}}

    def dispatch(self, method, path, headers, body=b''):
        """Return (status, JSON) for recognized routes, else None. Never logs."""
        path = urlsplit(path).path.rstrip('/') or '/'
        recognized = (method, path) in {('POST', '/auth/oauth/token'), ('GET', '/profile'),
                                     ('POST', '/api/ring'), ('POST', '/api/v2/ring')}
        if not recognized:
            return None
        headers = {k.casefold(): v for k, v in headers.items()}
        try:
            if not isinstance(body, bytes) or len(body) > self.MAX_BODY:
                raise BootstrapError('REQUEST TOO LARGE', 413)
            if path == '/profile':
                scheme, _, token = headers.get('authorization', '').partition(' ')
                if scheme.casefold() != 'bearer' or not token:
                    raise BootstrapError('INVALID ACCESS TOKEN', 401)
                return 200, self.profile(token)
            content_type = headers.get('content-type', '').split(';')[0].strip().casefold()
            if path == '/auth/oauth/token':
                if content_type != 'application/x-www-form-urlencoded':
                    raise BootstrapError('FORM CONTENT TYPE REQUIRED', 415)
                try:
                    fields = parse_qs(body.decode('utf-8'), keep_blank_values=True, strict_parsing=True,
                                      max_num_fields=20, encoding='utf-8', errors='strict')
                except (ValueError, UnicodeError):
                    raise BootstrapError('INVALID REQUEST', 400) from None
                if any(len(values) != 1 for values in fields.values()):
                    raise BootstrapError('DUPLICATE FORM FIELD', 400)
                return 200, self.oauth({k: v[0] for k, v in fields.items()}, headers.get('authorization', ''))
            if content_type != 'application/json':
                raise BootstrapError('JSON CONTENT TYPE REQUIRED', 415)
            try:
                data = json.loads(body)
            except (ValueError, UnicodeError):
                raise BootstrapError('INVALID DOORBELL REQUEST', 400) from None
            return 200, self.ring(data, path == '/api/v2/ring', headers.get('host'))
        except BootstrapError as exc:
            return exc.status, {'code': exc.status, 'error': str(exc), 'update_token': None}
