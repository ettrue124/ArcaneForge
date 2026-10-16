"""Explore uses its native revealed-card browser and preserves both destinations."""
import json,subprocess,sys,time
from collections import Counter
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from server.engine import Engine,EngineRejected
from server.feature_lab import prepare
catalogue={c['grpId']:c for c in json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'))}
ids={c['name']:gid for gid,c in catalogue.items()}
host=Path(sys.argv[1]) if len(sys.argv)>1 else ROOT/'protocol-host/bin/TargetAudit/ProtocolHost.dll'
fixtures=[]
for destination in ('Graveyard','Library','Hand'):
    decks,scenario=prepare('multistep',catalogue)
    setup=scenario['players'][0];setup['hand']=['Merfolk Branchwalker']
    library_card='Forest' if destination=='Hand' else 'Giant Growth'
    names=setup['battlefield']+setup['hand']+[library_card]*30
    decks[0]['cards']=[dict(name=n,grpId=ids[n],quantity=q) for n,q in Counter(names).items()]
    engine=Engine.from_config(Path(__file__).with_name('launch.json'))
    native=subprocess.Popen(['dotnet',str(host),'--flow-probe'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf8',cwd=ROOT)
    chosen=None;cast=False;observed_prompt=False;cursor=0
    try:
        engine.request('interactiveStart',decks=decks,featureScenario=scenario)
        deadline=time.monotonic()+100
        while time.monotonic()<deadline:
            engine.request('interactivePoll');s=engine.request('interactiveSnapshot',afterPlaybackId=cursor);cursor=s['playbackId']
            native.stdin.write(json.dumps(s)+'\n');native.stdin.flush();view=json.loads(native.stdout.readline())
            d=s['input'];kind=d['type'];event=s['buttonEventId']
            branch=next((c for c in s['cards'] if c['name']=='Merfolk Branchwalker' and c['zone']=='Battlefield'),None)
            if branch and kind=='InputPassPriority' and cast and not s.get('stack'):
                if destination=='Hand':
                    assert not observed_prompt and any(c['name']=='Forest' and c['zone']=='Hand' for c in s['cards'])
                    assert not branch['counters']
                else:
                    assert chosen is not None and any(c['instanceId']==chosen and c['zone']==destination for c in s['cards'])
                    assert any(c['count']==1 for c in branch['counters'])
                print('PASS: Explore destination',destination,'with correct native menu/counter behavior',flush=True)
                break
            if not event or kind in ('','InputLockUI'):time.sleep(.02);continue
            action={'type':'ok','eventId':event}
            if kind=='InputConfirm' and 'exploreCardId' in d:
                observed_prompt=True;chosen=d['exploreCardId']
                assert view['requestType']=='OptionalActionMessage' and view['exploreCards']==[chosen]
                assert any(c['instanceId']==chosen and c['grpId']==ids['Giant Growth'] for c in s['cards']), 'Explore card remains hidden'
                fixtures.append({'input':d,'recipientIds':view['exploreCards'],'destination':destination})
                action['type']='ok' if destination=='Graveyard' else 'cancel'
            elif kind=='InputPassPriority' and not cast:
                a=next((a for a in s['legalActions'] if a['cardName']=='Merfolk Branchwalker' and a.get('highlight',3)==3 and a['actionType']==1),None)
                if a:action.update(type='playAbility',abilityId=a['abilityId']);cast=True
            elif kind=='InputAttack':action.update(type='attackers',assignments=[])
            elif kind=='InputBlock':action.update(type='blockers',assignments=[])
            elif kind.startswith('InputGui'):raise AssertionError('Explore unexpectedly used a generic GUI menu: '+str(d))
            try:engine.request('interactiveAction',**action)
            except EngineRejected as error:
                if 'Stale human action' not in str(error):raise
            time.sleep(.02)
        else:raise AssertionError('Explore did not finish: '+destination)
    finally:
        native.terminate();native.wait(timeout=5);engine.close()
(ROOT/'analysis/explore-menu-check.json').write_text(json.dumps(fixtures),encoding='utf8')
