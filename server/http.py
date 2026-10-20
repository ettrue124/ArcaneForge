"""Loopback control API; this is not Arena's FrontDoor protocol."""
import argparse
import json
import logging
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlsplit

from .storage import Store, StorageError
from .engine import Engine, EngineError, EngineRejected
from .bootstrap import Bootstrap
from .arena import Arena
from . import commander, bot_decks, feature_lab

ROOT = Path(__file__).resolve().parent.parent
MAX_BODY = 1024 * 1024


def client_version():
    try:
        value = json.loads((ROOT / 'client-inspection' / 'version').read_text(encoding='utf-8-sig'))
        versions = value.get('Versions', {})
        return next(iter(versions)) if len(versions) == 1 else None
    except (OSError, ValueError, TypeError):
        return None


def resolve_deck(store, catalogue, uid, deck_id):
    cards = []
    deck_name = "Custom Deck"
    format_name = 'Historic'
    command_cards, companions, sideboard = [], [], []
    with store._db(write=False) as db:
        if isinstance(deck_id, str) and len(deck_id) > 10:
            row = db.execute("SELECT * FROM arena_decks WHERE arena_id=? AND user_id=?", (deck_id, uid)).fetchone()
            if not row:
                row = db.execute("SELECT * FROM arena_decks WHERE arena_id=?", (deck_id,)).fetchone()
            if row:
                summary = json.loads(row["summary_json"])
                deck_name = summary.get("Name", "Arena Deck")
                body = json.loads(row["body_json"])
                format_name = commander.deck_format(summary)
                if format_name in commander.FORMATS:
                    commander.validate(body, catalogue, complete=True, format_name=format_name)
                command_cards = body.get('CommandZone', [])
                companions = body.get('Companions', [])
                sideboard = body.get('Sideboard', body.get('SideBoard', []))
                for item in body.get("MainDeck", []):
                    cards.append({"grpId": item["cardId"], "quantity": item["quantity"]})
        if not cards:
            try:
                numeric_id = int(deck_id)
                row = db.execute("SELECT * FROM decks WHERE id=? AND user_id=?", (numeric_id, uid)).fetchone()
                if not row:
                    row = db.execute("SELECT * FROM decks WHERE id=?", (numeric_id,)).fetchone()
                if row:
                    deck_name = row["name"]
                    cards = json.loads(row["cards_json"])
            except (ValueError, TypeError):
                pass
    if not cards:
        raise StorageError(f"Deck {deck_id} not found", 404)
    fallbacks = {280: "Plains", 281: "Island", 282: "Swamp", 283: "Mountain", 284: "Forest"}
    resolved_cards = []
    for c in cards:
        gid = c["grpId"]
        qty = c["quantity"]
        meta = catalogue.get(gid)
        name = meta.get("name") if meta else fallbacks.get(gid)
        if not name:
            name = fallbacks.get(gid)
        if not name:
            raise StorageError(f"Card {gid} not found in catalogue", 400)
        resolved_cards.append({"name": name, "quantity": qty, "grpId": gid})
    def resolve_pile(entries):
        return [{'name': catalogue[c['cardId']]['name'], 'quantity': c['quantity'], 'grpId': c['cardId']} for c in entries]
    return {"name": deck_name, "cards": resolved_cards, "format": format_name,
            "commanders": resolve_pile(command_cards), "companions": resolve_pile(companions),
            "sideboard": resolve_pile(sideboard)}


def match_decks(player_deck, ai_deck, mode=None, catalogue=None):
    """The selected queue controls seats; either Commander queue uses the same deck."""
    if mode not in (None, 'play', 'no-bans-60', 'commander', 'commander4', 'commander-unbanned'):
        raise StorageError('Unknown game mode')
    is_commander = player_deck['format'] in commander.FORMATS
    if mode == 'no-bans-60' or player_deck['format'] == 'NoBans60':
        if is_commander or sum(c['quantity'] for c in player_deck['cards']) != 60 or player_deck.get('commanders'):
            raise StorageError('Select a 60-card deck without a commander')
        player_deck = dict(player_deck, format='NoBans60')
        ai_deck = dict(ai_deck, format='NoBans60')
        if sum(c['quantity'] for c in player_deck.get('sideboard', [])) > 15:
            raise StorageError('The sideboard can contain at most 15 cards')
    if mode in ('commander', 'commander4', 'commander-unbanned') and not is_commander:
        raise StorageError('Select a 100-card Commander deck with a commander')
    if mode == 'play' and is_commander:
        raise StorageError('Select a Commander game mode for this deck')
    if is_commander and ai_deck['format'] not in commander.FORMATS:
        raise StorageError('The AI also needs a Commander deck')
    if mode == 'commander-unbanned' or player_deck['format'] == 'CommanderUnbanned':
        player_deck = dict(player_deck, format='CommanderUnbanned')
        ai_deck = dict(ai_deck, format='CommanderUnbanned')
    count = 4 if mode == 'commander4' or mode is None and player_deck['format'] == 'Commander4' else 2
    if count == 4 and is_commander and catalogue:
        bot_pool = bot_decks.get_multiple_random_bot_decks(catalogue, count=3, format_name='Commander', exclude_name=player_deck.get('name'))
        if len(bot_pool) >= 3:
            return [player_deck] + [dict(bot_pool[i], name=f'Forge AI {i+1}') for i in range(3)]
    return [player_deck] + [dict(ai_deck, name=f'Forge AI {i}' if count > 2 else 'Forge AI') for i in range(1, count)]


class Handler(BaseHTTPRequestHandler):
    server_version = "ArenaResearch/0.1"
    protocol_version = "HTTP/1.1"

    def log_message(self, fmt, *args):
        # Do not log paths/query strings: they can contain private information.
        logging.info("HTTP %s", args[1] if len(args) > 1 else "request")

    def send_json(self, status, data):
        # Drain bounded unread bodies before closing: Windows can reset a socket
        # with pending inbound data and discard an otherwise valid error reply.
        if not getattr(self, '_body_consumed', False) and not self.headers.get('Transfer-Encoding'):
            try:
                remaining = int(self.headers.get('Content-Length', '0'))
                if 0 < remaining <= MAX_BODY:
                    previous_timeout = self.connection.gettimeout()
                    try:
                        self.connection.settimeout(2)
                        self.rfile.read(remaining)
                    finally:
                        self.connection.settimeout(previous_timeout)
            except (ValueError, OSError):
                pass
            self._body_consumed = True
        payload = json.dumps(data, ensure_ascii=False).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(payload)))
        self.send_header("Connection", "keep-alive")
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.end_headers()
        self.wfile.write(payload)
        try:
            self.wfile.flush()
        except OSError:
            pass

    def body(self):
        try:
            size = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            raise StorageError("Invalid Content-Length", 400)
        if size < 0 or size > MAX_BODY:
            raise StorageError("Request too large", 413)
        if self.headers.get("Transfer-Encoding"):
            raise StorageError("Chunked requests are unsupported", 400)
        try:
            raw = self.rfile.read(size)
            self._body_consumed = True
            value = json.loads(raw)
        except (ValueError, UnicodeError):
            raise StorageError("Expected a JSON object", 400)
        if not isinstance(value, dict):
            raise StorageError("Expected a JSON object", 400)
        return value

    def token(self):
        scheme, _, value = self.headers.get("Authorization", "").partition(" ")
        if scheme.lower() != "bearer" or not value:
            raise StorageError("Sign in first", 401)
        return value

    def handle_request(self):
        self._body_consumed = False
        try:
            # Cross-origin scripts must not mutate this local server.
            origin = self.headers.get("Origin")
            allowed = {f"http://127.0.0.1:{self.server.server_port}",
                       f"http://localhost:{self.server.server_port}"}
            if origin and origin not in allowed:
                raise StorageError("Cross-origin requests are disabled", 403)
            parts = urlsplit(self.path)
            path = parts.path.rstrip("/") or "/"
            method = self.command
            store = self.server.store
            bootstrap = self.server.bootstrap
            if bootstrap and path in ('/auth/oauth/token', '/profile', '/api/ring', '/api/v2/ring'):
                try:
                    size = int(self.headers.get('Content-Length', '0'))
                except ValueError:
                    raise StorageError('Invalid Content-Length', 400)
                if size < 0 or size > bootstrap.MAX_BODY or self.headers.get('Transfer-Encoding'):
                    raise StorageError('Invalid bootstrap request size', 413)
                self.connection.settimeout(10)
                raw = self.rfile.read(size)
                self._body_consumed = True
                result = bootstrap.dispatch(method, path, self.headers, raw)
                if result is not None:
                    logging.info('Local client bootstrap: %s %s -> %s', method, path, result[0])
                    return self.send_json(*result)
            if method == "GET" and path == "/":
                payload = (ROOT / "server" / "ui.html").read_bytes()
                self.send_response(200)
                self.send_header("Content-Type", "text/html; charset=utf-8")
                self.send_header("Content-Length", str(len(payload)))
                self.send_header("X-Content-Type-Options", "nosniff")
                self.send_header("Content-Security-Policy", "default-src 'self'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self'; frame-ancestors 'none'")
                self.end_headers()
                self.wfile.write(payload)
                return
            if method == "GET" and path == "/api/status":
                return self.send_json(200, {
                    "service": "Arena research backend", "arenaClientCompatible": False,
                    "rulesEngine": "forge_configured" if self.server.engine else "not_integrated", "playable": False,
                    "clientVersion": client_version(),
                    "localClientBootstrap": "configured" if bootstrap else "disabled",
                    "features": ["local_accounts", "card_catalogue", "decks", "queue", "direct_challenges"],
                    "missing": ([] if bootstrap else ["Arena account/ring bootstrap"]) + ["complete FrontDoor command mapping",
                                "interactive GRE rules adapter", "stock client integration verification"]})
            if method == "POST" and path in ("/api/register", "/api/login"):
                data = self.body()
                fn = store.register if path.endswith("register") else store.login
                result = fn(data.get("username", ""), data.get("password", ""))
                return self.send_json(201 if path.endswith("register") else 200, result)
            if path.startswith("/api/interactive/") or path.startswith("/api/bot/"):
                if not self.server.engine:
                    raise StorageError('Forge is not configured', 503)
                token = None
                try:
                    token = self.token()
                except StorageError:
                    pass
                user = store.authenticate(token) if token else None
                uid = user["id"] if user else 1
                if path == '/api/interactive/feature-lab':
                    if method == 'POST':
                        selected = self.body().get('scenario')
                        if selected is not None:
                            feature_lab.prepare(selected, self.server.catalogue)
                        self.server.feature_scenario = selected
                    elif method != 'GET':
                        raise StorageError('Method not allowed', 405)
                    return self.send_json(200, dict(scenarios=feature_lab.listing(), selected=self.server.feature_scenario))
                if path == "/api/interactive/start":
                    data = self.body()
                    decks = data.get("decks")
                    scenario = None
                    if self.server.feature_scenario:
                        decks, scenario = feature_lab.prepare(self.server.feature_scenario, self.server.catalogue)
                    if not decks:
                        deck_id = data.get("deckId")
                        if not deck_id:
                            raise StorageError("deckId or decks required", 400)
                        player_deck = resolve_deck(store, self.server.catalogue, uid, deck_id)
                        bot_deck_id = data.get("botDeckId")
                        is_cmdr = player_deck['format'] in commander.FORMATS or data.get('mode') in ('commander', 'commander4', 'commander-unbanned')
                        if bot_deck_id:
                            ai_deck = resolve_deck(store, self.server.catalogue, uid, bot_deck_id)
                        elif is_cmdr:
                            ai_deck = bot_decks.get_random_bot_deck(self.server.catalogue, format_name='Commander', exclude_name=player_deck.get('name'))
                            if not ai_deck:
                                ai_deck = dict(player_deck, name="Forge AI")
                        else:
                            ai_deck = bot_decks.get_random_bot_deck(self.server.catalogue,
                                format_name='NoBans60' if data.get('mode') == 'no-bans-60' else player_deck['format'],
                                exclude_name=player_deck.get('name'), exclude_deck=player_deck)
                            if not ai_deck:
                                raise StorageError('No complete bot deck is available for this mode')
                        decks = match_decks(player_deck, ai_deck, data.get('mode'), catalogue=self.server.catalogue)
                    try:
                        self.server.engine.request('interactiveClose')
                    except Exception:
                        pass
                    res = self.server.engine.request('interactiveStart', decks=decks, **({'featureScenario': scenario} if scenario else {}))
                    if scenario:
                        res['featureScenario'] = scenario['id']
                        res['deckCards'] = [c['grpId'] for c in decks[0]['cards'] for _ in range(c['quantity'])]
                    return self.send_json(200, res)
                if path == "/api/interactive/snapshot":
                    body = self.body() if method == 'POST' else {}
                    res = self.server.engine.request('interactiveSnapshot', **body)
                    return self.send_json(200, res)
                if path == "/api/interactive/action":
                    body = self.body()
                    res = self.server.engine.request('interactiveAction', **body)
                    return self.send_json(200, res)
                if path == "/api/interactive/answer":
                    body = self.body()
                    res = self.server.engine.request('interactiveAnswer', **body)
                    return self.send_json(200, res)
                if path == "/api/interactive/poll":
                    res = self.server.engine.request('interactivePoll')
                    return self.send_json(200, res)
                if path == "/api/interactive/close":
                    try:
                        res = self.server.engine.request('interactiveClose')
                    except Exception:
                        res = {"closed": True}
                    return self.send_json(200, res)
                if path == "/api/interactive/status":
                    res = self.server.engine.request('status')
                    return self.send_json(200, res)
                if path == "/api/bot/decks":
                    bot_list = bot_decks.load_bot_commander_decks(self.server.catalogue)
                    return self.send_json(200, {
                        "decks": [
                            {
                                "name": d["name"],
                                "format": d["format"],
                                "commander": d["commanders"][0]["name"] if d.get("commanders") else "",
                                "cardCount": sum(c["quantity"] for c in d["cards"]) + len(d.get("commanders", []))
                            }
                            for d in bot_list
                        ]
                    })
                if path == "/api/bot/decks/import":
                    body = self.body()
                    url = body.get("url")
                    text = body.get("text")
                    name = body.get("name")
                    if url:
                        imported = bot_decks.fetch_deck_from_url(url, self.server.catalogue, deck_name=name, save=True)
                    elif text:
                        imported = bot_decks.import_deck_text(text, self.server.catalogue, deck_name=name or "Imported Online Deck", save=True)
                    else:
                        raise StorageError("url or text required", 400)
                    return self.send_json(200, {"success": True, "deck": imported})
            user = store.authenticate(self.token())
            uid = user["id"]
            if method == 'POST' and path == '/api/arena/command':
                return self.send_json(200, self.server.arena.dispatch(self.body(), self.token()))
            if method == 'GET' and path == '/api/engine/status':
                if not self.server.engine:
                    raise StorageError('Run Setup-Forge.ps1 to configure Forge', 503)
                return self.send_json(200, self.server.engine.status())
            if method == 'POST' and path == '/api/engine/cards':
                if not self.server.engine:
                    raise StorageError('Forge is not configured', 503)
                names = self.body().get('names')
                if not isinstance(names, list) or len(names) > 1000 or any(not isinstance(x, str) or len(x) > 300 for x in names):
                    raise StorageError('Provide at most 1000 card names')
                return self.send_json(200, self.server.engine.cards(names=names))
            if method == 'POST' and path.startswith('/api/matches/') and path.endswith('/simulate'):
                if not self.server.engine:
                    raise StorageError('Forge is not configured', 503)
                match = store.get_match(uid, path[len('/api/matches/'):-len('/simulate')])
                data = self.body()
                seconds = data.get('maxSeconds', 30)
                seed = data.get('seed', 42)
                if type(seconds) is not int or not 5 <= seconds <= 60 or type(seed) is not int or not -(2**63) <= seed < 2**63:
                    raise StorageError('maxSeconds must be 5..60; seed must be a signed 64-bit integer')
                decks = []
                for deck in match['decks']:
                    cards = []
                    for card in deck['cards']:
                        metadata = self.server.catalogue.get(card['grpId'])
                        if not metadata:
                            raise StorageError('Catalogue metadata missing for card ' + str(card['grpId']))
                        raw = metadata.get('arenaMetadata', {})
                        if raw.get('IsDigitalOnly') or raw.get('IsRebalanced') or metadata['name'].startswith('A-'):
                            raise StorageError('Arena digital/rebalanced card requires an explicit Forge mapping: ' + metadata['name'])
                        cards.append({'name': metadata['name'], 'quantity': card['quantity'], 'grpId': card['grpId']})
                    decks.append({'name': deck['name'], 'cards': cards})
                seat = match['players'].index(uid) + 1
                result = self.server.engine.simulate(decks=decks, seed=seed, maxSeconds=seconds,
                    matchId=str(match['id']), viewerSeat=seat, timeout=seconds + 180)
                # Internal engine snapshots are not sent to callers without per-seat filtering.
                for key in ('snapshot', 'initialSnapshot'):
                    if result.get(key):
                        result[key] = mask_snapshot(result[key], seat)
                result['arenaPlayable'] = False
                result['note'] = 'AI rules simulation; this does not start a player-controlled Arena match.'
                return self.send_json(200, result)
            if method == "GET" and path == "/api/me":
                return self.send_json(200, {**user, **(bootstrap.identity(user) if bootstrap else {})})
            if method == "GET" and path == "/api/users":
                qs = parse_qs(parts.query)
                return self.send_json(200, store.list_users(qs.get("q", [""])[0]))
            if method == "POST" and path == "/api/logout":
                (bootstrap.revoke if bootstrap else store.logout)(self.token())
                return self.send_json(200, {"ok": True})
            if method == "GET" and path == "/api/cards":
                qs = parse_qs(parts.query)
                try:
                    limit = int(qs.get("limit", ["100"])[0])
                except ValueError:
                    raise StorageError("Invalid limit", 400)
                return self.send_json(200, store.list_cards(qs.get("q", [""])[0], limit))
            if path == "/api/decks":
                if method == "GET":
                    return self.send_json(200, store.list_decks(uid))
                if method == "POST":
                    data = self.body()
                    return self.send_json(201, store.save_deck(uid, data.get("name", ""), data.get("cards", [])))
            if path.startswith("/api/decks/"):
                deck_id = path.removeprefix("/api/decks/")
                if method == "GET":
                    return self.send_json(200, store.get_deck(uid, deck_id))
                if method == "PUT":
                    data = self.body()
                    return self.send_json(200, store.save_deck(uid, data.get("name", ""), data.get("cards", []), deck_id))
                if method == "DELETE":
                    store.delete_deck(uid, deck_id)
                    return self.send_json(200, {"ok": True})
            if path == "/api/queue":
                if method == "POST":
                    return self.send_json(200, store.queue(uid, self.body().get("deckId")))
                if method == "DELETE":
                    store.cancel_queue(uid)
                    return self.send_json(200, {"ok": True})
            if path == "/api/challenges":
                if method == "GET":
                    return self.send_json(200, store.list_challenges(uid))
                if method == "POST":
                    data = self.body()
                    return self.send_json(201, store.challenge(uid, data.get("opponentId"), data.get("deckId")))
            if method == 'DELETE' and path.startswith('/api/challenges/'):
                store.cancel_challenge(uid, path.removeprefix('/api/challenges/'))
                return self.send_json(200, {'ok': True})
            if method == "POST" and path.startswith("/api/challenges/") and path.endswith("/accept"):
                challenge_id = path[len("/api/challenges/"):-len("/accept")]
                return self.send_json(200, store.accept_challenge(uid, challenge_id, self.body().get("deckId")))
            if method == "GET" and path == "/api/matches":
                return self.send_json(200, store.list_matches(uid))
            if method == "GET" and path.startswith("/api/matches/"):
                return self.send_json(200, store.get_match(uid, path.removeprefix("/api/matches/")))
            raise StorageError("Endpoint not found", 404)
        except StorageError as exc:
            self.send_json(exc.status, {"error": str(exc)})
        except EngineRejected as exc:
            self.send_json(400, {'error': str(exc)})
        except EngineError as exc:
            self.send_json(503, {'error': str(exc)})
        except (TypeError, ValueError, KeyError):
            self.send_json(400, {"error": "Invalid request fields"})
        except Exception:
            logging.exception("Request failed")
            self.send_json(500, {"error": "Internal server error"})

    do_GET = handle_request
    do_POST = handle_request
    do_PUT = handle_request
    do_DELETE = handle_request


def mask_snapshot(snapshot, viewer_seat):
    value = json.loads(json.dumps(snapshot))
    value['viewerSeat'] = viewer_seat
    for card in value.get('cards', []):
        zone = card.get('zone', '').lower()
        private = zone == 'library' or (zone in ('hand', 'sideboard') and card.get('ownerSeat') != viewer_seat)
        if private:
            for key in ('grpId', 'originalGrpId', 'cardState', 'power', 'toughness', 'name', 'manaCost', 'cardTypes', 'colors'):
                card.pop(key, None)
    return value


def make_server(db_path, port=8787, engine=None):
    server = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    server.store = Store(db_path)
    server.engine = engine
    server.catalogue = {}
    server.feature_scenario = None
    server.bootstrap = None
    server.arena = Arena(server.store)
    return server


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8787)
    parser.add_argument("--database", type=Path, default=ROOT / "data" / "server.sqlite3")
    parser.add_argument("--catalogue", type=Path, default=ROOT / "data" / "catalogue.json")
    parser.add_argument('--engine-config', type=Path, default=ROOT / 'engine-bridge' / 'launch.json')
    parser.add_argument('--bootstrap-config', type=Path, default=ROOT / 'data' / 'local-client' / 'bootstrap.json')
    parser.add_argument('--warm-engine', action='store_true', help='Load rules while the local services start')
    args = parser.parse_args()
    logging.basicConfig(level=logging.INFO, format="%(message)s")
    args.database.parent.mkdir(parents=True, exist_ok=True)
    engine = Engine.from_config(args.engine_config) if args.engine_config.exists() else None
    if args.warm_engine and engine:
        def warm_engine():
            try:
                engine.status(timeout=180)
            except Exception as error:
                logging.error('Rules startup failed: %s', error)
        threading.Thread(target=warm_engine, name='rules-startup', daemon=True).start()
    server = make_server(args.database, args.port, engine)
    if args.bootstrap_config.exists():
        server.bootstrap = Bootstrap(server.store, **json.loads(args.bootstrap_config.read_text(encoding='utf-8')))
        logging.info('Local Arena WAS login/profile and doorbell routes enabled')
    if args.catalogue.exists():
        cards = json.loads(args.catalogue.read_text(encoding="utf-8"))
        server.store.import_cards(cards, replace=True)
        server.catalogue = {card['grpId']: card for card in cards}
        server.arena = Arena(server.store, cards)
        logging.info("Loaded %d catalogue entries", len(cards))
    logging.info("Local control panel: http://127.0.0.1:%d", server.server_port)
    logging.info('Arena frontend startup and interactive gameplay remain incomplete. Local login routes: %s.', 'enabled' if server.bootstrap else 'disabled')
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
        if engine:
            engine.close()


if __name__ == '__main__':
    main()

