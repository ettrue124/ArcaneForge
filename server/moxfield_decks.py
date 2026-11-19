"""Bounded public Moxfield discovery and strict local deck resolution."""
import json
import hashlib
import logging
import random
import re
import threading
import time
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode, urlsplit
from urllib.request import Request, build_opener, HTTPRedirectHandler
from . import formats, commander
from .storage import StorageError

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT/'data/moxfield-bot-decks'
LOCK = threading.Lock()
LAST_REQUEST = 0.0
BACKOFF_UNTIL = 0.0


class PublicRedirects(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        if urlsplit(newurl).scheme != 'https' or urlsplit(newurl).hostname != 'api2.moxfield.com':
            raise ValueError('Unexpected Moxfield redirect')
        return super().redirect_request(req, fp, code, msg, headers, newurl)


def fetch(path, deadline):
    global LAST_REQUEST, BACKOFF_UNTIL
    if time.monotonic() >= deadline or time.time() < BACKOFF_UNTIL:
        raise TimeoutError('Moxfield request budget exhausted')
    with LOCK:
        delay = max(0, 1.1-(time.monotonic()-LAST_REQUEST))
        if time.monotonic()+delay >= deadline: raise TimeoutError('Moxfield request budget exhausted')
        if delay: time.sleep(delay)
        LAST_REQUEST = time.monotonic()
        req = Request('https://api2.moxfield.com'+path, headers={
            'User-Agent': 'ArcaneForge/1.0 (public deck selection)', 'Accept': 'application/json'})
        try:
            with build_opener(PublicRedirects()).open(req, timeout=min(6, max(.2, deadline-time.monotonic()))) as response:
                payload = response.read(3_000_001)
            if len(payload)>3_000_000: raise ValueError('Moxfield deck response too large')
            return json.loads(payload)
        except HTTPError as error:
            if error.code in (403, 429): BACKOFF_UNTIL = time.time()+900
            raise


def signature(deck):
    return tuple(tuple(sorted((c['name'], c['quantity']) for c in deck.get(pile, [])))
                 for pile in ('cards', 'commanders'))


def resolve(payload, catalogue, format_name):
    if not re.fullmatch(r'[A-Za-z0-9_-]{10,50}', payload.get('publicId', '')):
        raise StorageError('Invalid public Moxfield identity')
    spec = formats.definition(format_name)
    expected = {'Explorer':'pioneer', 'StandardBrawl':'brawl', 'Brawl':'historicbrawl',
                'NoBans60':'modern'}.get(spec['base'], spec['base'].lower())
    if spec['base']=='Commander' and payload['publicId'] not in cedh_ids():
        raise StorageError('Deck is not in the vetted competitive cEDH pool')
    if payload.get('visibility') != 'public' or payload.get('format') != expected:
        raise StorageError('Public deck has a different format')
    by_name = {}
    for card in catalogue.values():
        if card.get('arenaMetadata', {}).get('IsToken'): continue
        by_name.setdefault(card['name'], card)
    coverage = json.loads((ROOT/'data/forge-coverage.json').read_text(encoding='utf-8'))
    supported = {c['grpId'] for c in coverage['cards'] if c.get('eligibleForForgeSimulation')}
    boards = payload.get('boards', {})
    def pile(name):
        counts = {}
        for item in boards.get(name, {}).get('cards', {}).values():
            title = item['card']['name']
            card = by_name.get(title)
            # Moxfield stores a combined Oracle title for linked-face cards;
            # the installed catalogue's deck identity is the front-face name.
            if card is None and ' // ' in title: card = by_name.get(title.split(' // ')[0])
            quantity = item['quantity']
            if not card or card['grpId'] not in supported:
                raise StorageError('Unavailable installed rules: '+title)
            if type(quantity) is not int or not 1 <= quantity <= 1000:
                raise StorageError('Invalid deck quantity')
            gid = card['grpId']
            if gid not in counts: counts[gid] = dict(name=card['name'], grpId=gid, quantity=0)
            counts[gid]['quantity'] += quantity
        return list(counts.values())
    deck = dict(name='Moxfield: '+str(payload.get('name', 'Public deck'))[:150], format=format_name,
                cards=pile('mainboard'), commanders=pile('commanders'), companions=pile('companions'),
                sideboard=[] if spec['commander'] else pile('sideboard'),
                source='moxfield', sourceUrl='https://moxfield.com/decks/'+payload['publicId'])
    formats.validate(deck, format_name)
    if spec['base']=='Commander':
        def entries(values): return [dict(cardId=c['grpId'], quantity=c['quantity']) for c in values]
        commander.validate(dict(MainDeck=entries(deck['cards']), CommandZone=entries(deck['commanders']),
            Sideboard=entries(deck['companions']), Companions=entries(deck['companions'])), catalogue,
            complete=True, format_name='CommanderUnbanned' if spec['unbanned'] else 'Commander')
    return deck


def cedh_ids():
    pool=json.loads((ROOT/'data/cedh-bot-pool.json').read_text(encoding='utf-8'))
    return {d['publicId'] for d in pool['decks']}


HISTORY_LOCK = threading.Lock()
HISTORY_PATH = ROOT/'data/bot-deck-history.json'

def deck_key(deck):
    return hashlib.sha256(repr(signature(deck)).encode('utf-8')).hexdigest()

def choose_varied(decks, format_name):
    if not decks:
        return None
    with HISTORY_LOCK:
        try:
            history = json.loads(HISTORY_PATH.read_text(encoding='utf-8'))
        except (OSError, ValueError):
            history = {}
        used = history.get(format_name, [])
        # Shuffle unseen decks; once the pool is exhausted, begin a new cycle
        # without immediately repeating the last deck when alternatives exist.
        choices = [d for d in decks if deck_key(d) not in used]
        if not choices:
            choices = [d for d in decks if not used or deck_key(d) != used[-1]] or decks
            used = used[-1:]
        deck = random.choice(choices)
        history[format_name] = (used + [deck_key(deck)])[-512:]
        temporary = HISTORY_PATH.with_suffix('.tmp')
        temporary.write_text(json.dumps(history), encoding='utf-8')
        temporary.replace(HISTORY_PATH)
        return deck


def random_deck(catalogue, format_name, exclude=None, exclude_signatures=()):
    """Try randomized public lists; never trim or substitute missing cards."""
    spec = formats.definition(format_name)
    fmt = {'Explorer':'pioneer','StandardBrawl':'brawl','Brawl':'historicbrawl',
           'NoBans60':'modern'}.get(spec['base'],spec['base'].lower())
    deadline = time.monotonic()+22
    candidates = []
    CACHE.mkdir(parents=True, exist_ok=True)
    # Match entry should not wait on external discovery when a complete local
    # Moxfield list is available. Revalidate against the current format, rules
    # coverage and cEDH pool before admitting each saved list.
    paths = list(CACHE.glob('*.json'))
    random.shuffle(paths)
    for path in paths:
        try:
            deck = resolve(json.loads(path.read_text(encoding='utf-8')), catalogue, format_name)
            if (exclude and signature(deck) == signature(exclude)) or signature(deck) in exclude_signatures:
                continue
            candidates.append(deck)
        except (OSError, ValueError, KeyError, TypeError, AttributeError, StorageError):
            continue
    if candidates:
        deck = choose_varied(candidates, format_name)
        logging.info('Selected validated varied cached Moxfield deck: %s', deck['sourceUrl'])
        return deck
    try:
        if spec['base']=='Commander':
            options=[{'publicId':i,'visibility':'public'} for i in cedh_ids()]
        else:
            query = urlencode(dict(pageNumber=random.randint(1, 100), pageSize=16,
                sortType='updated', sortDirection='descending', fmt=fmt))
            search = fetch('/v2/decks/search?'+query, deadline)
            options = list(search.get('data', []))
        random.shuffle(options)
        for row in options[:6]:
            public_id = row.get('publicId', '')
            if not re.fullmatch(r'[A-Za-z0-9_-]{10,50}', public_id): continue
            if row.get('visibility') != 'public': continue
            try:
                payload = fetch('/v3/decks/all/'+public_id, deadline)
                deck = resolve(payload, catalogue, format_name)
                if (exclude and signature(deck)==signature(exclude)) or signature(deck) in exclude_signatures: continue
                path = CACHE/(public_id+'.json')
                temporary = path.with_suffix('.tmp')
                temporary.write_text(json.dumps(payload, ensure_ascii=False), encoding='utf-8')
                temporary.replace(path)
                logging.info('Selected validated public Moxfield deck: %s', deck['sourceUrl'])
                return choose_varied([deck], format_name)
            except HTTPError as error:
                if error.code in (403, 429): raise
                continue
            except StorageError: continue
            except (KeyError, TypeError, ValueError, AttributeError): continue
    except (OSError, ValueError, TimeoutError, URLError, AttributeError, TypeError):
        logging.info('Moxfield discovery unavailable; checking validated cached lists')
    paths = list(CACHE.glob('*.json')); random.shuffle(paths)
    for path in paths[:100]:
        try:
            deck = resolve(json.loads(path.read_text(encoding='utf-8')), catalogue, format_name)
            if (exclude and signature(deck)==signature(exclude)) or signature(deck) in exclude_signatures: continue
            candidates.append(deck)
        except (OSError, ValueError, KeyError, TypeError, AttributeError, StorageError): continue
    return choose_varied(candidates, format_name)
