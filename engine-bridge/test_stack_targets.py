"""Counterspells remain playable and target the real stack object in Arena."""
import json
from pathlib import Path
import subprocess
import sys
import time
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from server.engine import Engine,EngineError
IDS={c['name']:c['grpId'] for c in json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'))}
def deck(entries):return {'name':'Stack regression','cards':[{'name':n,'grpId':IDS[n],'quantity':q} for n,q in entries]}
engine=Engine.from_config(Path(__file__).with_name('launch.json'))
native=subprocess.Popen(['dotnet',str(ROOT/'protocol-host/bin/TargetAudit/ProtocolHost.dll'),'--flow-probe'],
    stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf8',cwd=ROOT)
cursor=0;target=None;seen=set()
try:
    engine.request('interactiveStart',decks=[deck([('Island',40),('Negate',20)]),deck([('Forest',30),('Wolfwillow Haven',30)])])
    deadline=time.monotonic()+150
    while time.monotonic()<deadline:
        engine.request('interactivePoll');s=engine.request('interactiveSnapshot',afterPlaybackId=cursor);cursor=s['playbackId']
        native.stdin.write(json.dumps(s)+'\n');native.stdin.flush();p=json.loads(native.stdout.readline())
        cards={c['instanceId']:c for c in s['cards']};d=s['input'];kind=d['type'];event=s['buttonEventId']
        if target and cards.get(target,{}).get('zone')=='Graveyard':
            print('PASS: legal counterspell, native stack targeting, countered spell in graveyard',flush=True);break
        if not event or kind in ('','InputLockUI'):time.sleep(.02);continue
        if kind=='InputGuiStackTargets':
            assert p['requestType']=='SelectTargetsReq' and not p['shouldPass'] and p['targetingSource']==d['sourceId']
            assert d['targets'] and all(t in [c['instanceId'] for c in s['stack']] for t in d['targets'])
            target=d['targets'][0];engine.request('interactiveAnswer',eventId=event,answer=[target]);time.sleep(.02);continue
        if kind.startswith('InputGui'):
            assert kind=='InputGuiChoice',d
            engine.request('interactiveAnswer',eventId=event,answer=[o['id'] for o in d['options'][:max(1,d['min'])]])
            time.sleep(.02);continue
        action={'type':'ok','eventId':event}
        if kind=='InputPassPriority':
            options=[a for a in s['legalActions'] if a.get('highlight',3)==3 and (a['isLand'] or a['cardName']=='Negate' and target is None)]
            options.sort(key=lambda a:not a['isLand'])
            if options:
                a=options[0];action.update(type='playAbility',abilityId=a['abilityId'])
                if a['cardName']=='Negate':assert not p['shouldPass'],'Counterspell response was skipped'
        elif kind in ('InputAttack','InputBlock'):action.update(type='attackers' if kind=='InputAttack' else 'blockers',assignments=[])
        elif kind in ('InputSelectCardsFromList','InputSelectEntitiesFromList'):action.update(type='selectMany',instanceIds=d['choices'][:d['min']])
        try:engine.request('interactiveAction',**action)
        except EngineError as error:
            if 'Stale human action' not in str(error):raise
        time.sleep(.02)
    else:raise AssertionError('Counterspell never completed')
except Exception:
    if 's' in globals():(ROOT/'analysis/stack-target-failure.json').write_text(json.dumps(s),encoding='utf8')
    raise
finally:
    native.terminate();native.wait(timeout=5);engine.close()
