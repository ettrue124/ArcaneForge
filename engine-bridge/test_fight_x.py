"""Real Forge casts: select X, both fight/bite target groups, resolve damage."""
import json, subprocess, sys, time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from server.engine import Engine, EngineError
ids={c['name']:c['grpId'] for c in json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'))}
def deck(entries):return {'name':'Fight and X regression','cards':[{'name':n,'grpId':ids[n],'quantity':q} for n,q in entries]}
engine=Engine.from_config(Path(__file__).with_name('launch.json'))
native=subprocess.Popen(['dotnet',str(ROOT/'protocol-host/bin/TargetAudit/ProtocolHost.dll'),'--flow-probe'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf8',cwd=ROOT)
seen=set();cast=set();dead_before={};cursor=0;previous=None
try:
 engine.request('interactiveStart',decks=[deck([('Forest',26),('Questing Beast',18),('Primal Might',8),('Rabid Bite',8)]),deck([('Forest',30),('Wall of Blossoms',30)])])
 deadline=time.monotonic()+180
 while time.monotonic()<deadline:
  poll=engine.request('interactivePoll');s=engine.request('interactiveSnapshot',afterPlaybackId=cursor);cursor=s['playbackId']
  native.stdin.write(json.dumps(s)+'\n');native.stdin.flush();p=json.loads(native.stdout.readline())
  d=s['input'];kind=d['type'];cards={c['instanceId']:c for c in s['cards']}
  status=(kind,s['turn']['number'],d.get('targetIndex'))
  if status!=previous:print('Input:',status,flush=True);previous=status
  ours=[c for c in cards.values() if c['zone']=='Battlefield' and c['controllerSeat']==1 and c['grpId']==ids['Questing Beast']]
  theirs=[c for c in cards.values() if c['zone']=='Battlefield' and c['controllerSeat']==2 and c['grpId']==ids['Wall of Blossoms']]
  for name,target in dead_before.items():
   if cards.get(target,{}).get('zone')=='Graveyard':seen.add(name+' resolved')
  if any(c.get('power',0)>=6 for c in ours):seen.add('X=2 buff')
  if 2 in p.get('xValues',[]):seen.add('X=2 stack display')
  if {'Primal Might resolved','Rabid Bite resolved','X=2 buff','X=2 stack display'}<=seen:
   print('PASS:',', '.join(sorted(seen)),flush=True);break
  if kind=='InputChooseNumber':
   assert not p['shouldPass'] and p['numberRequest'], 'Numeric prompt was skipped'
   assert d['isX'] and d['sourceId']>0,'X prompt lost casting source'
   engine.request('interactiveAnswer',eventId=s['buttonEventId'],answer=2);seen.add('X chosen');time.sleep(.03);continue
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
   name=d['cardName'];index=d['targetIndex'];candidates=d['targets']
   assert p['targetingIndex']==index,'Native target group lost'
   target=next(t for t in candidates if cards[t]['controllerSeat']==(1 if index==0 else 2))
   if index==1:dead_before[name]=target
   seen.add(name+' target '+str(index));action.update(type='selectTarget',instanceId=target)
  elif kind=='InputPassPriority':
   options=[a for a in s['legalActions'] if a.get('highlight',3)==3 and a['actionType'] in (1,3)]
   # Ignore attacks to keep a stable creature available for both spells.
   options=[a for a in options if a['isLand'] or a['grpId']==ids['Questing Beast'] and not ours
    or a['cardName'] in ('Primal Might','Rabid Bite') and a['cardName'] not in cast and ours and theirs
      and len([c for c in cards.values() if c['zone']=='Battlefield' and c['ownerSeat']==1 and c['grpId']==ids['Forest'] and not c['tapped']])>=3]
   options.sort(key=lambda a:(not a['isLand'],a['cardName']!='Questing Beast'))
   if options:
    a=options[0];action.update(type='playAbility',abilityId=a['abilityId'])
    if a['cardName'] in ('Primal Might','Rabid Bite'):cast.add(a['cardName'])
   nonmana=[a for a in s['legalActions'] if a.get('highlight',3)==3 and a['actionType']!=4]
   if not nonmana:assert p['shouldPass'],'Unusable priority held'
  elif kind in ('InputAttack','InputBlock'):action.update(type='attackers' if kind=='InputAttack' else 'blockers',assignments=[])
  elif kind in ('InputSelectCardsFromList','InputSelectEntitiesFromList'):action.update(type='selectMany',instanceIds=d['choices'][:d['min']])
  try:engine.request('interactiveAction',**action)
  except EngineError as error:
   if 'Stale human action' not in str(error):raise
  time.sleep(.02)
 else:
  (ROOT/'analysis/fight-x-failure.json').write_text(json.dumps(s),encoding='utf8')
  raise AssertionError('Missing flows '+str(seen))
finally:
 native.terminate();native.wait(timeout=5);engine.close()
