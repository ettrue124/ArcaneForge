"""Real Forge MDFC/Adventure announcements and linked native card identities."""
import json
from pathlib import Path
import subprocess
import sys
import time

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from server.engine import Engine,EngineError
IDS={c['name']:c['grpId'] for c in json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'))}
engine=Engine.from_config(Path(__file__).with_name('launch.json'))

def deck(cards):
    return {'name':'Face regression','cards':[{'name':n,'grpId':IDS[n],'quantity':q} for n,q in cards]}

def run(entries,action_type,expected_face,zone):
    native=subprocess.Popen(['dotnet',str(ROOT/'protocol-host/bin/TargetAudit/ProtocolHost.dll'),'--flow-probe'],
        stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf8',cwd=ROOT)
    played=None;cursor=0
    try:
        engine.request('interactiveStart',decks=[deck(entries),deck([('Forest',60)])])
        deadline=time.monotonic()+150
        while time.monotonic()<deadline:
            engine.request('interactivePoll');s=engine.request('interactiveSnapshot',afterPlaybackId=cursor);cursor=s['playbackId']
            native.stdin.write(json.dumps(s)+'\n');native.stdin.flush();p=json.loads(native.stdout.readline())
            d=s['input'];kind=d['type'];event=s['buttonEventId']
            if played is not None:
                card=next(c for c in s['cards'] if c['instanceId']==played)
                if card['zone']==zone:
                    assert card['grpId']==expected_face,(card['name'],card['grpId'])
                    assert card['originalGrpId']!=card['grpId']
                    assert p['printedFaces'] and any(c['id']==played and c['grpId']==expected_face and c['source']==card['originalGrpId'] for c in p['printedFaces'])
                    print('PASS: native action',action_type,'and linked face',expected_face,zone,flush=True);return
            if not event or kind in ('','InputLockUI'):time.sleep(.02);continue
            if kind.startswith('InputGui'):
                assert kind=='InputGuiChoice',d
                engine.request('interactiveAnswer',eventId=event,answer=[o['id'] for o in d['options'][:d['min']]])
                time.sleep(.02);continue
            action={'type':'ok','eventId':event}
            if kind=='InputPassPriority':
                options=[a for a in s['legalActions'] if a.get('highlight',3)==3 and (a['actionType']==action_type or a['isLand'])]
                options.sort(key=lambda a:a['actionType']!=action_type)
                if options:
                    selected=options[0]
                    assert any(a['id']==selected['instanceId'] and a['type']==selected['actionType'] for a in p['actions'])
                    action.update(type='playAbility',abilityId=selected['abilityId'])
                    if selected['actionType']==action_type:played=selected['instanceId']
            elif kind=='InputAttack':action.update(type='attackers',assignments=[])
            elif kind=='InputBlock':action.update(type='blockers',assignments=[])
            elif kind in ('InputSelectCardsFromList','InputSelectEntitiesFromList'):action.update(type='selectMany',instanceIds=d['choices'][:d['min']])
            try:engine.request('interactiveAction',**action)
            except EngineError as error:
                if 'Stale human action' not in str(error):raise
            time.sleep(.02)
        raise AssertionError('Missing linked-face flow '+str(action_type))
    finally:
        native.terminate();native.wait(timeout=5);engine.request('interactiveClose')

try:
    run([('Jwari Disruption',60)],19,94291,'Battlefield')
    run([('Forest',36),('Beanstalk Giant',24)],16,70492,'Stack')
finally:
    engine.close()
