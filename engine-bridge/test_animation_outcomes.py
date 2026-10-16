"""Real Forge outcomes plus native wire checks in an isolated prepared match."""
import json, subprocess, sys, time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from server.engine import Engine, EngineRejected
from server.feature_lab import prepare

catalogue={c['grpId']:c for c in json.loads((ROOT/'data/catalogue.json').read_text(encoding='utf-8'))}
replacement_check='--replacements' in sys.argv
regeneration_check='--regeneration' in sys.argv
facedown_check='--facedown' in sys.argv
copy_check='--copies' in sys.argv
multistep_check='--multistep' in sys.argv
designation_check='--designations' in sys.argv
ability_change_check='--ability-changes' in sys.argv
color_check='--colors' in sys.argv
stat_check='--stats' in sys.argv
phasing_check='--phasing' in sys.argv
illegal_attachment_check='--illegal-attachments' in sys.argv
blocking_check='--blocking-restrictions' in sys.argv
decks,scenario=prepare('blocking-restrictions' if blocking_check else 'illegal-attachments' if illegal_attachment_check else 'combat' if phasing_check else 'temporary-stats' if stat_check else 'color-choice' if color_check else 'ability-changes' if ability_change_check else 'designations' if designation_check else 'multistep' if multistep_check else 'copies' if copy_check else 'facedown' if facedown_check else 'replacements' if replacement_check else 'effects',catalogue)
expected={'CopiedState'} if copy_check else {'FaceDownState','FaceUpState'} if facedown_check else {'PermanentRegenerated'} if regeneration_check else {'ReplacementEffectApplied','CounterAdded'} if replacement_check else {'Scry','DieRoll','CoinFlip'}
if multistep_check:expected={category+':'+str(starting) for category in ['Scry','Surveil','Explore','Proliferate','Connive'] for starting in (True,False)}
if designation_check:expected={'Monstrous','Monarch','CitysBlessing'}
if ability_change_check:expected={'SuppressedPrintedGranted'}
if color_check:expected={'ColorChoice'}
if stat_check:expected={'BoostedPower','ExpiredPower'}
if phasing_check:expected={'PhaseOutState','PhaseInState','PhasedOut','PhasedIn'}
if illegal_attachment_check:expected={'RemoveAttachment'}
if blocking_check:expected={'BlockingRestriction','BlockingRestrictionRemoved'}
phased_ids=set()
def option(name, default):
    return Path(sys.argv[sys.argv.index(name)+1]) if name in sys.argv else default
engine=Engine.from_config(option('--engine-config',Path(__file__).with_name('launch.json')))
native=subprocess.Popen(['dotnet',str(option('--host',ROOT/'protocol-host/bin/TargetAudit/ProtocolHost.dll')),'--flow-probe'],
    stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf-8',cwd=ROOT)
cursor=0;events=[];observed=set();cast=set();previous=None;fixtures=[]
face_down_id=None
try:
    engine.request('interactiveStart',decks=decks,featureScenario=scenario)
    deadline=time.monotonic()+120
    while time.monotonic()<deadline:
        poll=engine.request('interactivePoll')
        s=engine.request('interactiveSnapshot',afterPlaybackId=cursor)
        events.extend(s.get('playbackEvents',[]));cursor=s['playbackId']
        native.stdin.write(json.dumps(s)+'\n');native.stdin.flush()
        presentation=json.loads(native.stdout.readline())
        observed.update(presentation['annotations'])
        if blocking_check:
            beast=next((c for c in s['cards'] if c['name']=='Questing Beast' and c['zone']=='Battlefield'),None)
            if beast:
                markers=[m for m in presentation['blockingRestrictions'] if m['target']==beast['instanceId']]
                if beast.get('blockingRestrictions'):
                    assert len(markers)==1 and markers[0]['ability']==87941, 'Conditional blocking marker lost its actual rule identity'
                    observed.add('BlockingRestriction')
                elif 'BlockingRestriction' in observed:
                    assert not markers and beast.get('suppressPrintedAbilities'), 'Restriction did not expire with ability suppression'
                    observed.add('BlockingRestrictionRemoved')
        if phasing_check:
            phased=[c for c in s['cards'] if c['zone']=='PhasedOut']
            if phased:
                phased_ids.update(c['instanceId'] for c in phased)
                assert phased_ids<=set(presentation['phasedCards']), 'Phased-out cards missing from native state'
                assert not any(a['instanceId'] in phased_ids and a.get('highlight',3)==3 for a in s.get('legalActions',[])), 'Phased permanent remains playable'
                observed.add('PhaseOutState')
            elif phased_ids:
                cards={c['instanceId']:c for c in s['cards']}
                assert all(identifier in cards and cards[identifier]['zone']=='Battlefield' for identifier in phased_ids), 'Phased permanents disappeared or returned with different identity'
                assert not presentation['phasedCards'], 'Native phase-out state survived phase in'
                observed.add('PhaseInState')
        if stat_check:
            piledriver=next((c for c in s['cards'] if c['name']=='Goblin Piledriver' and c['zone']=='Battlefield'),None)
            marks=[m for m in presentation['statMarkers'] if piledriver and m['target']==piledriver['instanceId']]
            if piledriver and piledriver['power']>1:
                assert piledriver.get('powerModified') and any(m['power'] for m in marks), 'Temporary attack boost is missing native power marker'
                assert not any(m['toughness'] for m in marks), 'Power-only boost incorrectly marks toughness'
                observed.add('BoostedPower')
            elif piledriver and 'BoostedPower' in observed:
                assert piledriver['power']==1 and not marks and not piledriver.get('powerModified'), 'Expired attack boost remains marked'
                observed.add('ExpiredPower')
        if color_check:
            for choice in presentation['choiceResults']:
                if choice['domain']==6:
                    chosen=next(c for c in s['cards'] if c['instanceId']==choice['source'])
                    assert chosen['chosenColors'] and choice['values'] and choice['player']==chosen['controllerSeat']
                    observed.add('ColorChoice')
        if designation_check:observed.update(e['designation'] for e in presentation['designations'])
        if ability_change_check:
            changed=next((c for c in s['cards'] if c.get('name')=='Serra Angel' and c.get('suppressPrintedAbilities') and c.get('grantedAbilities')),None)
            if changed:
                abilities=next(c['abilities'] for c in presentation['cardAbilities'] if c['id']==changed['instanceId'])
                if not abilities or 8 in abilities or 15 in abilities:
                    (ROOT/'analysis/ability-changes-failure.json').write_text(json.dumps({'snapshot':s,'presentation':presentation}),encoding='utf-8')
                    print('Changed abilities:',json.dumps({'native':abilities,'granted':changed['grantedAbilities']}),flush=True)
                assert abilities and 8 not in abilities and 15 not in abilities, 'Printed keywords survived or granted mana ability was lost'
                observed.add('SuppressedPrintedGranted')
        if multistep_check:
            observed.update(e['category']+':'+str(e['starting']) for e in presentation['multistep'])
        if copy_check and any(c.get('isCopy') for c in s['cards']):observed.add('CopiedState')
        if facedown_check:
            face=next((c for c in s['cards'] if c['zone']=='Battlefield' and c['ownerSeat']==1 and c.get('faceDown')),None)
            if face:
                face_down_id=face['instanceId'];observed.add('FaceDownState')
                assert any(c['id']==face_down_id and c['grpId']==3 for c in presentation['printedFaces']), 'Native face-down printing missing'
            if face_down_id and any(c['instanceId']==face_down_id and c['zone']=='Battlefield' and not c.get('faceDown') for c in s['cards']):
                observed.add('FaceUpState')
        if previous and s.get('playbackEvents'):fixtures.append({'previous':previous,'current':s})
        previous=s
        if expected<=observed:break
        d=s.get('input',{});kind=d.get('type','');event=s.get('buttonEventId',0)
        if kind.startswith('InputGui'):
            if not poll['pending']:time.sleep(.02);continue
            if kind=='InputGuiGroups':
                answer=[{'zoneType':5,'subZoneType':0,'ids':d['choices']}] if d.get('surveil') else [{'subZoneType':2,'ids':d['choices']}]
            else:
                choices=d.get('options',d.get('choices',[]))
                answer=[c['id'] for c in choices[:max(1,d.get('min',0))]]
                if regeneration_check:
                    regeneration=next((c for c in choices if 'Regenerate' in c.get('label','')),None)
                    if regeneration:answer=[regeneration['id']]
                if illegal_attachment_check:
                    white=next((c for c in choices if 'white' in c.get('label','').lower()),None)
                    if white:answer=[white['id']]
            try:
                engine.request('interactiveAnswer',eventId=poll['pending'][0]['eventId'],answer=answer)
            except EngineRejected:
                print('Rejected decision:',json.dumps({'input':d,'pending':poll['pending'],'answer':answer}),flush=True)
                raise
            time.sleep(.02);continue
        if not event or kind in ('','InputLockUI'):time.sleep(.02);continue
        action={'type':'ok','eventId':event}
        selected=None
        if kind=='InputPassPriority' and not s.get('stack'):
            for name in (['Minimus Containment'] if blocking_check else ['Pacifism','Gods Willing'] if illegal_attachment_check else ["Teferi's Protection"] if phasing_check else [] if stat_check else ['Caged Sun'] if color_check else ['Minimus Containment'] if ability_change_check else ['Palace Jailer'] if designation_check else ['Opt','Consider','Merfolk Branchwalker','Experimental Augury',"Raffine's Informant"] if multistep_check else ['Clone'] if copy_check else ['Rattleclaw Mystic'] if facedown_check else ['Golgari Charm','Murder'] if regeneration_check else ['Mossborn Hydra'] if replacement_check else ['Grizzly Bears','Contact Other Plane','Opt']):
                if name in cast:continue
                if illegal_attachment_check and name=='Gods Willing' and not any(c['name']=='Pacifism' and c.get('attachedTo') for c in s['cards']):continue
                selected=next((a for a in s.get('legalActions',[]) if a.get('cardName')==name
                    and a.get('highlight',3)==3 and a['actionType']==1 and (not facedown_check or a.get('faceDownCast'))),None)
                if selected:action.update(type='playAbility',abilityId=selected['abilityId']);break
            if facedown_check and face_down_id:
                selected=next((a for a in s.get('legalActions',[]) if a['instanceId']==face_down_id and a['actionType']==8 and a.get('highlight',3)==3),None)
                if selected:action.update(type='playAbility',abilityId=selected['abilityId'])
            if designation_check and not selected and not any(c.get('monstrous') for c in s['cards']):
                selected=next((a for a in s.get('legalActions',[]) if a['cardName']=='Fleecemane Lion' and a['actionType']==2 and a.get('highlight',3)==3),None)
                if selected:action.update(type='playAbility',abilityId=selected['abilityId'])
        elif kind=='InputAttack':action.update(type='attackers',assignments=[{'instanceId':a['instanceId'],'recipient':a['defenders'][0]} for a in d['attackers']] if stat_check else [])
        elif kind=='InputBlock':action.update(type='blockers',assignments=[])
        elif kind=='InputSelectTargets':
            if ability_change_check or blocking_check:
                target=next(c['instanceId'] for c in s['cards'] if c.get('name')==('Questing Beast' if blocking_check else 'Serra Angel') and c['instanceId'] in d['targets'])
                action.update(type='selectTarget',instanceId=target)
            else:
                ownBear=next((c['instanceId'] for c in s['cards'] if c.get('name')=='Grizzly Bears' and c['ownerSeat']==1
                    and c['zone']=='Battlefield' and c['instanceId'] in d['targets']),None)
                action.update(type='selectTarget',instanceId=ownBear if (regeneration_check or illegal_attachment_check) and ownBear else d['targets'][0])
        elif kind in ('InputSelectCardsFromList','InputSelectEntitiesFromList'):
            action.update(type='selectMany',instanceIds=d['choices'][:d['min']])
            if copy_check:
                angel=next((c['instanceId'] for c in s['cards'] if c.get('name')=='Serra Angel' and c['instanceId'] in d['choices']),None)
                if angel:action['instanceIds']=[angel]
        try:
            engine.request('interactiveAction',**action)
            if selected:cast.add(selected['cardName'])
        except EngineRejected as error:
            if not any(x in str(error) for x in ('Stale human action','Previous human action still running','Ability no longer available')):raise
        time.sleep(.03)
    if not expected<=observed:
        (ROOT/'analysis/animation-outcome-failure.json').write_text(json.dumps({'snapshot':s,'events':events,'observed':sorted(observed)}),encoding='utf-8')
    assert expected<=observed, ('Missing native effects',observed,engine.diagnostics[-10:])
    if blocking_check:
        (ROOT/'analysis/animation-blocking-restrictions-check.json').write_text(json.dumps(fixtures),encoding='utf-8')
        print('PASS: real Forge Questing Beast has its native conditional blocking marker and loses it under Minimus Containment')
        sys.exit(0)
    if phasing_check:
        (ROOT/'analysis/animation-phasing-check.json').write_text(json.dumps(fixtures),encoding='utf-8')
        print('PASS: real Forge phasing preserves all permanent IDs, disables actions, and phases back in with native playback')
        sys.exit(0)
    if illegal_attachment_check:
        assert any(c['name']=='Pacifism' and c['zone']=='Graveyard' for c in s['cards']), 'Protection did not put the illegal Aura in the graveyard'
        assert any(c['name']=='Grizzly Bears' and c['ownerSeat']==1 and c['zone']=='Battlefield' for c in s['cards']), 'Protected creature left play'
        assert any(e['type']=='illegalAttachment' for e in events), 'Missing authoritative illegal attachment observation'
        (ROOT/'analysis/animation-illegal-attachments-check.json').write_text(json.dumps(fixtures),encoding='utf-8')
        print('PASS: real Forge protection detaches Pacifism, sends it to graveyard and emits native invalidating ability playback')
        sys.exit(0)
    if stat_check:
        (ROOT/'analysis/animation-temporary-stats-check.json').write_text(json.dumps(fixtures),encoding='utf-8')
        print('PASS: real Forge Goblin Piledriver attack boost has native power marker and clears after combat')
        sys.exit(0)
    if color_check:
        (ROOT/'analysis/animation-color-choice-check.json').write_text(json.dumps(fixtures),encoding='utf-8')
        print('PASS: real Forge Caged Sun choice retains chosen color and emits native color choice result')
        sys.exit(0)
    if ability_change_check:
        (ROOT/'analysis/animation-ability-changes-check.json').write_text(json.dumps(fixtures),encoding='utf-8')
        print('PASS: real Forge Minimus Containment removes printed keywords and retains granted mana ability in Arena')
        sys.exit(0)
    if designation_check:
        assert any(c.get('monstrous') for c in s['cards']), 'No real monstrous creature'
        assert any(p.get('monarch') and p.get('citysBlessing') for p in s['players']), 'No real monarch and blessing'
        (ROOT/'analysis/animation-designation-check.json').write_text(json.dumps(fixtures),encoding='utf-8')
        print('PASS: real Forge monstrosity, monarch and city blessing reach native designation state')
        sys.exit(0)
    if multistep_check:
        boundaries=[e for e in events if e['type']=='multistepEffect']
        active={}
        for e in boundaries:
            key=(e['sourceId'],e['targetId'],e['category'])
            active[key]=active.get(key,0)+(1 if e['starting'] else -1)
            assert active[key]>=0, 'Completion arrived before its start'
        assert all(n==0 for n in active.values()), 'Unbalanced effect boundaries'
        (ROOT/'analysis/animation-multistep-check.json').write_text(json.dumps(fixtures),encoding='utf-8')
        print('PASS: real Forge scry/surveil/explore/proliferate/connive emit ordered, balanced native effect boundaries')
        sys.exit(0)
    if copy_check:
        copy=next(c for c in s['cards'] if c.get('isCopy'))
        assert copy['name']=='Serra Angel' and copy['grpId']==copy['copyFromGrpId'] and copy['originalGrpId']!=copy['grpId'], 'Copied printing or original identity incorrect'
        assert any(c['id']==copy['instanceId'] and c['grpId']==copy['grpId'] for c in presentation['printedFaces']), 'Native copied printing missing'
        (ROOT/'analysis/animation-copy-check.json').write_text(json.dumps(fixtures),encoding='utf-8')
        print('PASS: real Forge Clone takes the chosen creature printing and native copied-object state')
        sys.exit(0)
    if facedown_check:
        (ROOT/'analysis/animation-facedown-check.json').write_text(json.dumps(fixtures),encoding='utf-8')
        print('PASS: real Forge morph casts use native face-down printing and turn face up through action type 8')
        sys.exit(0)
    if regeneration_check:
        assert any(e['type']=='regenerated' for e in events)
        assert any(c.get('regenerationShields',0)>0 for f in fixtures for c in f['current']['cards']), 'No live regeneration shield recorded'
        (ROOT/'analysis/animation-regeneration-check.json').write_text(json.dumps(fixtures),encoding='utf-8')
        print('PASS: real Forge regeneration protects the chosen creature and emits native regeneration playback')
        sys.exit(0)
    if replacement_check:
        replacements=[e for e in events if e['type']=='replacementApplied']
        assert replacements and all(e['sourceId']>=1000 and e['targetId']>=1000 for e in replacements)
        (ROOT/'analysis/animation-replacement-check.json').write_text(json.dumps(fixtures),encoding='utf-8')
        print('PASS: real Forge counter replacements produce native replacement/counter playback with known ability IDs')
        sys.exit(0)
    coins=[e for e in events if e['type']=='randomOutcome' and e['kind']=='coin']
    dice=[e for e in events if e['type']=='randomOutcome' and e['kind']=='die']
    scries=[e for e in events if e['type']=='scryResult']
    assert coins and all(v in (0,1) for e in coins for v in e['results'])
    assert dice and all(e['sides']==20 and 1<=e['natural'][0]<=20 for e in dice)
    assert scries and all(not set(e['topIds'])&set(e['bottomIds']) for e in scries)
    (ROOT/'analysis/animation-outcome-check.json').write_text(json.dumps(fixtures),encoding='utf-8')
    print('PASS: actual Forge coin/d20/scry decisions produce native annotations, with recorded outcomes and destinations')
finally:
    native.terminate();native.wait(timeout=5)
    engine.close()
