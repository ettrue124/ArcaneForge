"""Generate dated format rules and a digital-card review inventory from cached Oracle data."""
import gzip
import json
from pathlib import Path
import re

ROOT=Path(__file__).resolve().parents[1]


def main():
    inputs=sorted((ROOT/'data/magic-import').glob('oracle-cards-*.jsonl.gz'))
    if not inputs:
        raise RuntimeError('Import the Oracle catalogue before generating format rules')
    source=inputs[-1]
    cards={}
    digital=[]
    with gzip.open(source,'rt',encoding='utf8') as stream:
        for line in stream:
            card=json.loads(line)
            text=card.get('oracle_text','')+'\n'+'\n'.join(face.get('oracle_text','') for face in card.get('card_faces',[]))
            front_type=card.get('card_faces',[{}])[0].get('type_line',card.get('type_line',''))
            rule={'name':card['name'],'legalities':card.get('legalities',{}),'typeLine':front_type,
                  'colorIdentity':card.get('color_identity',[]),
                  'canBeCommander':'can be your commander' in text.lower()}
            if 'any number of cards named' in text:
                rule['copyLimit']=10000
            if card['name']=='Seven Dwarves':rule['copyLimit']=7
            if card['name']=='Nazgûl':rule['copyLimit']=9
            cards[card['name']]=rule
            for face in card.get('card_faces',[]):
                cards.setdefault(face['name'],rule)
            if 'arena' in card.get('games',[]) and (card.get('digital') or card.get('name','').startswith('A-')):
                digital.append({'name':card['name'],'status':'requires_rules_verification',
                    'mechanics':sorted(set(re.findall(r'\b(?:seek|conjure|perpetually|spellbook|intensity|heist|specialize|draft)\b',text.lower())))})
    result={'source':source.name,'sourceProvider':'Scryfall Oracle bulk cache','cards':cards}
    (ROOT/'data/format-policies.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    report={'source':source.name,'verifiedAllCards':False,'cards':digital,
            'note':'Catalogue presence and matching names do not certify digital rules. Every entry requires an explicit rules check.'}
    (ROOT/'data/arena-digital-support.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    print('Generated format policies for',len(cards),'cards;',len(digital),'digital/rebalanced entries require verification.')


if __name__=='__main__':main()
