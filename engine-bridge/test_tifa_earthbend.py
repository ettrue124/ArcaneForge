"""Real Forge Tifa landfall/expiry and repeat Ba Sing Se activation, plus native wire."""
import json, subprocess, sys, time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from server.engine import Engine,EngineError
ids={c['name']:c['grpId'] for c in json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'))}
def deck(entries): return {'name':'Tifa/earthbend regression','cards':[{'name':n,'grpId':ids[n],'quantity':q} for n,q in entries]}
engine=Engine.from_config(Path(__file__).with_name('launch.json'))
native=subprocess.Popen(['dotnet',str(ROOT/'protocol-host/bin/TargetAudit/ProtocolHost.dll'),'--flow-probe'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf8',cwd=ROOT)
seen=set(); cursor=0; earth_target=None; earth_activations=0; last_activation_turn=0; boosted_turn=0
try:
    engine.request('interactiveStart',decks=[deck([('Forest',32),('Tifa Lockhart',12),('Ba Sing Se',16)]),deck([('Forest',60)])])
    deadline=time.monotonic()+150
    while time.monotonic()<deadline:
        poll=engine.request('interactivePoll'); s=engine.request('interactiveSnapshot',afterPlaybackId=cursor);cursor=s['playbackId']
        native.stdin.write(json.dumps(s)+'\n');native.stdin.flush();p=json.loads(native.stdout.readline())
        cards={c['instanceId']:c for c in s['cards']}; wire={c['id']:c for c in p['stats']}; d=s['input']; kind=d['type']; turn=s['turn']['number']
        battlefield=[c for c in s['cards'] if c['zone']=='Battlefield' and c['ownerSeat']==1]
        for c in battlefield:
            assert wire[c['instanceId']]['power']==c['power'] and wire[c['instanceId']]['toughness']==c['toughness'],'Native power/toughness diverged from Forge'
            if c['grpId']==ids['Tifa Lockhart']:
                assert not c['counters'], 'Tifa power doubling incorrectly created counters'
                if c['power']>1:
                    assert c['power'] in (2,4,8,16) and c['toughness']==2,'Incorrect Tifa doubling'
                    seen.add('tifaDoubledPower');boosted_turn=turn
                if boosted_turn and turn>boosted_turn and c['power']==1:seen.add('tifaExpired')
        for a in p['stackAbilities']:
            if cards[a['parent']]['grpId']==ids['Tifa Lockhart']:
                assert a['ability']==189386,'Wrong native Tifa landfall identity'
                seen.add('tifaNativeTrigger')
        for a in p['actions']:
            if a['type']==2 and cards[a['id']]['grpId']==ids['Ba Sing Se']:
                assert a['ability']==192869 and a['source']==a['id'],'Earthbend native menu missing/wrong ability'
                seen.add('earthbendNativeMenu')
        if earth_target and earth_target in cards:
            c=cards[earth_target]; count=sum(x['count'] for x in c['counters'] if x['type']=='P1P1')
            if count>=2:
                assert c['power']==count and c['toughness']==count and set(wire[earth_target]['types'])=={'Creature','Land'},'Earthbent land lost creature type/stats'
                assert next(x['count'] for x in p['counters'] if x['target']==earth_target and x['type']==1)==count,'Earthbend counters missing from wire'
                seen.add('earthbendCreatureCounters')
                if count>=4: seen.add('earthbendRepeatedCounters')
        if {'tifaDoubledPower','tifaExpired','tifaNativeTrigger','earthbendNativeMenu','earthbendTargeting','earthbendCreatureCounters','earthbendRepeatedCounters'}<=seen:
            print('PASS:', ', '.join(sorted(seen)),flush=True);break
        assert not poll['pending'], 'Unhandled prompt '+str(poll['pending'])
        event=s['buttonEventId']
        if not event or kind=='InputLockUI':time.sleep(.02);continue
        action={'type':'ok','eventId':event}
        if kind=='InputPassPriority' and not p['shouldPass']:
            options=[a for a in s['legalActions'] if a.get('highlight',3)==3]
            forest_exists=any(c['grpId']==ids['Forest'] for c in battlefield)
            ba_exists=any(c['grpId']==ids['Ba Sing Se'] for c in battlefield)
            tifa_exists=any(c['grpId']==ids['Tifa Lockhart'] for c in battlefield)
            land=[a for a in options if a['isLand']]
            land.sort(key=lambda a: a['grpId']!=(ids['Ba Sing Se'] if forest_exists and not ba_exists else ids['Forest']))
            tifa=[a for a in options if a['actionType']==1 and a['grpId']==ids['Tifa Lockhart'] and not tifa_exists]
            earth=[a for a in options if a['actionType']==2 and a['grpId']==ids['Ba Sing Se'] and earth_activations<2 and turn!=last_activation_turn]
            choice=(tifa or land or earth or [None])[0]
            if choice:
                action.update(type='playAbility',abilityId=choice['abilityId'])
                if choice in earth:
                    earth_activations+=1;last_activation_turn=turn
                    print('Activate earthbend',earth_activations,'turn',turn,flush=True)
        elif kind=='InputSelectTargets':
            assert p['targetingAbility']==192869,'Wrong earthbend targeting ability'
            seen.add('earthbendTargeting')
            if earth_target not in d['targets']:earth_target=next(t for t in d['targets'] if cards[t]['grpId']==ids['Forest'])
            action.update(type='selectTarget',instanceId=earth_target)
        elif kind=='InputAttack': action.update(type='attackers',assignments=[])
        elif kind=='InputBlock': action.update(type='blockers',assignments=[])
        try:engine.request('interactiveAction',**action)
        except EngineError as error:
            if 'Stale human action' not in str(error):raise
        time.sleep(.03)
    else:raise AssertionError('Missing flows '+str(seen))
finally:
    native.terminate();native.wait(timeout=5);engine.close()
