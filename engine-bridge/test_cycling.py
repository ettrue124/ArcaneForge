"""Real Forge land-play and cycling choices must stay distinct in Arena."""
import json, subprocess, sys, time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from server.engine import Engine, EngineError
ids={c['name']:c['grpId'] for c in json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'))}
engine=Engine.from_config(Path(__file__).with_name('launch.json'))
native=subprocess.Popen(['dotnet',str(ROOT/'protocol-host/bin/TargetAudit/ProtocolHost.dll'),'--flow-probe'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf8',cwd=ROOT)
try:
    engine.request('interactiveStart',decks=[{'name':name,'cards':[{'name':name,'grpId':ids[name],'quantity':60}]} for name in ['Forgotten Cave','Plains']])
    deadline=time.monotonic()+120
    chosen=None; before=0; cursor=0
    while time.monotonic()<deadline:
        engine.request('interactivePoll')
        s=engine.request('interactiveSnapshot',afterPlaybackId=cursor);cursor=s['playbackId']
        native.stdin.write(json.dumps(s)+'\n');native.stdin.flush();p=json.loads(native.stdout.readline())
        cards={c['instanceId']:c for c in s['cards']};d=s['input'];event=s['buttonEventId'];kind=d['type']
        battlefield=sum(c['zone']=='Battlefield' and c['controllerSeat']==1 for c in cards.values())
        if chosen and battlefield>before:
            assert cards[chosen]['zone']=='Battlefield','Play land activated cycling'
            assert any(e['type']=='zoneTransfer' and e['targetId']==chosen and e['category']=='PlayLand'
                for e in s['playbackEvents']),'Land-play animation event missing'
            assert any(e['type']=='userAction' and e['sourceId']==chosen and e['actionType']==3
                for e in s['playbackEvents']),'Official-style land user-action event missing'
            assert 'UserActionTaken' in p['annotations'],'Land user-action annotation missing from native output'
            print('PASS: native Play land and Cycling choices have distinct ability IDs; Play land put the card on the battlefield.',flush=True)
            break
        if not event or kind in ('','InputLockUI'):time.sleep(.02);continue
        action={'type':'ok','eventId':event}
        if kind=='InputPassPriority':
            legal=[a for a in s['legalActions'] if a.get('highlight',3)==3]
            plays=[a for a in legal if a['isLand']]
            cycles=[a for a in legal if a.get('cycling')]
            pair=next(((land,cycle) for land in plays for cycle in cycles if land['instanceId']==cycle['instanceId']),None)
            if pair:
                land,cycle=pair
                menus=[a for a in p['actions'] if a['id']==land['instanceId']]
                assert any(a['type']==3 for a in menus),menus
                assert any(a['type']==2 and a['ability']==3183 for a in menus),menus
                assert land['abilityId']!=cycle['abilityId']
                chosen=land['instanceId'];before=battlefield
                action.update(type='playAbility',abilityId=land['abilityId'])
            elif plays:action.update(type='playAbility',abilityId=plays[0]['abilityId'])
        elif kind=='InputAttack':action.update(type='attackers',assignments=[])
        elif kind=='InputBlock':action.update(type='blockers',assignments=[])
        elif kind.startswith('InputGui'):
            if kind=='InputGuiChoice':engine.request('interactiveAnswer',eventId=event,answer=[o['id'] for o in d['options'][:max(1,d['min'])]])
            else:raise AssertionError(d)
            continue
        try:engine.request('interactiveAction',**action)
        except EngineError as e:
            if 'Stale human action' not in str(e):raise
        time.sleep(.02)
    else:raise AssertionError('Land play/cycling menu was not offered')
finally:
    native.terminate();native.wait(timeout=5);engine.close()
