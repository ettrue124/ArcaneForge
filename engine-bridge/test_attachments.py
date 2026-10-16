"""Real Forge equipment attachment and reassignment through the Arena encoder."""
import json
from pathlib import Path
import subprocess
import sys
import time

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from server.engine import Engine, EngineError

ids={c['name']:c['grpId'] for c in json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'))}
def deck(entries):
    return {'name':'Attachment regression','cards':[{'name':name,'grpId':ids[name],'quantity':count} for name,count in entries]}

engine=Engine.from_config(ROOT/'engine-bridge/launch.json')
native=subprocess.Popen(['dotnet',str(ROOT/'protocol-host/bin/TargetAudit/ProtocolHost.dll'),'--flow-probe'],
    stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf8',cwd=ROOT)
first_target=None
desired_target=None
try:
    engine.request('interactiveStart',decks=[deck([('Forest',24),('Grizzly Bears',24),('Short Sword',12)]),deck([('Forest',60)])])
    deadline=time.monotonic()+180
    while time.monotonic()<deadline:
        poll=engine.request('interactivePoll')
        snapshot=engine.request('interactiveSnapshot')
        native.stdin.write(json.dumps(snapshot)+'\n');native.stdin.flush()
        line=native.stdout.readline()
        assert line,'Arena probe exited during equipment scenario'
        view=json.loads(line)
        cards={c['instanceId']:c for c in snapshot['cards']}
        ours=[c for c in cards.values() if c['zone']=='Battlefield' and c['controllerSeat']==1]
        bears=[c for c in ours if c['grpId']==ids['Grizzly Bears']]
        swords=[c for c in ours if c['grpId']==ids['Short Sword']]
        if swords and swords[0].get('attachedTo'):
            sword=swords[0];target=sword['attachedTo']
            assert {'source':sword['instanceId'],'target':target} in view['attachments'],'Native equipment link missing'
            assert cards[target]['power']==3 and cards[target]['toughness']==3,'Equipped bear did not receive +1/+1'
            if first_target is None:
                first_target=target
                print('PASS: real Forge equip creates native attachment and +1/+1',flush=True)
            elif target!=first_target:
                assert cards[first_target]['power']==2 and cards[first_target]['toughness']==2,'Old bear retained equipment bonus'
                assert len([a for a in view['attachments'] if a['source']==sword['instanceId']])==1,'Duplicate native equipment link'
                print('PASS: moving real Equipment updates its native target and removes the old bonus',flush=True)
                break
        descriptor=snapshot['input'];kind=descriptor['type'];event=snapshot['buttonEventId']
        assert not poll['pending'],'Unhandled equipment prompt'
        if not event or kind=='InputLockUI':
            time.sleep(.02);continue
        action={'type':'ok','eventId':event}
        if kind=='InputPassPriority':
            legal=[a for a in snapshot.get('legalActions',[]) if a.get('highlight',3)==3]
            equips=[a for a in legal if a['actionType']==2 and swords and a['instanceId']==swords[0]['instanceId']]
            targets=[c for c in bears if c['instanceId']!=first_target]
            if equips and len(bears)>=2 and targets:
                desired_target=targets[0]['instanceId']
                action.update(type='playAbility',abilityId=equips[0]['abilityId'])
            else:
                options=[a for a in legal if a['isLand'] or a['actionType']==1 and
                    (a['grpId']==ids['Grizzly Bears'] and len(bears)<2 or a['grpId']==ids['Short Sword'] and not swords)]
                options.sort(key=lambda a:not a['isLand'])
                if options:action.update(type='playAbility',abilityId=options[0]['abilityId'])
        elif kind=='InputSelectTargets':
            assert desired_target in descriptor['targets'],'Forge did not offer the chosen equipment target'
            action.update(type='selectTarget',instanceId=desired_target)
        elif kind in ('InputAttack','InputBlock'):
            action.update(type='attackers' if kind=='InputAttack' else 'blockers',assignments=[])
        elif kind in ('InputSelectCardsFromList','InputSelectEntitiesFromList'):
            action.update(type='selectMany',instanceIds=descriptor['choices'][:descriptor['min']])
        try:
            engine.request('interactiveAction',**action)
        except EngineError as error:
            if 'Stale human action' not in str(error):raise
        time.sleep(.03)
    else:
        raise AssertionError('Equipment attachment/reassignment did not complete')
finally:
    native.terminate();native.wait(timeout=5)
    engine.close()
