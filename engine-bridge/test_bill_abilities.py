"""Bill's landfall targeting and counter-doubling activation stay distinct."""
import json, subprocess, sys, time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from server.engine import Engine, EngineError
ids={c['name']:c['grpId'] for c in json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'))}
def deck(entries):return {'name':'Bill ability regression','cards':[{'name':n,'grpId':ids[n],'quantity':q} for n,q in entries]}
engine=Engine.from_config(Path(__file__).with_name('launch.json'))
native=subprocess.Popen(['dotnet',str(ROOT/'protocol-host/bin/TargetAudit/ProtocolHost.dll'),'--flow-probe'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf8',cwd=ROOT)
seen=set();cursor=0;activated=False;counts_before=None
try:
 engine.request('interactiveStart',decks=[deck([('Forest',28),('Bristly Bill, Spine Sower',8),('Traveling Chocobo',24)]),deck([('Forest',60)])])
 deadline=time.monotonic()+150
 while time.monotonic()<deadline:
  poll=engine.request('interactivePoll');s=engine.request('interactiveSnapshot',afterPlaybackId=cursor);cursor=s['playbackId']
  assert sorted(p['seat'] for p in s['players'])==[1,2],f'Player seats changed: {s["players"]}'
  native.stdin.write(json.dumps(s)+'\n');native.stdin.flush();line=native.stdout.readline()
  if not line:
   (ROOT/'analysis/bill-probe-failure.json').write_text(json.dumps(s),encoding='utf8')
   raise AssertionError('Native probe exited; snapshot saved to analysis/bill-probe-failure.json')
  p=json.loads(line)
  cards={c['instanceId']:c for c in s['cards']};d=s['input'];kind=d['type']
  bills=[c for c in cards.values() if c['zone']=='Battlefield' and c['ownerSeat']==1 and c['grpId']==ids['Bristly Bill, Spine Sower']]
  counts={c['instanceId']:next((n['count'] for n in c['counters'] if n['type']=='P1P1'),0) for c in bills}
  if counts_before and all(counts.get(k)==2*v for k,v in counts_before.items()):seen.add('doubledCounters')
  for a in p['actions']:
   if a['type']==2 and cards[a['id']]['grpId']==ids['Bristly Bill, Spine Sower']:
    assert a['ability']==171898 and a['source']==a['id'],f'Bill activation menu uses wrong ability: {a}'
    seen.add('activationMenu')
  for a in p['stackAbilities']:
   if cards[a['parent']]['grpId']==ids['Bristly Bill, Spine Sower']:
    assert a['ability'] in (171897,171898),f'Bill stack has wrong ability: {a}'
    if a['ability']==171898:seen.add('activationStack')
  if {'landfallTarget','activationMenu','activationStack','doubledCounters'}<=seen:
   print('PASS:',', '.join(sorted(seen)),flush=True);break
  if kind=='InputGuiChoice' and poll['pending']:
   options=d.get('options',[])
   assert options, 'Labeled ability decision has no options: '+str(d)
   engine.request('interactiveAnswer',eventId=poll['pending'][0]['eventId'],answer=[o['id'] for o in options[:max(1,d.get('min',0))]])
   time.sleep(.03);continue
  if poll['pending']:raise AssertionError('Unhandled ability menu '+str(poll['pending']))
  event=s['buttonEventId']
  if not event or kind=='InputLockUI':time.sleep(.02);continue
  action={'type':'ok','eventId':event}
  if kind=='InputSelectTargets':
   if 'landfallTarget' not in seen:(ROOT/'analysis/bill-target-snapshot.json').write_text(json.dumps(s),encoding='utf8')
   assert not activated,'Bill counter-doubling activation incorrectly asked for a target'
   assert p['targetingAbility']==171897 and p['targetingSource']==d['sourceId'],'Bill landfall targeting menu uses wrong ability'
   assert p['targetingPrompt'] in (1463, 1629),'Bill landfall did not use the creature targeting prompt'
   seen.add('landfallTarget');target=next(t for t in d['targets'] if t in {c['instanceId'] for c in bills})
   action.update(type='selectTarget',instanceId=target)
  elif kind=='InputPassPriority' and not p['shouldPass']:
   activations=[a for a in s['legalActions'] if a.get('highlight',3)==3 and a['actionType']==2 and a['grpId']==ids['Bristly Bill, Spine Sower']]
   if not activated and activations and any(counts.values()) and not s['stack'] and 'landfallTarget' in seen:
    counts_before=counts.copy();activated=True;action.update(type='playAbility',abilityId=activations[0]['abilityId'])
   elif not activated:
    has_chocobo=any(c['zone']=='Battlefield' and c['ownerSeat']==1 and c['grpId']==ids['Traveling Chocobo'] for c in cards.values())
    options=[a for a in s['legalActions'] if a.get('highlight',3)==3 and a['actionType'] in (1,3) and (a['isLand']
     or a['grpId']==ids['Bristly Bill, Spine Sower'] and not bills
     or a['grpId']==ids['Traveling Chocobo'] and not has_chocobo)]
    options.sort(key=lambda a:(not a['isLand'],a['grpId']!=ids['Bristly Bill, Spine Sower']))
    if options:action.update(type='playAbility',abilityId=options[0]['abilityId'])
  elif kind in ('InputAttack','InputBlock'):action.update(type='attackers' if kind=='InputAttack' else 'blockers',assignments=[])
  elif kind in ('InputSelectCardsFromList','InputSelectEntitiesFromList'):action.update(type='selectMany',instanceIds=d['choices'][:d['min']])
  try:engine.request('interactiveAction',**action)
  except EngineError as error:
   if 'Stale human action' not in str(error):raise
  time.sleep(.03)
 else:raise AssertionError('Missing Bill flows '+str(seen))
 engine.request('interactiveAction',type='concede')
 deadline=time.monotonic()+10
 while time.monotonic()<deadline:
  engine.request('interactivePoll');s=engine.request('interactiveSnapshot',afterPlaybackId=cursor)
  if s.get('winnerSeat')==2:
   assert sorted(p['seat'] for p in s['players'])==[1,2],'Defeated human changed the winner seat'
   assert s['loserSeat']==1 and s['lossReason']=='Conceded','Concede outcome mapped to wrong player'
   native.stdin.write(json.dumps(s)+'\n');native.stdin.flush()
   assert native.stdout.readline(),'Native encoder rejected the AI victory snapshot'
   print('PASS: AI victory keeps registered seats 1 and 2',flush=True);break
  time.sleep(.02)
 else:raise AssertionError('Concede outcome did not arrive')
finally:
 native.terminate();native.wait(timeout=5);engine.close()
