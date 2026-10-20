"""Build the same event registry into Arena's native format and queue fixtures."""
import copy
import json
import re
from pathlib import Path
import sys

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from server import formats


def read(path):return json.loads(path.read_text(encoding='utf8'))
def write(path,value):
    text=json.dumps(value,ensure_ascii=False,indent=2)
    text=re.sub(r'\[\s*(?:\d+,\s*)*\d+\s*\]',lambda m:json.dumps(json.loads(m.group()),separators=(',',':')),text)
    path.write_text(text+'\n',encoding='utf8')


def main():
    catalogue=read(ROOT/'data/catalogue.json')
    policies=formats.policies()['cards']
    path=ROOT/'analysis/deckbuilder-formats.json'
    fixture=read(path)
    response=fixture['GetFormatsResponse']
    template=next(f for f in response['formats'] if f['name']=='Historic')
    entries=[]
    labels=read(ROOT/'data/mode-labels.json')
    for spec in [e for e in formats.events() if e['opponent']=='human']:
        name=spec['name']
        f=copy.deepcopy(template)
        f.update(name=name,mainDeckQuota={'min':spec['deckSize']-(1 if spec['commander'] else 0),
                 'max':spec['deckSize']-1 if spec['commander'] else 250},
                 commandZoneQuota={'min':int(spec['commander']),'max':int(spec['commander'])},
                 sideBoardQuota={'min':0,'max':spec['sideboardMax']},
                 cardCountRestrictionInternal=1 if spec['commander'] else 0,
                 bannedTitleIds=[],suspendedTitleIds=[],supressedTitleIds=[],individualCardQuotas={})
        f['allowedTitleIds']=[]
        f['legalSets']=[]
        f['filterSets']=[]
        if name=='NoBans60':f['mainDeckQuota']={'min':60,'max':60}
        if spec['base']=='Commander':
            f['mainDeckQuota']={'min':98,'max':99}
            f['commandZoneQuota']={'min':1,'max':2}
        key=formats.legality_key(spec)
        titles={}
        leaders=set()
        for card in catalogue:
            title=card.get('arenaMetadata',{}).get('TitleId')
            if not title:continue
            policy=policies.get(card['name'],{})
            status=policy.get('legalities',{}).get(key,'not_legal')
            legal=spec['base']=='NoBans60' or status in ('legal','restricted') or spec['unbanned'] and status=='banned'
            titles[title]=titles.get(title,False) or legal
            if status=='restricted' and not spec['unbanned']:f['individualCardQuotas'][str(title)]={'min':0,'max':1}
            types=policy.get('typeLine','')
            if legal and ('Legendary' in types and ('Creature' in types or spec['base']!='Commander' and 'Planeswalker' in types)
                          or policy.get('canBeCommander') or spec['base']=='Commander' and card['name'] in formats.commander_names()):leaders.add(title)
        f['bannedTitleIds']=sorted(title for title,legal in titles.items() if not legal)
        allowed=sorted(title for title,legal in titles.items() if legal)
        if allowed and len(allowed)<len(f['bannedTitleIds']):
            f['allowedTitleIds']=allowed
            f['bannedTitleIds']=[]
        if spec['commander']:f['allowedCommanderTitleIds']=sorted(leaders)
        entries.append(f)
    names={f['name'] for f in entries}
    response['formats']=[f for f in response['formats'] if f['name'] not in names]+entries
    for group in response.get('formatGroups',[]):
        values=group.get('formats',group.get('formatNames',[]))
        values.extend(sorted(names-set(values)))
    fixture['commands']['GetFormats']['payload']=response
    write(path,fixture)
    path=ROOT/'analysis/startup-fixtures.json'
    fixture=read(path)
    blade=fixture['commands']['GetPlayBladeQueueConfig']['payload']
    events=fixture['commands']['EventGetActiveEventsV2']['payload']['Events']
    template_event=next(e for e in events if e['InternalEventName']=='NoBans60AI')
    old={'AIBotMatch','CommanderAI','CommanderAI4','CommanderUnbannedAI','NoBans60AI'}
    configured=[]
    for index,spec in enumerate(formats.events()):
        event_id=spec['id']
        if event_id in old:continue
        title='ArenaLab/Events/'+event_id
        labels[title]=spec['name'].replace('Unbanned',' — Unbanned')+(' — Bots' if spec['opponent']=='bot' else ' — Players')
        size='ArenaLab/DeckSize/'+str(spec['deckSize'])
        labels.setdefault(size,str(spec['deckSize'])+' cards')
        blade=[row for row in blade if row['Id']!=event_id]
        blade.append({'Id':event_id,'QueueType':'Unranked','LocTitle':title,'EventNameBO1':event_id,
            'EventNameBO3':None,'DeckSizeBO1':size,'DeckSizeBO3':None,
            'SideBoardBO1':'ArenaLab/Sideboard/15' if spec['sideboardMax'] else 'ArenaLab/Sideboard/None',
            'SideBoardBO3':None,'Tags':[spec['name']]})
        new=copy.deepcopy(template_event)
        new.update(InternalEventName=event_id)
        # The deck format lives in the event payload as well as the blade tag.
        def replace(value):
            if isinstance(value,dict):return {k:replace(v) for k,v in value.items()}
            if isinstance(value,list):return [replace(v) for v in value]
            return spec['name'] if value=='NoBans60' else title if value=='ArenaLab/Modes/NoBans60' else value
        new=replace(new)
        new['Flags']=['IsArenaPlayModeEvent']+(['IsAiBotMatch'] if spec['opponent']=='bot' else [])
        new['EventUXInfo']['PublicEventName']=event_id
        new['EventUXInfo']['DisplayPriority']=100+index
        events=[row for row in events if row['InternalEventName']!=event_id]+[new]
        configured.append(event_id)
    fixture['commands']['GetPlayBladeQueueConfig']['payload']=blade
    fixture['commands']['EventGetActiveEventsV2']['payload']['Events']=events
    write(path,fixture)
    write(ROOT/'data/mode-labels.json',labels)
    write(ROOT/'data/multiplayer-events.json',configured)
    print('Prepared',len(entries),'formats and',len(configured),'additional native events.')


if __name__=='__main__':main()
