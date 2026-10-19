"""Real representative loyalty activations against the prepared planeswalker pack."""
import json,sys,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT))
from server.engine import Engine,EngineRejected
from server.feature_lab import SCENARIOS,prepare
catalogue={c['grpId']:c for c in json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'))}
for r in json.loads((ROOT/'data/magic-import/manifest.json').read_text(encoding='utf8')):catalogue.setdefault(r['grpId'],dict(name=r['name']))
engine=Engine.from_config(Path(__file__).with_name('launch.json'))
try:
 for name,cost,start in [('Garruk Wildspeaker','+1',3),('Garruk Wildspeaker','-1',3),('Garruk Wildspeaker','-4',4),('Tezzeret the Seeker','-X',4)]:
  SCENARIOS['pw-audit']=dict(hand=[],battlefield=[name,'Grizzly Bears'],loyalty=start)
  decks,scenario=prepare('pw-audit',catalogue)
  ring=next(g for g,c in catalogue.items() if c['name']=='Sol Ring');decks[0]['cards'].append(dict(name='Sol Ring',quantity=1,grpId=ring))
  engine.request('interactiveStart',decks=decks,featureScenario=scenario)
  used=False;done=False;x_seen=False;deadline=time.monotonic()+100
  while time.monotonic()<deadline:
   engine.request('interactivePoll');s=engine.request('interactiveSnapshot');d=s['input'];kind=d['type'];event=s['buttonEventId']
   if not event or kind in ('','InputLockUI'):time.sleep(.02);continue
   action=dict(type='ok',eventId=event)
   if kind=='InputPassPriority' and not s.get('stack') and s['turn']['activeSeat']==1 and s['turn']['phase'] in ('MAIN1','MAIN2'):
    if used:
     walker=next(c for c in s['cards'] if c['name']==name and c['ownerSeat']==1)
     loyalty=next((c['count'] for c in walker.get('counters',[]) if c['type']=='LOYALTY'),0)
     expected={'+1':4,'-1':2,'-4':0,'-X':2}[cost];assert loyalty==expected,(name,cost,loyalty)
     assert not any(a.get('cardName')==name and a.get('highlight',3)==3 for a in s['legalActions']),'Loyalty ability offered twice in same turn'
     if cost=='-1':assert any(c['name'] in ('Beast','Beast Token') and c['zone']=='Battlefield' and c.get('power')==3 for c in s['cards']),([(c.get('name'),c.get('zone'),c.get('power')) for c in s['cards'] if c.get('isToken')],s.get('omittedUnmappedCards'))
     if cost=='-4':
      bear=next(c for c in s['cards'] if c['name']=='Grizzly Bears' and c['zone']=='Battlefield');assert bear['power']==5 and bear['toughness']==5 and 'Trample' in bear['grantedKeywords']
     if cost=='-X':assert x_seen and any(c['name']=='Sol Ring' and c['zone']=='Battlefield' for c in s['cards'])
     done=True;break
    a=next((a for a in s['legalActions'] if a.get('cardName')==name and a.get('abilityIdentity',{}).get('loyaltyCost')==cost and a.get('highlight',3)==3),None)
    assert a,(name,cost,'No legal activation',[(a.get('description'),a.get('manaCost'),a.get('abilityIdentity')) for a in s['legalActions'] if a.get('cardName')==name])
    used=True;action.update(type='playAbility',abilityId=a['abilityId'])
   elif kind=='InputSelectTargets':
    targets=[c['instanceId'] for c in s['cards'] if c['instanceId'] in d['targets'] and c.get('isLand')]
    action.update(type='targets',instanceIds=targets[:d['min']])
   elif kind=='InputChooseNumber':x_seen=True;engine.request('interactiveAnswer',eventId=event,answer=2);continue
   elif kind.startswith('InputGui'):
    options=d.get('options',[]);chosen=next((o for o in options if 'Sol Ring' in o.get('label','')),None)
    engine.request('interactiveAnswer',eventId=event,answer=[chosen['id']] if chosen else [o['id'] for o in options[:max(1,d.get('min',0))]]);continue
   elif kind=='InputAttack':action.update(type='attackers',assignments=[])
   elif kind=='InputBlock':action.update(type='blockers',assignments=[])
   try:engine.request('interactiveAction',**action)
   except EngineRejected as e:
    if not any(x in str(e) for x in ('Stale human action','Previous human action still running')):raise
   time.sleep(.02)
  assert done,('Planeswalker check stalled',name,cost,d)
  print('PASS:',name,cost,'loyalty payment, effect resolution and once-per-turn limit',flush=True)
  engine.request('interactiveClose')
finally:engine.close()
