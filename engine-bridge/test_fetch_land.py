"""Exercise the fetch-land confirmation and search without hiding decisions."""
import json,sys,time,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from server.engine import Engine,EngineError
ids={c['name']:c['grpId'] for c in json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf8'))}
fetch_name=next((a for a in sys.argv[1:] if not a.startswith('--')),'Terramorphic Expanse')
deck={'name':'Fetch regression','cards':[{'name':n,'grpId':ids[n],'quantity':q} for n,q in [(fetch_name,30),('Forest',30)]]}
engine=Engine.from_config(Path(__file__).with_name('launch.json'))
activated=False;confirmed=False;search_selected=False;activated_id=None;previous=None
cursor=0;playback=[];native_seen=set()
native=subprocess.Popen(['dotnet',str(ROOT/'protocol-host/bin/TargetAudit/ProtocolHost.dll'),'--flow-probe'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf8',cwd=ROOT) if '--native' in sys.argv else None
try:
 engine.request('interactiveStart',decks=[deck,{'name':'Passive','cards':[{'name':'Forest','grpId':ids['Forest'],'quantity':60}]}])
 end=time.monotonic()+90
 while time.monotonic()<end:
  p=engine.request('interactivePoll');s=engine.request('interactiveSnapshot',afterPlaybackId=cursor);cursor=s['playbackId'];playback.extend(s['playbackEvents']);d=s['input'];kind=d['type']
  presentation=None
  if native:
   native.stdin.write(json.dumps(s)+'\n');native.stdin.flush();presentation=json.loads(native.stdout.readline())
   if presentation['abilities']>0:native_seen.add('abilityObject')
   native_seen.update(set(presentation['annotations']) & {'AbilityInstanceCreated','ResolutionStart','ResolutionComplete'})
  if kind!=previous:print('Input',d,flush=True);previous=kind
  if kind=='InputGuiChoice' and p['pending']:
   assert d.get('nativeKind')=='search' and d.get('options'), 'Missing native search candidates'
   engine.request('interactiveAnswer',eventId=p['pending'][0]['eventId'],answer=[d['options'][0]['id']])
   search_selected=True
   time.sleep(.03);continue
  if p['pending']:
   raise AssertionError(f"Untranslated search prompt: {[e for e in p['events'] if e['blocking']] or p['pending']}")
  if activated and any(c['grpId']==ids['Forest'] and c['zone']=='Battlefield' and c['ownerSeat']==1 and c['tapped'] for c in s['cards']):
   assert search_selected, 'No library card selected'
   assert any(c['instanceId']==activated_id and c['zone']=='Graveyard' for c in s['cards']), 'Fetch source was not sacrificed'
   assert any(e['type']=='shuffle' and e['playerId']==1 for e in playback), 'Fetch shuffle playback missing'
   if native:assert {'abilityObject','AbilityInstanceCreated','ResolutionStart','ResolutionComplete'} <= native_seen, f'Missing native ability playback: {native_seen}'
   print('PASS: fetch activation, actual sacrifice, native search, selected basic land enters tapped, shuffle playback');break
  event=s['buttonEventId']
  if not event or kind=='InputLockUI':time.sleep(.03);continue
  action={'type':'ok','eventId':event}
  if presentation and presentation['shouldPass']:
   pass
  elif kind=='InputPassPriority':
   choices=[a for a in s['legalActions'] if a.get('highlight',3)==3 and a['grpId']==ids[fetch_name]]
   if choices:action.update(type='playAbility',abilityId=choices[0]['abilityId'])
  elif kind=='InputConfirm' and not d['startingPlayer']:
   assert 'sacrifice' in d['message'].lower() and d['sourceId']>0;confirmed=True
  elif kind in ('InputSelectCardsFromList','InputSelectEntitiesFromList'):
   action.update(type='selectMany',instanceIds=d['choices'][:d['min']])
  try:
   engine.request('interactiveAction',**action)
   if kind=='InputPassPriority' and action['type']=='playAbility' and choices[0]['actionType']==2:
    activated=True;activated_id=choices[0]['instanceId']
  except EngineError as e:
   if 'Stale human action' not in str(e):raise
  time.sleep(.05)
 else:raise AssertionError('Fetch land stalled')
finally:
 if native:native.terminate();native.wait(timeout=5)
 engine.close()
