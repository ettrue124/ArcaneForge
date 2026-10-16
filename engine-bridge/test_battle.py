"""Real Forge regression: casts, mana payment, player targets, combat and AI."""
import json
import sys
import time
import subprocess
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from server.engine import Engine, EngineError

catalogue = json.loads((ROOT / 'data/catalogue.json').read_text(encoding='utf8'))
ids = {card['name']: card['grpId'] for card in catalogue}
def deck():
    return {'name': 'Battle regression', 'cards': [
        {'name': name, 'grpId': ids[name], 'quantity': count}
        for name, count in [('Mountain', 24), ('Goblin Assailant', 24), ('Shock', 12)]]}
engine = Engine.from_config(Path(__file__).with_name('launch.json'))
seen = set()
previous = None
playback_cursor = 0
playback = []
playback_cases = []
previous_snapshot = None
native = subprocess.Popen(['dotnet',str(ROOT/'protocol-host/bin/TargetAudit/ProtocolHost.dll'),'--flow-probe'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf8',cwd=ROOT) if '--native' in sys.argv else None
native_seen=set()
try:
    opponent = {'name': 'Stable battle opponent', 'cards': [{'name': 'Mountain', 'grpId': ids['Mountain'], 'quantity': 60}]}
    engine.request('interactiveStart', decks=[deck(), opponent])
    deadline = time.monotonic() + 180
    while time.monotonic() < deadline:
        batch = engine.request('interactivePoll')
        snapshot = engine.request('interactiveSnapshot', afterPlaybackId=playback_cursor)
        entries = snapshot.get('playbackEvents', [])
        assert not snapshot.get('playbackGap'), 'Playback events were lost'
        assert all(e['eventId'] > playback_cursor for e in entries)
        assert [e['eventId'] for e in entries] == sorted({e['eventId'] for e in entries})
        playback.extend(entries)
        if entries and previous_snapshot:
            playback_cases.append({'previous': previous_snapshot, 'current': snapshot})
        playback_cursor = snapshot['playbackId']
        previous_snapshot = snapshot
        if (batch.get('state') or {}).get('gameOver'):
            assert snapshot['stage'] == 3 and snapshot['winnerSeat'] == 1 and not snapshot['draw'], 'Lethal damage lost the final outcome'
            assert snapshot['loserSeat'] == 2 and snapshot['lossReason'] == 'LifeReachedZero', 'Lethal damage lost Arena defeat-effect context'
            seen.add('gameOver')
            break
        descriptor = snapshot.get('input', {})
        kind = descriptor.get('type', '')
        presentation=None
        if native:
            native.stdin.write(json.dumps(snapshot)+'\n');native.stdin.flush()
            presentation=json.loads(native.stdout.readline())
            if presentation['targetLinks']: native_seen.add('targetLinks')
            if 'PlayerSelectingTargets' in presentation['annotations']: native_seen.add('selecting')
            if 'PlayerSubmittedTargets' in presentation['annotations']: native_seen.add('submitted')
            if kind=='InputSelectTargets':
                assert not presentation['shouldPass'], 'Native flow skipped target choice'
                assert presentation['targetingAbility']>0, 'Native targeting request lacks installed ability identity'
        if kind != previous:
            print('Input:', kind, 'turn:', snapshot['turn']['number'], flush=True)
            previous = kind
        event = batch.get('buttonEventId', 0)
        if batch.get('pending'):
            blocking = [e for e in batch['events'] if e['blocking']]
            for prompt in blocking:
                method, args = prompt['method'], prompt['arguments']
                if method in ('confirm', 'showConfirmDialog'): answer = True
                elif method in ('showOptionDialog', 'getAbilityToPlay'): answer = 0
                elif method == 'getChoices': answer = list(range(args[1]))
                else: raise AssertionError(f'Unhandled prompt {method}')
                engine.request('interactiveAnswer', eventId=prompt['eventId'], answer=answer)
            time.sleep(.02)
            continue
        if not event:
            time.sleep(.02)
            continue
        action = {'type': 'ok', 'eventId': event}
        if presentation and presentation['shouldPass']:
            native_seen.add('automatic')
            if snapshot.get('stack'): native_seen.add('ownStackAutoPass')
            if kind.startswith('InputPayMana'): seen.add('mana')
        elif kind == 'InputPassPriority':
            options = [a for a in snapshot.get('legalActions', []) if a['actionType'] != 4 and a.get('highlight',3)==3]
            # Prefer a land, then any available spell; Forge validates affordability.
            options.sort(key=lambda a: not a['isLand'])
            if options:
                action.update(type='playAbility', abilityId=options[0]['abilityId'])
                seen.add('land' if options[0]['isLand'] else 'cast')
        elif kind.startswith('InputPayMana'):
            seen.add('mana')
        elif kind == 'InputSelectTargets':
            target = 2 if 2 in descriptor['targets'] else descriptor['targets'][0]
            action.update(type='selectTarget', instanceId=target)
            seen.add('target')
        elif kind == 'InputAttack':
            action.update(type='attackers', assignments=[
                {'instanceId': a['instanceId'], 'recipient': a['defenders'][0]}
                for a in descriptor['attackers']])
            if descriptor['attackers']: seen.add('attack')
        elif kind == 'InputBlock':
            action.update(type='blockers', assignments=[
                {'instanceId': b['instanceId'], 'recipient': b['attackers'][0]}
                for b in descriptor['blockers']])
            if descriptor['blockers']: seen.add('block')
        try:
            engine.request('interactiveAction', **action)
        except EngineError as error:
            if 'Stale human action' not in str(error): raise
        time.sleep(.03)
    assert {'cast', 'mana', 'target', 'attack', 'gameOver'} <= seen, f'Missing flows: {seen}'
    if native:
        assert {'targetLinks','selecting','submitted','automatic','ownStackAutoPass'} <= native_seen, f'Missing native flows: {native_seen}'
        print('PASS: native adapter targets and automatic progression:', ', '.join(sorted(native_seen)))
    assert {e['damageType'] for e in playback if e['type'] == 'damage'} >= {1,2}, 'Combat/spell damage playback missing'
    assert all(e['amount'] > 0 and e['sourceId'] >= 1000 and e['targetId'] == 2 for e in playback if e['type'] == 'damage')
    resolving = []
    for e in playback:
        if e['type'] == 'resolutionStart': resolving.append(e['sourceId'])
        if e['type'] == 'resolutionComplete': assert resolving.pop() == e['sourceId'], 'Resolution order/source mismatch'
    assert not resolving, 'Resolution was left open'
    assert any(e['type'] == 'tap' for e in playback) and any(e['type'] == 'zoneTransfer' for e in playback)
    replay = engine.request('interactiveSnapshot', afterPlaybackId=playback_cursor)
    assert replay['playbackEvents'] == [], 'Acknowledged playback replayed'
    fixture = ROOT/'analysis/playback-check.json'
    fixture.write_text(json.dumps(playback_cases), encoding='utf8')
    print(f'PASS: {len(playback)} ordered Forge playback events; spell/combat damage, resolution, taps, zone moves and cursor deduplication')
    print('PASS: real Forge battle flows:', ', '.join(sorted(seen)))
finally:
    if native: native.terminate();native.wait(timeout=5)
    engine.request('interactiveClose')
    engine.close()
