"""Exercise real storm casts in an isolated Forge session, leaving the live match alone."""
import json,sys,time,subprocess,atexit
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from server.engine import Engine,EngineRejected
from server.feature_lab import SCENARIOS,prepare

catalogue={c['grpId']:c for c in json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'))}
native=subprocess.Popen(['dotnet',str(ROOT/'protocol-host/bin/ReadableStackTargets/ProtocolHost.dll'),'--flow-probe'],
 stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf8',cwd=ROOT)
def close_native():
 native.terminate();native.wait(timeout=5)
atexit.register(close_native)
def check_native(s):
 native.stdin.write(json.dumps(s)+'\n');native.stdin.flush();view=json.loads(native.stdout.readline())
 for entry in s.get('stack',[]):
  assert entry['instanceId'] in view['stack'],'Arena omitted a storm stack object'
  if entry.get('description','').startswith('Storm ('):
   assert any(a['id']==entry['instanceId'] and a['ability'] for a in view['stackAbilities']),'Storm trigger has no Arena ability identity'
 if s['input']['type']=='InputGuiStackTargets' and s['input'].get('readableStackTargets'):
  assert view['requestType']=='SelectNReq',('Missing readable stack target list',view['requestType'])
 elif s['input']['type'] in ('InputSelectTargets','InputGuiStackTargets'):
  assert view['requestType']=='SelectTargetsReq',('Wrong Arena target workflow',view['requestType'])
  if not view['targetingSource'] or not view['targetingAbility']:
   (ROOT/'analysis/storm-target-failure.json').write_text(json.dumps(s),encoding='utf8')
   raise AssertionError(('Missing Arena target source/ability',s['input'],view['targetingSource'],view['targetingAbility'],[(c['instanceId'],c.get('grpId'),c.get('zone')) for c in s['cards'] if c['instanceId']==s['input']['sourceId']]))
  if s['input'].get('sourceIsSpellCopy'):
   assert any(c['id']==s['input']['sourceId'] for c in view['stats']),'Pending copy has no Arena card preview'
SCENARIOS['storm-audit']=dict(hand=['Memnite','Ornithopter','Grapeshot'],battlefield=[])
decks,scenario=prepare('storm-audit',catalogue)
engine=Engine.from_config(Path(__file__).with_name('launch.json'))
cast=[];copies_seen=0;trigger_seen=False;retargets=0;last=None
try:
 engine.request('interactiveStart',decks=decks,featureScenario=scenario)
 deadline=time.monotonic()+120
 while time.monotonic()<deadline:
  engine.request('interactivePoll');s=engine.request('interactiveSnapshot');last=s
  check_native(s)
  stack=s.get('stack',[])
  trigger_seen |= any(e.get('description','').startswith('Storm (') for e in stack)
  spells=[e for e in stack if e.get('cardName')=='Grapeshot' and not e.get('isAbility')]
  copies_seen=max(copies_seen,len(spells)-1)
  if len(spells)>1:
   assert len({e['instanceId'] for e in spells})==len(spells),'Storm copies share stack identities'
   assert all(e['sourceGrpId'] for e in spells),'Storm copy has no Arena printing'
  if 'Grapeshot' in cast and not stack and s['input']['type']=='InputPassPriority':
   assert trigger_seen,'No storm trigger on stack'
   assert copies_seen==2,('Wrong storm count',copies_seen)
   assert next(p['life'] for p in s['players'] if p['seat']==2)==17,'Grapeshot copies did not resolve separately'
   assert retargets==3,('Original and both copies must offer targets',retargets)
   print('PASS: two earlier spells produce two distinct mapped copies; trigger visible; each copy can retarget; all three damage spells resolve.')
   break
  d=s['input'];kind=d['type'];event=s['buttonEventId']
  if not event or kind in ('','InputLockUI'):time.sleep(.02);continue
  action=dict(type='ok',eventId=event)
  if kind=='InputPassPriority' and not stack and len(cast)<3:
   name=['Memnite','Ornithopter','Grapeshot'][len(cast)]
   a=next((a for a in s['legalActions'] if a.get('cardName')==name and a.get('highlight',3)==3),None)
   if a:cast.append(name);action.update(type='playAbility',abilityId=a['abilityId'])
  elif kind=='InputSelectTargets':
   assert 2 in d['targets'],d
   if 'Grapeshot' in cast:retargets+=1
   action.update(type='targets',instanceIds=[2])
  elif kind=='InputAttack':action.update(type='attackers',assignments=[])
  elif kind=='InputBlock':action.update(type='blockers',assignments=[])
  elif kind.startswith('InputGui'):
   print('GUI',json.dumps(d)[:500],flush=True)
   engine.request('interactiveAnswer',eventId=event,answer=[o['id'] for o in d.get('options',[])[:max(1,d.get('min',0))]])
   continue
  try:engine.request('interactiveAction',**action)
  except EngineRejected as e:
   if not any(x in str(e) for x in ('Stale human action','Previous human action still running')):raise
  time.sleep(.02)
 else:raise AssertionError('Storm flow stalled: '+str(cast)+' '+json.dumps(last.get('input')))
finally:engine.close()

# Counterspell copies use stack targets rather than creature/player targeting.
close_native()
native=subprocess.Popen(['dotnet',str(ROOT/'protocol-host/bin/ReadableStackTargets/ProtocolHost.dll'),'--flow-probe'],
 stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf8',cwd=ROOT)
SCENARIOS['storm-counter-audit']=dict(hand=['Memnite','Ornithopter','Divination','Opt','Flusterstorm'],battlefield=[])
decks,scenario=prepare('storm-counter-audit',catalogue)
engine=Engine.from_config(Path(__file__).with_name('launch.json'))
cast=[];peak=0;copy_targets=0;chosen=set();last=None
try:
 engine.request('interactiveStart',decks=decks,featureScenario=scenario)
 deadline=time.monotonic()+120
 while time.monotonic()<deadline:
  engine.request('interactivePoll');s=engine.request('interactiveSnapshot');last=s
  check_native(s)
  stack=s.get('stack',[])
  flusters=[e for e in stack if e.get('cardName')=='Flusterstorm' and not e.get('isAbility')]
  peak=max(peak,len(flusters))
  if len(flusters)>1:
   assert len({e['instanceId'] for e in flusters})==len(flusters),'Counterspell copies share identities'
   assert all(e['sourceGrpId'] for e in flusters),'Counterspell copy has no native printing'
  if 'Flusterstorm' in cast and not stack and s['input']['type']=='InputPassPriority':
   assert peak==5,('Four earlier spells should make four copies',peak)
   assert copy_targets>=5,('Copies did not offer stack targeting',copy_targets)
   assert len(chosen)==2,'Copies could not choose different targets'
   assert all(any(c['name']==name and c['zone']=='Graveyard' for c in s['cards']) for name in ['Divination','Opt'])
   print('PASS: Flusterstorm after four earlier spells creates four distinct copies; copies can select different stack targets; declining payment counters both selected spells.')
   break
  d=s['input'];kind=d['type'];event=s['buttonEventId']
  if not event or kind in ('','InputLockUI'):time.sleep(.02);continue
  action=dict(type='ok',eventId=event)
  if kind=='InputPassPriority' and len(cast)<5:
   name=['Memnite','Ornithopter','Divination','Opt','Flusterstorm'][len(cast)]
   ready=not stack or name in ('Opt','Flusterstorm')
   a=next((a for a in s['legalActions'] if a.get('cardName')==name and a.get('highlight',3)==3),None) if ready else None
   if a:cast.append(name);action.update(type='playAbility',abilityId=a['abilityId'])
  elif kind=='InputGuiStackTargets':
   eligible=[e['instanceId'] for e in stack if e.get('cardName') in ('Divination','Opt') and e['instanceId'] in d['targets']]
   if d.get('finish'):
    answer=[]
   else:
    assert eligible,d
    target=eligible[copy_targets%len(eligible)];chosen.add(target);copy_targets+=1;answer=[target]
   engine.request('interactiveAnswer',eventId=event,answer=answer);continue
  elif kind=='InputConfirm' and not d.get('startingPlayer'):action['type']='cancel'
  elif kind=='InputAttack':action.update(type='attackers',assignments=[])
  elif kind=='InputBlock':action.update(type='blockers',assignments=[])
  elif kind.startswith('InputGui'):
   options=d.get('options',[])
   decline=next((o for o in options if o.get('label') in ('Decline','No')),None)
   engine.request('interactiveAnswer',eventId=event,answer=[decline['id']] if decline else [o['id'] for o in options[:max(1,d.get('min',0))]])
   continue
  try:engine.request('interactiveAction',**action)
  except EngineRejected as e:
   if not any(x in str(e) for x in ('Stale human action','Previous human action still running')):raise
  time.sleep(.02)
 else:raise AssertionError('Flusterstorm flow stalled: '+str(cast)+' '+json.dumps(last.get('input')))
finally:engine.close()
