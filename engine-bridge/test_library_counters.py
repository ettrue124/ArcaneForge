"""Real Forge + native wire regression for Chocobo top plays and Hydra counters."""
import json, subprocess, sys, time
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from server.engine import Engine, EngineError
ids = {c['name']: c['grpId'] for c in json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'))}
def deck(entries):
    return {'name':'Library/counters regression','cards':[{'name':n,'grpId':ids[n],'quantity':q} for n,q in entries]}
engine=Engine.from_config(Path(__file__).with_name('launch.json'))
native=subprocess.Popen(['dotnet',str(ROOT/'protocol-host/bin/TargetAudit/ProtocolHost.dll'),'--flow-probe'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf8',cwd=ROOT)
seen=set();cursor=0;old_top=None;library_plays={}
try:
    engine.request('interactiveStart',decks=[deck([('Forest',28),('Traveling Chocobo',16),('Mossborn Hydra',16)]),deck([('Forest',60)])])
    deadline=time.monotonic()+150
    while time.monotonic()<deadline:
        poll=engine.request('interactivePoll')
        s=engine.request('interactiveSnapshot',afterPlaybackId=cursor);cursor=s['playbackId']
        native.stdin.write(json.dumps(s)+'\n');native.stdin.flush();presentation=json.loads(native.stdout.readline())
        by_id={c['instanceId']:c for c in s['cards']}
        for instance,label in library_plays.items():
            if by_id.get(instance,{}).get('zone')=='Battlefield':seen.add(label)
        library=[c for c in s['cards'] if c['zone']=='Library' and c['ownerSeat']==1]
        own=next(z for z in presentation['library'] if z['seat']==1)
        expected=[c['instanceId'] for c in library if 1 in c['viewers']]
        assert own['ids']==[c['instanceId'] for c in library], 'Library order changed in wire encoding'
        assert own['visible']==expected, 'Native top look differs from Forge permission'
        assert not next(z for z in presentation['library'] if z['seat']==2)['visible'], 'Opponent library leaked'
        if expected:
            assert expected==[library[0]['instanceId']], 'Top permission revealed other cards'
            seen.add('topVisible')
            if old_top and old_top!=expected[0]:seen.add('topChanged')
            old_top=expected[0]
        else:seen.add('hiddenWithoutPermission')
        for counter in presentation['counters']:
            actual=next(c for c in by_id[counter['target']]['counters'] if c['type']=='P1P1')
            assert counter['type']==1 and counter['count']==actual['count'], 'Native counter type/count differs from rules'
            seen.add('typedCounter')
            if counter['count']>1:seen.add('counterGrowth')
        if 'CounterAdded' in presentation['annotations']:seen.add('counterAnimation')
        if {'topVisible','topChanged','hiddenWithoutPermission','libraryLand','libraryBird','typedCounter','counterGrowth','counterAnimation'}<=seen:
            print('PASS:', ', '.join(sorted(seen)),flush=True);break
        d=s['input'];kind=d['type'];event=s['buttonEventId']
        if poll['pending']:raise AssertionError('Unexpected blocking prompt: '+str(poll['pending']))
        if not event or kind=='InputLockUI':time.sleep(.02);continue
        action={'type':'ok','eventId':event}
        library_play=None
        if kind=='InputPassPriority' and not presentation['shouldPass']:
            options=[a for a in s['legalActions'] if a.get('highlight',3)==3 and a['actionType'] in (1,3)]
            has_chocobo=any(c['zone']=='Battlefield' and c['ownerSeat']==1 and c['grpId']==ids['Traveling Chocobo'] for c in s['cards'])
            # Reach a look permission, then prefer top-card actions over hand cards.
            options.sort(key=lambda a:(not a['isLand'], not (has_chocobo and by_id[a['instanceId']]['zone']=='Library'), a['grpId']!=ids['Traveling Chocobo'] if not has_chocobo else a['grpId']!=ids['Mossborn Hydra']))
            if options:
                choice=options[0];action.update(type='playAbility',abilityId=choice['abilityId'])
                if by_id[choice['instanceId']]['zone']=='Library':
                    assert choice['instanceId']==library[0]['instanceId']
                    library_play=(choice['instanceId'],'libraryLand' if choice['isLand'] else 'libraryBird')
                    print('Play from library:',by_id[choice['instanceId']]['grpId'],flush=True)
        elif kind=='InputAttack':action.update(type='attackers',assignments=[])
        elif kind=='InputBlock':action.update(type='blockers',assignments=[])
        elif kind in ('InputSelectCardsFromList','InputSelectEntitiesFromList'):
            action.update(type='selectMany',instanceIds=d['choices'][:d['min']])
        try:
            engine.request('interactiveAction',**action)
            if library_play:library_plays[library_play[0]]=library_play[1]
        except EngineError as error:
            if 'Stale human action' not in str(error):raise
        time.sleep(.03)
    else:raise AssertionError('Missing flows: '+str(seen))
finally:
    native.terminate();native.wait(timeout=5)
    engine.close()
