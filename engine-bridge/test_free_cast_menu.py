"""Real Ur-Sphinx cast/decline prompts and battlefield arrival identities."""
import json,subprocess,sys,time
from collections import Counter
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from server.engine import Engine,EngineRejected
from server.feature_lab import prepare
catalogue={c['grpId']:c for c in json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'))}
ids={c['name']:gid for gid,c in catalogue.items()}
host=Path(sys.argv[1]) if len(sys.argv)>1 else ROOT/'protocol-host/bin/ArrivalAudit/ProtocolHost.dll'
results=[]
for accept in (True,False):
    decks,scenario=prepare('multistep',catalogue)
    for seat,setup in enumerate(scenario['players']):
        setup['hand']=[];setup['battlefield']=['The Ur-Sphinx'] if seat==0 else []
        names=setup['battlefield']+(['Azorius Locket'] if seat==0 else ['Forest'])*35
        decks[seat]['cards']=[dict(name=n,grpId=ids[n],quantity=q) for n,q in Counter(names).items()]
    engine=Engine.from_config(Path(__file__).with_name('launch.json'))
    native=subprocess.Popen(['dotnet',str(host),'--flow-probe'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf8',cwd=ROOT)
    answered=False;offered=None;cursor=0;arrival=None;previous_arrival=None
    try:
        engine.request('interactiveStart',decks=decks,featureScenario=scenario)
        deadline=time.monotonic()+100
        while time.monotonic()<deadline:
            engine.request('interactivePoll');s=engine.request('interactiveSnapshot',afterPlaybackId=cursor);cursor=s['playbackId']
            native.stdin.write(json.dumps(s)+'\n');native.stdin.flush();view=json.loads(native.stdout.readline())
            d=s['input'];kind=d['type'];event=s['buttonEventId']
            sphinx=next((c for c in s['cards'] if c['name']=='The Ur-Sphinx' and c['zone']=='Battlefield'),None)
            locket=next((c for c in s['cards'] if c['name']=='Azorius Locket' and c['zone']=='Battlefield'),None)
            if locket:
                assert locket['instanceId']>sphinx['instanceId'],'New arrivals must have increasing layout identities'
                arrival=locket['instanceId']
                assert any(c['newId']==arrival for c in view['identityChanges']) or arrival==previous_arrival
            previous_arrival=arrival
            if answered and kind=='InputPassPriority' and not s.get('stack'):
                assert bool(locket)==accept
                assert accept or any(c['instanceId']==offered and c['zone']=='Graveyard' for c in s['cards'])
                results.append(dict(accept=accept,offered=offered,arrival=arrival))
                print('PASS: Ur-Sphinx free cast', 'accept and increasing arrival identity' if accept else 'decline leaves card in graveyard',flush=True)
                break
            if not event or kind in ('','InputLockUI'):time.sleep(.02);continue
            action={'type':'ok','eventId':event}
            if kind=='InputConfirm' and 'freeCastCardId' in d:
                assert not answered
                offered=d['freeCastCardId'];actions=view['offeredActions']
                assert view['requestType']=='ActionsAvailableReq'
                cast=next(a for a in actions if a['type']=='Cast')
                assert cast['id']==offered and any(a['type']=='Pass' for a in actions)
                assert any(a['id']==cast['source'] for a in view['stackAbilities']), 'Must identify the resolving ability'
                answered=True;action['type']='ok' if accept else 'cancel'
            elif kind=='InputAttack':
                action.update(type='attackers',assignments=[{'instanceId':a['instanceId'],'recipient':a['defenders'][0]} for a in d['attackers']])
            elif kind=='InputBlock':action.update(type='blockers',assignments=[])
            elif kind.startswith('InputGui'):
                options=d.get('options',[])
                assert options, str(d)
                engine.request('interactiveAnswer',eventId=event,answer=[options[0]['id']]);continue
            try:engine.request('interactiveAction',**action)
            except EngineRejected as error:
                if 'Stale human action' not in str(error):raise
            time.sleep(.02)
        else:raise AssertionError('Ur-Sphinx prompt did not finish')
    finally:
        native.terminate();native.wait(timeout=5);engine.close()
(ROOT/'analysis/free-cast-menu-check.json').write_text(json.dumps(results,indent=2),encoding='utf8')
