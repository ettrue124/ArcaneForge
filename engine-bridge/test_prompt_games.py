"""Real Forge decisions translated to installed Arena requests, without a GUI."""
import json
from pathlib import Path
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from server.engine import Engine, EngineError

IDS = {c['name']: c['grpId'] for c in json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'))}

def deck(entries):
    return {'name': 'Native decision regression', 'cards': [
        {'name': n, 'grpId': IDS[n], 'quantity': q} for n, q in entries]}

engine = Engine.from_config(Path(__file__).with_name('launch.json'))

def exercise(name, ours, theirs, required, limit=150):
    native = subprocess.Popen(['dotnet', str(ROOT/'protocol-host/bin/TargetAudit/ProtocolHost.dll'), '--flow-probe'],
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True, encoding='utf8', cwd=ROOT)
    seen = set()
    grouped = None
    try:
        engine.request('interactiveStart', decks=[deck(ours), deck(theirs)])
        deadline = time.monotonic()+limit
        cursor = 0
        while time.monotonic()<deadline:
            engine.request('interactivePoll')
            s = engine.request('interactiveSnapshot', afterPlaybackId=cursor)
            cursor = s['playbackId']
            native.stdin.write(json.dumps(s)+'\n'); native.stdin.flush()
            p = json.loads(native.stdout.readline())
            d = s['input']; kind = d['type']; event = s['buttonEventId']
            if grouped is not None and kind!='InputGuiGroups':
                library=[c['instanceId'] for c in s['cards'] if c['zone']=='Library' and c['ownerSeat']==1]
                if grouped[0] in library and grouped[1] in library:
                    assert library.index(grouped[0])<library.index(grouped[1]), 'Scry top/bottom order reversed'
                    assert library[-1]==grouped[1], 'Scry bottom card did not reach library bottom'
                    seen.add('library order');grouped=None
            if required <= seen:
                print('PASS:', name, ', '.join(sorted(seen)), flush=True)
                return
            if not event or kind in ('', 'InputLockUI'):
                time.sleep(.02); continue
            if kind.startswith('InputGui'):
                assert not p['shouldPass'], 'A blocking Forge choice was auto-passed'
                if kind == 'InputGuiChoice':
                    if p['requestType']=='CastingTimeOptionsReq':
                        assert d.get('nativeKind')=='modes' and d.get('modeDefinitions'), 'Native modal request lost its definitions'
                    elif p['requestType']=='SearchReq':
                        assert d.get('nativeKind')=='search' and d.get('choices'), 'Native search request lost its candidates'
                    else:
                        assert p['choiceIds'] == [o['id'] for o in d['options']]
                    if not d['entityChoices'] and p['requestType']=='SelectNReq':
                        assert p['choiceLabels'] == [o['label'] for o in d['options']]
                    if d['method'] == 'order':
                        answer=[]; seen.add('optional scry choice')
                    elif d['method'] == 'getChoices' and any('Form a Party' in o['label'] for o in d['options']):
                        answer=[next(o['id'] for o in d['options'] if 'Form a Party' in o['label'])]
                        seen.add('modal choice')
                    else:
                        answer=[o['id'] for o in d['options'][:max(d['min'], min(1, d['max']))]]
                        if d['entityChoices']: seen.add('card selection')
                elif kind == 'InputGuiOrder':
                    assert p['orderIds'] == [o['id'] for o in d['options']]
                    answer=list(reversed(p['orderIds'])); seen.add('library order')
                elif kind == 'InputGuiGroups':
                    assert p['requestType']=='GroupReq' and len(d['choices'])>=2
                    answer=[{'subZoneType':1,'ids':d['choices'][:-1]}, {'subZoneType':2,'ids':d['choices'][-1:]}]
                    grouped=(d['choices'][0],d['choices'][-1]);seen.add('optional scry choice')
                elif kind == 'InputGuiCombatDamage':
                    assert p['damageAssigners'][0]['total'] == d['amount']
                    answer=[{'id': a['id'], 'amount': a['amount']} for a in p['damageAssigners'][0]['assignments']]
                    try:
                        engine.request('interactiveAnswer', eventId=event, answer=[])
                        raise AssertionError('Unassigned combat damage accepted')
                    except EngineError as error:
                        assert 'must be assigned' in str(error)
                    seen.add('combat damage allocation')
                elif kind == 'InputGuiDistribution':
                    assert p['requestType']=='DistributionReq'
                    allocations=[d['minPerTarget'] for _ in d['targets']]
                    allocations[0]+=d['amount']-sum(allocations)
                    answer=[{'id': t['id'], 'amount': n} for t,n in zip(d['targets'],allocations)]
                    seen.add('divided damage amount')
                else:
                    raise AssertionError('Unexpected GUI decision '+str(d))
                engine.request('interactiveAnswer', eventId=event, answer=answer)
                time.sleep(.02); continue
            if kind == 'InputChooseNumber':
                assert p['numberRequest'] and not p['shouldPass']
                amount=d['max']
                engine.request('interactiveAnswer', eventId=event, answer=amount)
                if not d['isX']:
                    assert p['requestType']=='NumericInputReq'
                    seen.add('divided damage amount')
                time.sleep(.02); continue
            action={'type': 'ok', 'eventId': event}
            if kind == 'InputPassPriority':
                candidates=[a for a in s['legalActions'] if a.get('highlight',3)==3 and (a['isLand'] or a['actionType'] == 1)]
                candidates.sort(key=lambda a: not a['isLand'])
                if candidates:
                    action.update(type='playAbility', abilityId=candidates[0]['abilityId'])
            elif kind == 'InputAttack':
                assignments=[]
                if name=='trample':
                    assignments=[{'instanceId': a['instanceId'], 'recipient': a['defenders'][0]} for a in d['attackers']]
                action.update(type='attackers', assignments=assignments)
            elif kind == 'InputBlock': action.update(type='blockers', assignments=[])
            elif kind == 'InputSelectTargets':
                if name=='divided damage' and len(d['selected'])>=2:
                    action.update(type='ok')
                else:
                    candidates=[t for t in d['targets'] if t not in d['selected']]
                    target=next((t for t in candidates if t==2), candidates[0])
                    action.update(type='selectTarget', instanceId=target)
                if d['divided']: seen.add('divided target')
            elif kind in ('InputSelectCardsFromList', 'InputSelectEntitiesFromList'):
                action.update(type='selectMany', instanceIds=d['choices'][:max(d['min'], min(1,d['max']))])
                seen.add('card selection')
            elif kind == 'InputConfirm' and not d['startingPlayer']:
                seen.add('confirmation')
            try:
                engine.request('interactiveAction', **action)
            except EngineError as error:
                if 'Stale human action' not in str(error): raise
            time.sleep(.02)
        (ROOT/'analysis/prompt-games-failure.json').write_text(json.dumps(s, indent=2), encoding='utf8')
        raise AssertionError(name+': missing '+str(required-seen))
    finally:
        native.terminate(); native.wait(timeout=5)
        engine.request('interactiveClose')

try:
    exercise('scry', [('Island',28),('Faerie Seer',20),('Preordain',12)], [('Forest',60)], {'optional scry choice','library order'})
    exercise('modes', [('Forest',28),('You Meet in a Tavern',16),('Llanowar Elves',16)], [('Forest',60)], {'modal choice','card selection'})
    exercise('divided damage', [('Mountain',28),('Flames of the Firebrand',32)], [('Forest',60)], {'divided target','divided damage amount'})
    exercise('trample', [('Forest',36),('Colossal Dreadmaw',24)], [('Forest',22),('Wall of Blossoms',38)], {'combat damage allocation'})
finally:
    engine.close()
