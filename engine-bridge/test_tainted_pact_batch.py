"""Verify batch decline follows real Tainted Pact stopping rules in isolated games."""
import json,sys,time
from collections import Counter
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT))
from server.engine import Engine,EngineRejected
from server.feature_lab import LANDS
catalogue=json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'));byname={c['name']:c['grpId'] for c in catalogue}
for library in [['Memnite','Ornithopter','Opt','Divination','Serra Angel'],['Forest']*6]:
 setups=[dict(hand=['Tainted Pact']*2,battlefield=LANDS,loyalty=0),dict(hand=[],battlefield=[],loyalty=0)]
 decks=[]
 for i,setup in enumerate(setups):
  names=setup['hand']+setup['battlefield']+(library if i==0 else ['Forest']*30)
  decks.append(dict(name='Pact batch audit',format='Historic',cards=[dict(name=n,quantity=q,grpId=byname[n]) for n,q in Counter(names).items()]))
 engine=Engine.from_config(Path(__file__).with_name('launch.json'));casts=0;prompts=0;done=False
 try:
  engine.request('interactiveStart',decks=decks,featureScenario=dict(id='pact-batch-audit',players=setups))
  deadline=time.monotonic()+100
  while time.monotonic()<deadline:
   engine.request('interactivePoll');s=engine.request('interactiveSnapshot');d=s['input'];kind=d['type'];event=s['buttonEventId']
   if not event or kind in ('','InputLockUI'):time.sleep(.02);continue
   stack=s.get('stack',[]);action=dict(type='ok',eventId=event)
   if kind=='InputPassPriority' and not stack:
    if casts:
     remaining=[c for c in s['cards'] if c['ownerSeat']==1 and c['zone']=='Library']
     if len(set(library))>1:
      assert not remaining and prompts==1,('Batch did not exhaust singleton library',len(remaining),prompts);done=True;break
     if casts==1:
      assert len(remaining)==len(library)-2 and prompts==1,('Duplicate did not stop batch',len(remaining),prompts)
     else:
      assert prompts==2,'Decline-all leaked into another spell resolution';done=True;break
    a=next((a for a in s['legalActions'] if a.get('cardName')=='Tainted Pact' and a.get('highlight',3)==3),None)
    if a:casts+=1;action.update(type='playAbility',abilityId=a['abilityId'])
   elif kind=='InputGuiChoice' and d.get('title','').startswith('Tainted Pact:'):
    prompts+=1;label='No to all remaining' if casts==1 else 'Put this card into hand'
    option=next(o for o in d['options'] if o['label']==label)
    engine.request('interactiveAnswer',eventId=event,answer=[option['id']]);continue
   elif kind=='InputAttack':action.update(type='attackers',assignments=[])
   elif kind=='InputBlock':action.update(type='blockers',assignments=[])
   elif kind.startswith('InputGui'):
    engine.request('interactiveAnswer',eventId=event,answer=[o['id'] for o in d.get('options',[])[:max(1,d.get('min',0))]]);continue
   try:engine.request('interactiveAction',**action)
   except EngineRejected as e:
    if not any(x in str(e) for x in ('Stale human action','Previous human action still running')):raise
   time.sleep(.02)
  assert done,('Pact batch stalled',d)
  print('PASS:', 'one batch decline exhausts a singleton library' if len(set(library))>1 else 'duplicate names stop batch; next Pact resolution offers choices again')
 finally:engine.close()
