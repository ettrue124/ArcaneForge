"""Format/event definitions and fail-closed deck legality from a dated rules snapshot."""
from collections import Counter
from functools import lru_cache
import json
from pathlib import Path
from .storage import StorageError

ROOT = Path(__file__).resolve().parents[1]
CONSTRUCTED = ('Standard', 'Alchemy', 'Explorer', 'Pioneer', 'Historic', 'Timeless',
               'Modern', 'Legacy', 'Vintage', 'Pauper')
COMMAND = ('Brawl', 'StandardBrawl', 'Commander')


def legality_key(spec):
    return {'Explorer':'pioneer','StandardBrawl':'standardbrawl','Brawl':'brawl'}.get(spec['base'],spec['base'].lower())


@lru_cache(maxsize=1)
def commander_names():
    return set(json.loads((ROOT/'data/commander-eligible.json').read_text(encoding='utf8'))['names'])


def definition(name):
    unbanned = name.endswith('Unbanned') or name == 'NoBans60'
    base = name.removesuffix('Unbanned')
    if name == 'NoBans60':
        base = name
    if base not in CONSTRUCTED + COMMAND + ('NoBans60',):
        raise StorageError('Unknown format: '+str(name))
    return {'name': name, 'base': base, 'unbanned': unbanned,
            'commander': base in COMMAND,
            'deckSize': 60 if base not in ('Brawl', 'Commander') else 100,
            'startingLife': 40 if base == 'Commander' else 25 if base in COMMAND else 20,
            'sideboardMax': 0 if base in COMMAND else 15}


def events():
    names = [*CONSTRUCTED, *COMMAND, 'NoBans60']
    names += [name+'Unbanned' for name in CONSTRUCTED+COMMAND]
    return [{**definition(name), 'id': name+('AI' if bot else 'PvP'),
             'opponent': 'bot' if bot else 'human'} for name in names for bot in (True, False)]


def event(event_id):
    result = next((value for value in events() if value['id'] == event_id), None)
    if not result:
        raise StorageError('Unknown event')
    return result


@lru_cache(maxsize=1)
def policies():
    path = ROOT/'data/format-policies.json'
    if not path.exists():
        raise StorageError('Format legality snapshot has not been generated', 503)
    return json.loads(path.read_text(encoding='utf8'))


@lru_cache(maxsize=1)
def missing_rules():
    path=ROOT/'data/forge-coverage.json'
    if not path.exists():raise StorageError('Rules coverage inventory is missing',503)
    return {row['grpId'] for row in json.loads(path.read_text(encoding='utf8'))['cards'] if not row['nameResolved']}


def validate(deck, name):
    spec = definition(name)
    cards = deck.get('cards', [])
    commanders = deck.get('commanders', [])
    sideboard = deck.get('sideboard', [])
    total = sum(c['quantity'] for c in cards+commanders)
    if spec['commander']:
        allowed=(1,2) if spec['base']=='Commander' else (1,)
        if len(commanders) not in allowed or any(card['quantity']!=1 for card in commanders) or total != spec['deckSize']:
            raise StorageError(name+' requires exactly '+str(spec['deckSize'])+' cards including its commander or legal commander pair')
    elif commanders or total < 60 or name == 'NoBans60' and total != 60:
        raise StorageError('This event requires a 60-card constructed deck without commanders')
    if sum(c['quantity'] for c in sideboard) > spec['sideboardMax']:
        raise StorageError('Too many sideboard cards for '+name)
    table = policies()['cards']
    counts = Counter()
    identity = set()
    if commanders:
        for commander in commanders:
            leader = table.get(commander['name'])
            if not leader:
                raise StorageError('Commander rules metadata is missing')
            types = leader['typeLine']
            if not ('Legendary' in types and ('Creature' in types or spec['base'] != 'Commander' and 'Planeswalker' in types)
                    or leader.get('canBeCommander') or spec['base']=='Commander' and commander['name'] in commander_names()):
                raise StorageError('This card cannot be the commander')
            identity.update(leader['colorIdentity'])
    key = legality_key(spec)
    for card in cards+commanders+sideboard:
        if card.get('grpId') in missing_rules():
            raise StorageError('Rules are not implemented for '+card['name'])
        policy = table.get(card['name'])
        if not policy:
            raise StorageError('Missing legality metadata for '+card['name'])
        status = policy['legalities'].get(key, 'not_legal')
        if spec['base'] != 'NoBans60' and status not in ('legal', 'restricted') and not (spec['unbanned'] and status == 'banned'):
            raise StorageError(card['name']+' is not legal in '+name)
        canonical=policy.get('name',card['name'])
        counts[canonical] += card['quantity']
        basic = 'Basic' in policy['typeLine'] and 'Land' in policy['typeLine']
        limit = 10000 if basic else policy.get('copyLimit', 1 if spec['commander'] else 4)
        if status == 'restricted' and not spec['unbanned']:
            limit = 1
        if counts[canonical] > limit:
            raise StorageError('Copy limit exceeded: '+card['name'])
        if spec['commander'] and not set(policy['colorIdentity']) <= identity:
            raise StorageError(card['name']+' is outside the commander color identity')
    return spec


def bot_deck(catalogue, name, exclude=None):
    """Generate a legal starter opponent when no curated list fits this event."""
    import random
    spec=definition(name)
    table=policies()['cards']
    key=legality_key(spec)
    covered=json.loads((ROOT/'data/forge-coverage.json').read_text(encoding='utf8'))
    executable={c['grpId'] for c in covered['cards'] if c['eligibleForForgeSimulation']}
    available={}
    for card in catalogue.values():
        policy=table.get(card['name'])
        if card['grpId'] not in executable or not policy:continue
        status=policy['legalities'].get(key,'not_legal')
        if spec['base']!='NoBans60' and status!='legal' and not(spec['unbanned'] and status=='banned'):continue
        available.setdefault(card['name'],card)
    candidates=[]
    for color,land in [('W','Plains'),('U','Island'),('B','Swamp'),('R','Mountain'),('G','Forest')]:
        if land not in available:continue
        pool=[card for card in available.values() if set(table[card['name']]['colorIdentity'])<=set(color)
              and 'Creature' in table[card['name']]['typeLine'] and 'Basic' not in table[card['name']]['typeLine']]
        random.shuffle(pool)
        leaders=[card for card in pool if 'Legendary' in table[card['name']]['typeLine']]
        if spec['commander'] and not leaders:continue
        leader=leaders[0] if spec['commander'] else None
        pool=[c for c in pool if not leader or c['name']!=leader['name']]
        land_count=36 if spec['deckSize']==100 else 24
        count=spec['deckSize']-land_count-(1 if leader else 0)
        required=count if spec['commander'] else (count+3)//4
        if len(pool)<required:continue
        def entry(card,quantity):return {'name':card['name'],'grpId':card['grpId'],'quantity':quantity}
        cards=[entry(available[land],land_count)]
        for card in pool[:required]:
            amount=1 if spec['commander'] else min(4,count)
            cards.append(entry(card,amount));count-=amount
        deck={'name':name+' '+color+' starter','format':name,'cards':cards,
              'commanders':[entry(leader,1)] if leader else [],'sideboard':[],'companions':[]}
        validate(deck,name)
        candidates.append(deck)
    if not candidates:raise StorageError('No complete legal bot starter is available for '+name,503)
    def signature(deck):
        return sorted((c['name'],c['quantity']) for c in deck.get('cards',[])),sorted((c['name'],c['quantity']) for c in deck.get('commanders',[]))
    distinct=[deck for deck in candidates if not exclude or signature(deck)!=signature(exclude)]
    if distinct:return random.choice(distinct)
    deck=random.choice(candidates)
    deck['cards'][0]['quantity']+=1
    deck['cards'][1]['quantity']-=1
    deck['cards']=[card for card in deck['cards'] if card['quantity']]
    validate(deck,name)
    return deck
