import json,sys,time
from pathlib import Path
sys.path.insert(0,str(Path.cwd()))
from server.engine import Engine
c=json.loads(Path('data/catalogue.json').read_text(encoding='utf8')); ids={x['name']:x['grpId'] for x in c}
d={'name':'Mana regression','cards':[{'name':n,'grpId':ids[n],'quantity':q} for n,q in [('Forest',24),('Llanowar Elves',36)]]}
e=Engine.from_config(Path('engine-bridge/launch.json')); activated=False
try:
 e.request('interactiveStart',decks=[d,d]); end=time.monotonic()+80
 while time.monotonic()<end:
  p=e.request('interactivePoll');s=e.request('interactiveSnapshot'); k=s.get('input',{}).get('type');event=p['buttonEventId']
  if p['pending']:
   print('PENDING',p['pending'],[x for x in p['events'] if x['blocking']],flush=True);break
  if not event or k=='InputLockUI':time.sleep(.03);continue
  a={'type':'ok','eventId':event}
  if k=='InputPassPriority':
   options=s['legalActions']; lands=[x for x in options if x['isLand']];mana=[x for x in options if x['actionType']==4]
   if lands:a.update(type='playAbility',abilityId=lands[0]['abilityId'])
   elif mana and not activated:a.update(type='playAbility',abilityId=mana[0]['abilityId']);activated=True
   elif activated:print('AFTER MANA',k,'actions',options,flush=True);break
  print(k,a,flush=True)
  try:e.request('interactiveAction',**a)
  except Exception as err:print(str(err),flush=True)
  time.sleep(.1)
finally:e.close()

