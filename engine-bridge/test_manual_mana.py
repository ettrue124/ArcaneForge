"""Regression for the recorded Arena freeze: tap Forest, then cast an Elf."""
import json
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from server.engine import Engine, EngineError

cards = json.loads((ROOT / 'data/catalogue.json').read_text(encoding='utf8'))
ids = {c['name']: c['grpId'] for c in cards}
deck = {'name': 'Manual mana regression', 'cards': [
    {'name': name, 'grpId': ids[name], 'quantity': count}
    for name, count in [('Forest', 30), ('Llanowar Elves', 30)]]}
opponent = {'name': 'Passive opponent', 'cards': [
    {'name': 'Forest', 'grpId': ids['Forest'], 'quantity': 60}]}
engine = Engine.from_config(Path(__file__).with_name('launch.json'))
activated = False
cast = False
floating_mana = False
source_preview = False
pool_preview = False
undo_requested = False
undo_verified = False
undo_source = None
try:
    engine.request('interactiveStart', decks=[deck, opponent])
    deadline = time.monotonic() + 90
    while time.monotonic() < deadline:
        poll = engine.request('interactivePoll')
        snapshot = engine.request('interactiveSnapshot')
        for option in snapshot.get('legalActions', []):
            if option['actionType'] == 1 and option['grpId'] == ids['Llanowar Elves']:
                source_preview |= bool(option.get('autoTapSources'))
                pool_preview |= bool(option.get('autoTapPool'))
        floating_mana |= any(p['seat'] == 1 and any(m['color'] == 5 and m['count'] > 0
            for m in p.get('manaPool', [])) for p in snapshot['players'])
        # No hidden ability picker may require a separate UI answer.
        assert not poll['pending'], f"Untranslated blocking prompt: {poll['pending']}"
        event = poll['buttonEventId']
        kind = snapshot.get('input', {}).get('type')
        if undo_requested and not undo_verified and kind == 'InputPassPriority' and poll['buttonEventId']:
            source = next(c for c in snapshot['cards'] if c['instanceId'] == undo_source)
            if not source['tapped'] and not next(p for p in snapshot['players'] if p['seat']==1)['manaPool']:
                undo_verified = True
                activated = False
                assert not snapshot['canUndo'], 'Undo history retained the undone tap'
                print('PASS: manual land tap undo untaps the source and removes its mana',flush=True)
        if cast and kind == 'InputPassPriority' and any(
                c['grpId'] == ids['Llanowar Elves'] and c['ownerSeat'] == 1
                for c in snapshot['cards'] if c.get('zone') == 'Battlefield'):
            assert activated and floating_mana, 'Mana activation or floating green mana missing'
            assert undo_verified, 'Manual land tap undo was not verified'
            assert source_preview and pool_preview, f'Authoritative mana payment previews missing: sources={source_preview}, pool={pool_preview}'
            print('PASS: mana source/pool previews, manual mana, Elf cast and priority restored')
            break
        if not event or kind == 'InputLockUI':
            time.sleep(.03)
            continue
        action = {'type': 'ok', 'eventId': event}
        options = [a for a in snapshot.get('legalActions', []) if a.get('highlight',3)==3]
        if kind == 'InputPassPriority':
            lands = [a for a in options if a['isLand']]
            mana = [a for a in options if a['actionType'] == 4]
            elves = [a for a in options if a['actionType'] == 1 and a['grpId'] == ids['Llanowar Elves']]
            if lands:
                action.update(type='playAbility', abilityId=lands[0]['abilityId'])
            elif mana and elves and not activated:
                action.update(type='playAbility', abilityId=mana[0]['abilityId'])
            elif activated and not undo_requested:
                assert snapshot['canUndo'], 'Manual mana ability did not offer native undo'
                action.update(type='undo')
            elif activated and undo_verified and elves and not cast:
                action.update(type='playAbility', abilityId=elves[0]['abilityId'])
        try:
            engine.request('interactiveAction', **action)
            if action['type']=='undo': undo_requested = True
            if kind == 'InputPassPriority' and action['type'] == 'playAbility':
                chosen = next(a for a in options if a['abilityId'] == action['abilityId'])
                activated |= chosen['actionType'] == 4
                if chosen['actionType'] == 4: undo_source = chosen['instanceId']
                cast |= chosen['actionType'] == 1
        except EngineError as error:
            if 'Stale human action' not in str(error):
                raise
        time.sleep(.05)
    else:
        raise AssertionError(f'Manual mana flow stalled: activated={activated}, cast={cast}, input={kind}')
finally:
    engine.close()
