"""Strip hidden identities before a game snapshot leaves the rules process."""
import copy


def snapshot_for_player(snapshot):
    value=copy.deepcopy(snapshot)
    value['viewerSeat']=1
    prompt=value.get('input',{})
    choices=set(prompt.get('choices',[])) if prompt.get('type','').startswith('InputGui') else set()
    for group in prompt.get('groups',[]):
        if not group.get('faceDown'):choices.update(group.get('ids',[]))
    hidden=set()
    for card in value.get('cards',[]):
        private=card.get('zone')=='Library' or card.get('zone') in ('Hand','Sideboard') and card.get('ownerSeat')!=1 or card.get('faceDown',False)
        known=1 in card.get('viewers',[]) or card.get('instanceId') in choices
        if not private or known:continue
        hidden.add(card['instanceId'])
        public_face_down=card.get('faceDown',False) and card.get('zone') in ('Battlefield','Stack','PhasedOut')
        public_stats={key:copy.deepcopy(card[key]) for key in ('power','toughness','cardTypes','subtypes','supertypes','colors') if key in card} if public_face_down else {}
        card.update(grpId=0,originalGrpId=0,name='',manaCost='{0}',power=0,toughness=0,
                    cardTypes=[],subtypes=[],supertypes=[],colors=[],grantedAbilities=[],
                    grantedKeywords=[],suppressedKeywords=[],blockingRestrictions=[],isLand=False)
        if public_face_down:
            card.update(public_stats)
            card['grpId']=3
        for key in ('emblemText','emblemGrantText','cardState','chosenType','chosenColors','chosenNumber'):
            card.pop(key,None)
    stack_entries=list(value.get('stack',[]))+list(value.get('playbackObjects',[]))
    for event in value.get('playbackEvents',[]):
        if isinstance(event.get('object'),dict):stack_entries.append(event['object'])
    for entry in stack_entries:
        if entry.get('parentId') in hidden and not entry.get('isAbility'):
            entry.update(sourceGrpId=0,cardName='Face-down spell',description='')
            entry.pop('abilityIdentity',None)
    return value


def poll_for_player(poll):
    value=copy.deepcopy(poll)
    value['events']=[{key:item[key] for key in ('eventId','method','blocking') if key in item}
                     for item in value.get('events',[])]
    return value
