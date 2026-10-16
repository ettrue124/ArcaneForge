"""Real Bill landfall targeting, distinct native trigger stack and resolution."""
import json,subprocess,sys,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from server.engine import Engine,EngineError
ids={c['name']:c['grpId'] for c in json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'))}
def deck(entries):return {'name':'Landfall stack regression','cards':[{'name':n,'grpId':ids[n],'quantity':q} for n,q in entries]}
engine=Engine.from_config(Path(__file__).with_name('launch.json'))
native=subprocess.Popen(['dotnet',str(ROOT/'protocol-host/bin/TargetAudit/ProtocolHost.dll'),'--flow-probe'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf8',cwd=ROOT)
cursor=0;seen=set();resolving=[];created=set();selected=set();counter_counts={}
try:
 engine.request('interactiveStart',decks=[deck([('Forest',28),('Bristly Bill, Spine Sower',8),('Traveling Chocobo',12),('Mossborn Hydra',12)]),deck([('Forest',60)])])
 deadline=time.monotonic()+150
 while time.monotonic()<deadline:
  poll=engine.request('interactivePoll');s=engine.request('interactiveSnapshot',afterPlaybackId=cursor);cursor=s['playbackId']
  native.stdin.write(json.dumps(s)+'\n');native.stdin.flush();p=json.loads(native.stdout.readline())
  cards={c['instanceId']:c for c in s['cards']};d=s['input'];kind=d['type']
  expected=[e['instanceId'] for e in s['stack'] if not e['isAbility'] or any(a['id']==e['instanceId'] for a in p['stackAbilities'])]
  # Forge can put a spell's card in Stack while its cast/payment is still in
  # progress, before creating the actual stack instance. Preserve that card too.
  casting=[c['instanceId'] for c in s['cards'] if c['zone']=='Stack' and c['instanceId'] not in expected]
  assert p['stack']==expected+casting, 'Native stack reordered actual Forge stack: '+str((p['stack'],expected,casting,kind))
  for ability in p['stackAbilities']:
   if cards[ability['parent']]['grpId']==ids['Bristly Bill, Spine Sower']:
    assert ability['ability']==171897 and ability['controller']==1,'Wrong landfall ability or player on stack'
    seen.add('billStackObject');created.add(ability['id'])
  if len(p['stackAbilities'])>1:seen.add('multipleStackObjects')
  for entry in s['playbackEvents']:
   if entry['type']=='resolutionStart':resolving.append(entry['sourceId'])
   if entry['type']=='resolutionComplete':
    assert resolving.pop()==entry['sourceId'],'Stack resolved out of order'
    if entry['sourceId'] in created:seen.add('billResolution')
  if 'CounterAdded' in p['annotations'] and any(c['target'] in selected for c in p['counters']):seen.add('selectedCounter')
  if {'targetingAbility','billStackObject','multipleStackObjects','billResolution','selectedCounter'}<=seen:
   print('PASS:',', '.join(sorted(seen)),flush=True);break
  if kind=='InputGuiChoice' and poll['pending']:
   options=d.get('options',[])
   assert options, 'Labeled decision has no options: '+str(d)
   engine.request('interactiveAnswer',eventId=poll['pending'][0]['eventId'],answer=[o['id'] for o in options[:max(1,d.get('min',0))]])
   time.sleep(.03);continue
  if poll['pending']:raise AssertionError('Unhandled prompt '+str(poll['pending']))
  event=s['buttonEventId']
  if not event or kind=='InputLockUI':time.sleep(.02);continue
  action={'type':'ok','eventId':event}
  if kind=='InputSelectTargets':
   if cards[d['sourceId']]['grpId']==ids['Bristly Bill, Spine Sower']:
    assert p['targetingAbility']==171897 and p['targetingSource']==d['sourceId'],'Landfall target request uses wrong source/ability'
    seen.add('targetingAbility')
   target=next(t for t in d['targets'] if cards.get(t,{}).get('ownerSeat')==1)
   selected.add(target);action.update(type='selectTarget',instanceId=target)
  elif kind=='InputPassPriority' and not p['shouldPass']:
   options=[a for a in s['legalActions'] if a.get('highlight',3)==3 and a['actionType'] in (1,3)]
   has_bill=any(c['zone']=='Battlefield' and c['ownerSeat']==1 and c['grpId']==ids['Bristly Bill, Spine Sower'] for c in s['cards'])
   options.sort(key=lambda a:(not a['isLand'],a['grpId']!=ids['Bristly Bill, Spine Sower'] if not has_bill else a['grpId']!=ids['Traveling Chocobo']))
   if options:action.update(type='playAbility',abilityId=options[0]['abilityId'])
  elif kind=='InputAttack':action.update(type='attackers',assignments=[])
  elif kind=='InputBlock':action.update(type='blockers',assignments=[])
  elif kind in ('InputSelectCardsFromList','InputSelectEntitiesFromList'):action.update(type='selectMany',instanceIds=d['choices'][:d['min']])
  try:engine.request('interactiveAction',**action)
  except EngineError as error:
   if 'Stale human action' not in str(error):raise
  time.sleep(.03)
 else:raise AssertionError('Missing flows '+str(seen))
finally:
 native.terminate();native.wait(timeout=5);engine.close()
