"""One Forge creature blocks multiple attackers through native assignments."""
import json
from pathlib import Path
import subprocess
import sys
import time
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from server.engine import Engine,EngineError
IDS={c['name']:c['grpId'] for c in json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'))}
def deck(entries):return {'name':'Combat regression','cards':[{'name':n,'grpId':IDS[n],'quantity':q} for n,q in entries]}
engine=Engine.from_config(Path(__file__).with_name('launch.json'))
native=subprocess.Popen(['dotnet',str(ROOT/'protocol-host/bin/TargetAudit/ProtocolHost.dll'),'--flow-probe'],
    stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf8',cwd=ROOT)
cursor=0;planned=None
try:
    # Forced attackers make this a combat regression, independent of whether
    # the AI chooses to attack a larger defending board with ordinary Bears.
    engine.request('interactiveStart',decks=[deck([('Forest',30),('Spike-Tailed Ceratops',15),('Arboreal Grazer',15)]),deck([('Forest',30),('Juggernaut',30)])])
    deadline=time.monotonic()+150
    while time.monotonic()<deadline:
        engine.request('interactivePoll');s=engine.request('interactiveSnapshot',afterPlaybackId=cursor);cursor=s['playbackId']
        native.stdin.write(json.dumps(s)+'\n');native.stdin.flush();p=json.loads(native.stdout.readline())
        d=s['input'];kind=d['type'];event=s['buttonEventId'];cards={c['instanceId']:c for c in s['cards']}
        if not event or kind in ('','InputLockUI'):time.sleep(.02);continue
        if kind.startswith('InputGui'):
            if kind=='InputGuiChoice':answer=[o['id'] for o in d['options'][:max(1,d['min'])]]
            elif kind=='InputGuiCombatDamage':answer=[{'id':a['id'],'amount':a['amount']} for a in p['damageAssigners'][0]['assignments']]
            else:raise AssertionError(d)
            engine.request('interactiveAnswer',eventId=event,answer=answer);time.sleep(.02);continue
        action={'type':'ok','eventId':event}
        if kind=='InputPassPriority':
            names={c['name'] for c in cards.values() if c['zone']=='Battlefield' and c['controllerSeat']==1}
            options=[a for a in s['legalActions'] if a.get('highlight',3)==3 and (a['isLand'] or a['cardName']=='Spike-Tailed Ceratops'
                or a['cardName']=='Arboreal Grazer' and len([c for c in cards.values() if c['zone']=='Battlefield' and c['name']=='Arboreal Grazer' and c['controllerSeat']==1])<2)]
            options.sort(key=lambda a:(not a['isLand'],a['cardName']!='Spike-Tailed Ceratops'))
            if options:action.update(type='playAbility',abilityId=options[0]['abilityId'])
        elif kind=='InputAttack':action.update(type='attackers',assignments=[])
        elif kind=='InputBlock':
            for card in cards.values():
                if 'attackTarget' in card:
                    assert card['attackTarget'] in cards or card['attackTarget'] in [1,2],card
                    assert card['attackState'] in [1,2] and 'blockedBy' in card,card
            if planned:
                b=next(b for b in d['blockers'] if b['instanceId']==planned[0])
                assert b['selected']==planned[1],('Forge lost multi-block assignment',b,planned)
                native_b=next(b for b in p['blockers'] if b['id']==planned[0])
                assert native_b['max']>=2 and native_b['selected']==planned[1]
                print('PASS: Forge multi-attacker blocker assignment and native capacity/selected IDs',flush=True);break
            multiple=next((b for b in d['blockers'] if b['maxAttackers']>1 and len(b['attackers'])>=2),None)
            if multiple:
                targets=multiple['attackers'][:2];planned=(multiple['instanceId'],targets)
                action.update(type='blockers',submit=False,assignments=[{'instanceId':planned[0],'recipients':targets}])
            else:
                assignments=[]
                for b in d['blockers']:
                    if b['attackers']:assignments.append({'instanceId':b['instanceId'],'recipients':b['attackers'][:1]})
                action.update(type='blockers',assignments=assignments)
        elif kind in ('InputSelectCardsFromList','InputSelectEntitiesFromList'):
            action.update(type='selectMany',instanceIds=d['choices'][:d['min']])
        try:engine.request('interactiveAction',**action)
        except EngineError as error:
            if 'Stale human action' not in str(error):raise
        time.sleep(.02)
    else:raise AssertionError('Multi-block assignment was not offered')
except Exception:
    if 's' in globals():(ROOT/'analysis/multiblock-failure.json').write_text(json.dumps(s),encoding='utf8')
    for diagnostic in engine.diagnostics:print(diagnostic,file=sys.stderr)
    raise
finally:
    native.terminate();native.wait(timeout=5);engine.close()
