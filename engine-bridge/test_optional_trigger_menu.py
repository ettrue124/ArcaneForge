"""Chrome Mox optional trigger uses native confirmation, with both outcomes."""
import json,subprocess,sys,time
from collections import Counter
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from server.engine import Engine,EngineRejected
from server.feature_lab import prepare
catalogue={c['grpId']:c for c in json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'))}
ids={c['name']:gid for gid,c in catalogue.items()}
host=Path(sys.argv[1]) if len(sys.argv)>1 else ROOT/'protocol-host/bin/TriggerMenuAudit/ProtocolHost.dll'
for accept in (True,False):
    decks,scenario=prepare('multistep',catalogue)
    for seat,setup in enumerate(scenario['players']):
        setup['hand']=['Chrome Mox','Giant Growth'] if seat==0 else []
        setup['battlefield']=[]
        names=setup['hand']+['Forest']*35
        decks[seat]['cards']=[dict(name=n,grpId=ids[n],quantity=q) for n,q in Counter(names).items()]
    engine=Engine.from_config(Path(__file__).with_name('launch.json'))
    native=subprocess.Popen(['dotnet',str(host),'--flow-probe'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf8',cwd=ROOT)
    cast=False;answered=False
    try:
        engine.request('interactiveStart',decks=decks,featureScenario=scenario)
        deadline=time.monotonic()+100
        while time.monotonic()<deadline:
            engine.request('interactivePoll');s=engine.request('interactiveSnapshot')
            native.stdin.write(json.dumps(s)+'\n');native.stdin.flush();view=json.loads(native.stdout.readline())
            d=s['input'];kind=d['type'];event=s['buttonEventId']
            if answered and kind=='InputPassPriority' and not s.get('stack'):
                exiled=any(c['name']=='Giant Growth' and c['zone']=='Exile' for c in s['cards'])
                assert exiled==accept
                print('PASS: native Chrome Mox optional trigger', 'accept exiles chosen card' if accept else 'decline preserves hand',flush=True)
                break
            if not event or kind in ('','InputLockUI'):time.sleep(.02);continue
            action={'type':'ok','eventId':event}
            if kind=='InputConfirm' and d.get('optionalTrigger'):
                assert d.get('optionalTriggerPrompt')==7132 and view['requestType']=='OptionalActionMessage'
                answered=True;action['type']='ok' if accept else 'cancel'
            elif kind=='InputPassPriority' and not cast:
                offered=next((a for a in s['legalActions'] if a['cardName']=='Chrome Mox' and a.get('highlight',3)==3 and a['actionType']==1),None)
                if offered:action.update(type='playAbility',abilityId=offered['abilityId']);cast=True
            elif kind=='InputAttack':action.update(type='attackers',assignments=[])
            elif kind=='InputBlock':action.update(type='blockers',assignments=[])
            elif kind.startswith('InputGui'):
                assert answered and accept, str(d)
                options=d.get('options',[])
                assert options,str(d)
                engine.request('interactiveAnswer',eventId=event,answer=[options[0]['id']]);continue
            try:engine.request('interactiveAction',**action)
            except EngineRejected as error:
                if 'Stale human action' not in str(error):raise
            time.sleep(.02)
        else:raise AssertionError('Chrome Mox optional trigger did not finish')
    finally:
        native.terminate();native.wait(timeout=5);engine.close()
