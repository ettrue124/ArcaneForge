"""Real Forge tokens: Arena identities, creation, abilities and sacrifice."""
import json
from pathlib import Path
import subprocess
import sys
import time

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from server.engine import Engine,EngineError

IDS={c['name']:c['grpId'] for c in json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'))}
def deck(entries):
    return {'name':'Generated card regression','cards':[{'name':n,'grpId':IDS[n],'quantity':q} for n,q in entries]}

engine=Engine.from_config(Path(__file__).with_name('launch.json'))
native=subprocess.Popen(['dotnet',str(ROOT/'protocol-host/bin/TargetAudit/ProtocolHost.dll'),'--flow-probe'],
    stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf8',cwd=ROOT)
seen=set();sacrificing={};cursor=0;previous=None;life_before=None
try:
    engine.request('interactiveStart',decks=[deck([('Forest',26),('Tireless Provisioner',18),('Scute Swarm',16)]),deck([('Forest',60)])])
    deadline=time.monotonic()+180
    while time.monotonic()<deadline:
        engine.request('interactivePoll');s=engine.request('interactiveSnapshot',afterPlaybackId=cursor);cursor=s['playbackId']
        native.stdin.write(json.dumps(s)+'\n');native.stdin.flush();p=json.loads(native.stdout.readline())
        cards={c['instanceId']:c for c in s['cards']};d=s['input'];kind=d['type'];event=s['buttonEventId']
        state=(kind,s['turn']['number'],tuple(sorted(seen)))
        if state!=previous:print('Token flow:',state,flush=True);previous=state
        ours=[c for c in cards.values() if c['zone']=='Battlefield' and c['controllerSeat']==1]
        tokens=[c for c in ours if c.get('isToken')]
        for token in tokens:
            native_token=next(t for t in p['tokens'] if t['id']==token['instanceId'])
            assert native_token['grpId']==token['grpId'] and native_token['zone']==30
            assert native_token['abilities'] or token['name'].removesuffix(' Token')=='Insect'
            seen.add(token['name'].removesuffix(' Token')+' visible')
        if 'TokenCreated' in p['annotations']:seen.add('creation animation')
        if 'TokenDeleted' in p['annotations']:seen.add('sacrifice animation')
        for name,identifier in sacrificing.items():
            if identifier not in cards:
                seen.add(name+' sacrificed')
                if name=='Treasure' and any(v['count'] for v in s['players'][0]['manaPool']):seen.add('Treasure mana')
                if name=='Food' and life_before is not None and s['players'][0]['life']==life_before+3:seen.add('Food life')
        expected={'Treasure visible','Food visible','Insect visible','Treasure sacrificed','Food sacrificed',
            'Treasure mana','Food life','creation animation','sacrifice animation'}
        if expected<=seen:
            print('PASS:',', '.join(sorted(seen)),flush=True);break
        assert s['omittedUnmappedCards']==0,('Unmapped token count',s['omittedUnmappedCards'])
        if not event or kind in ('','InputLockUI'):time.sleep(.02);continue
        if kind.startswith('InputGui'):
            assert kind=='InputGuiChoice',d
            options=d['options'];desired='Food' if 'Treasure visible' in seen else 'Treasure'
            chosen=next((o for o in options if desired in o['label']),None)
            answer=[chosen['id']] if chosen else [o['id'] for o in options[:max(1,d['min'])]]
            engine.request('interactiveAnswer',eventId=event,answer=answer);time.sleep(.02);continue
        if kind=='InputChooseNumber':
            # Color allocations can use bounded scalar numeric prompts.
            engine.request('interactiveAnswer',eventId=event,answer=d['max']);time.sleep(.02);continue
        action={'type':'ok','eventId':event}
        if kind=='InputPassPriority':
            names={c['name'] for c in ours if not c.get('isToken')}
            actions=[a for a in s['legalActions'] if a.get('highlight',3)==3]
            options=[a for a in actions if a['cardName'].removesuffix(' Token') in ('Treasure','Food')
                and a['cardName'].removesuffix(' Token') not in sacrificing]
            if not options:
                options=[a for a in actions if a['isLand'] or a['cardName'] in ('Tireless Provisioner','Scute Swarm')
                    and a['cardName'] not in names]
                options.sort(key=lambda a:(a['cardName']!='Tireless Provisioner',a['cardName']!='Scute Swarm',not a['isLand']))
            if options:
                a=options[0];action.update(type='playAbility',abilityId=a['abilityId'])
                name=a['cardName'].removesuffix(' Token')
                if name in ('Treasure','Food'):
                    match=next(n for n in p['actions'] if n['id']==a['instanceId'] and n['type']==a['actionType'])
                    assert match['ability']>0,('Token mana/rules ability identity missing',a)
                    sacrificing[name]=a['instanceId']
                    if name=='Food':life_before=s['players'][0]['life']
        elif kind in ('InputAttack','InputBlock'):
            action.update(type='attackers' if kind=='InputAttack' else 'blockers',assignments=[])
        elif kind in ('InputSelectCardsFromList','InputSelectEntitiesFromList'):
            action.update(type='selectMany',instanceIds=d['choices'][:d['min']])
        try:engine.request('interactiveAction',**action)
        except EngineError as error:
            if 'Stale human action' not in str(error):raise
        time.sleep(.02)
    else:raise AssertionError('Missing token flows: '+str(expected-seen))
except Exception:
    if 's' in globals():(ROOT/'analysis/token-failure.json').write_text(json.dumps(s),encoding='utf8')
    raise
finally:
    native.terminate();native.wait(timeout=5);engine.close()
