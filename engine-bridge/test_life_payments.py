"""Real shock-land payment and decline with Arena's native Pay Life request."""
import json,subprocess,sys,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from server.engine import Engine,EngineRejected
from server.feature_lab import prepare
catalogue={c['grpId']:c for c in json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'))}
decks,scenario=prepare('life-payments',catalogue)
engine=Engine.from_config(Path(__file__).with_name('launch.json'))
host=Path(sys.argv[1]) if len(sys.argv)>1 else ROOT/'protocol-host/bin/TargetAudit/ProtocolHost.dll'
native=subprocess.Popen(['dotnet',str(host),'--flow-probe'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf8',cwd=ROOT)
answered=set();verified=set();fixtures=[]
try:
    engine.request('interactiveStart',decks=decks,featureScenario=scenario)
    deadline=time.monotonic()+120
    while time.monotonic()<deadline:
        engine.request('interactivePoll');s=engine.request('interactiveSnapshot')
        native.stdin.write(json.dumps(s)+'\n');native.stdin.flush();view=json.loads(native.stdout.readline())
        d=s['input'];kind=d['type'];event=s['buttonEventId']
        for c in s['cards']:
            if c['name'] in answered and c['zone']=='Battlefield':
                assert c['tapped']==(c['name']=='Blood Crypt'), 'Paid/declined shock-land entry state incorrect'
                verified.add(c['name'])
        if len(verified)==2:
            assert s['players'][0]['life']==18,'Declined payment changed life or accepted payment was not charged'
            (ROOT/'analysis/life-payment-check.json').write_text(json.dumps(fixtures),encoding='utf8')
            print('PASS: native Pay Life prompt; accept loses 2 life and enters untapped; decline enters tapped without paying')
            break
        if not event or kind in ('','InputLockUI'):time.sleep(.02);continue
        action={'type':'ok','eventId':event}
        if kind=='InputConfirm' and not d.get('startingPlayer') and 'pay 2 life' in d.get('message','').lower():
            payment=view['lifePayment']
            assert view['requestType']=='OptionalActionMessage' and payment['prompt']==8167 and payment['amount']==2
            assert payment['source']==d['sourceId']
            assert d.get('entryReplacementDescription'), 'Entry payment lost its replacement-effect context'
            assert any(e['source']==d['sourceId'] and e['ability']==90846 for e in view['entryPayments']), 'Shock-land layout ability missing'
            answered.add(d['cardName']);fixtures.append({'input':d,'native':payment})
            action['type']='ok' if d['cardName']=='Hallowed Fountain' else 'cancel'
        elif kind=='InputPassPriority':
            name='Hallowed Fountain' if 'Hallowed Fountain' not in verified else 'Blood Crypt'
            selected=next((a for a in s['legalActions'] if a['cardName']==name and a.get('highlight',3)==3 and a['isLand']),None)
            if selected:action.update(type='playAbility',abilityId=selected['abilityId'])
        elif kind=='InputAttack':action.update(type='attackers',assignments=[])
        elif kind=='InputBlock':action.update(type='blockers',assignments=[])
        elif kind.startswith('InputGui'):
            engine.request('interactiveAnswer',eventId=event,answer=[o['id'] for o in d.get('options',[])[:max(1,d.get('min',0))]])
            continue
        try:engine.request('interactiveAction',**action)
        except EngineRejected as error:
            if not any(t in str(error) for t in ('Stale human action','Previous human action still running')):raise
        time.sleep(.02)
    else:raise AssertionError('Life-payment flow did not finish: '+str(verified))
finally:
    native.terminate();native.wait(timeout=5);engine.close()
