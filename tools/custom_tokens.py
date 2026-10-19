"""Stable native display records and preview links for Forge token scripts."""
import json,re,struct,zlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
MANIFEST=ROOT/'data/custom-arena-cards/tokens.json'

def prepare():
    rows=json.loads((ROOT/'analysis/planeswalker-audit/token-audit.json').read_text(encoding='utf8'))
    old=json.loads(MANIFEST.read_text(encoding='utf8')) if MANIFEST.exists() else {'tokens':[],'enums':{}}
    ids={r['script']:r['grpId'] for r in old['tokens']}
    previous={r['script']:r for r in old['tokens']}
    next_id=max(ids.values(),default=2_909_999)+1
    scripts={r['script'] for r in rows}
    for r in sorted(rows,key=lambda r:r['script']):
        if r['script'] not in ids:ids[r['script']]=next_id;next_id+=1
        r['grpId']=ids[r['script']]
        if r['mapped']>=2_910_000:r['mapped']=previous.get(r['script'],{}).get('mapped',0)
        r['name']=r['name'].removesuffix(' Token')
        raw=(ROOT/'vendor/forge/forge-gui/res/tokenscripts'/f"{r['script'].removesuffix('__back')}.txt").read_text(encoding='utf8')
        raw=raw.split('ALTERNATE',1)[-1] if '__back' in r['script'] else raw.split('ALTERNATE',1)[0]
        oracle=re.search(r'^Oracle:(.*)$',raw,re.M)
        if oracle:r['oracle']=oracle[1].replace('\\n','\n').replace('CARDNAME',r['name'])
        if not r['oracle']:
            lines=[]
            for line in raw.splitlines():
                if line.startswith('K:'):lines.append(line[2:].replace(':',' ',1))
                elif line.startswith(('A:','T:','S:')):
                    match=re.search(r'(?:SpellDescription|TriggerDescription|Description)\$\s*([^|]+)',line)
                    if match:lines.append(match[1].strip().replace('CARDNAME',r['name']))
            r['oracle']='\n'.join(dict.fromkeys(lines))
    sources={}
    folder=ROOT/'vendor/forge/forge-gui/res/cardsfolder'
    for path in folder.rglob('*.txt'):
        text=path.read_text(encoding='utf8');name=re.search(r'^Name:(.+)$',text,re.M)
        if not name:continue
        found=set()
        for value in re.findall(r'TokenScript\$\s*([^|\r\n]+)',text):
            found.update(v for v in re.split(r'[, &]+',value.strip()) if v in scripts)
        for keyword,script in [('Living weapon','b_0_0_phyrexian_germ'),('Fabricate','c_1_1_a_servo')]:
            if re.search(r'^K:'+re.escape(keyword)+r'(?:\b|:)',text,re.M|re.I) and script in scripts:found.add(script)
        if re.search(r'(?:DB|AB|SP)\$ Investigate\b',text):found.add('c_a_clue_draw')
        if re.search(r'(?:DB|AB|SP)\$ (?:Treasure|Food|Clue)\b',text):
            for kind,script in [('Treasure','c_a_treasure_sac'),('Food','c_a_food_sac'),('Clue','c_a_clue_draw')]:
                if re.search(r'(?:DB|AB|SP)\$ '+kind+r'\b',text) and script in scripts:found.add(script)
        if found:sources[name[1].strip()]=sorted(found)
    active=ROOT/'data/custom-arena-cards/manifest.json'
    if active.exists():
        for record in json.loads(active.read_text(encoding='utf8')):
            metadata=ROOT/record['metadata']
            original=json.loads(metadata.read_text(encoding='utf8')).get('name','')
            if original in sources:sources.setdefault(record['name'],sources[original])
    manifest={'tokens':rows,'sources':sources,'enums':old['enums']}
    MANIFEST.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
    print('Prepared',len(rows),'token records and',len(sources),'source-card preview mappings')

def stage(db,localize,mana):
    if not MANIFEST.exists():return
    data=json.loads(MANIFEST.read_text(encoding='utf8'))
    columns=[r[1] for r in db.execute('PRAGMA table_info(Cards)')]
    native={}
    for kind,value,loc in db.execute('SELECT Type,Value,LocId FROM Enums'):
        name=db.execute('SELECT Loc FROM Localizations_enUS WHERE LocId=? LIMIT 1',(loc,)).fetchone()
        if name:native.setdefault(kind,{})[name[0]]=value
    new_enums=data['enums'];next_enum=max(new_enums.values(),default=71_999)+1
    def enum(kind,name):
        nonlocal next_enum
        if name in native.get(kind,{}):return native[kind][name]
        key=kind+':'+name
        if key not in new_enums:new_enums[key]=next_enum;next_enum+=1
        value=new_enums[key];loc=92_000_000+value
        collision=db.execute('SELECT LocId FROM Enums WHERE Type=? AND Value=?',(kind,value)).fetchone()
        if collision and collision[0]!=loc:raise ValueError('Token enum collision: '+key)
        localize(loc,name)
        db.execute('INSERT OR REPLACE INTO Enums (Type,Value,LocId) VALUES (?,?,?)',(kind,value,loc))
        return value
    def enums(kind,values):return ','.join(str(enum(kind,v)) for v in values)
    templates={r['GrpId']:dict(r) for r in db.execute('SELECT * FROM Cards WHERE GrpId<2910000')}
    ability_template=dict(db.execute('SELECT * FROM Abilities WHERE Id=8').fetchone())
    for r in data['tokens']:
        i=r['grpId']-2_910_000;base=90_000_000+i*100;aidbase=80_000_000+i*64
        prior=db.execute('SELECT TitleId FROM Cards WHERE GrpId=?',(r['grpId'],)).fetchone()
        if prior and prior[0]!=base:raise ValueError('Token ID collision: '+r['script'])
        template=templates.get(r['mapped']) or next((v for v in templates.values() if v['IsToken'] and v.get('Order_Title')==r['name']),None)
        if template is None:template=templates[67068] if 'Creature' not in r['types'] else templates[68323]
        card=dict(template)
        localize(base,r['name']);localize(base+1,' '.join(r['supertypes']+r['types']));localize(base+2,' '.join(r['subtypes']))
        abilities=[]
        for n,line in enumerate(r['oracle'].splitlines()):
            if n>=64:raise ValueError('Too many token abilities: '+r['script'])
            ability=dict(ability_template);loc=base+10+n;aid=aidbase+n
            localize(loc,mana(line))
            ability.update({k:None for k in ability if k not in ('Id','BaseId','FakeBaseIdNumeral','TextId','Category','SubCategory','AbilityWord','NumericAid','RequiresConfirmation','PaymentType','FullyParsed','IsIntrinsicAbility')})
            ability.update(Id=aid,BaseId=0,BaseIdNumeral=0,FakeBaseIdNumeral=0,TextId=loc,Category=1 if ': ' in line else 2 if line.startswith(('When ','Whenever ','At ')) else 3,SubCategory=0,AbilityWord=0,NumericAid=0,RequiresConfirmation=0,PaymentType=0,FullyParsed=1,IsIntrinsicAbility=0)
            loyalty=re.fullmatch(r'\[?([+\-\u2212]?\d+)\]?:\s*(.+)',line) if 'Planeswalker' in r['types'] else None
            if loyalty:
                cost=loyalty[1].replace('\u2212','-');localize(loc,mana(loyalty[2]))
                ability.update(Category=1,PaymentType=2,LoyaltyCost=cost,CostTypes='5',RequiresConfirmation=8 if cost.startswith('-') else 0)
            keys=list(ability);db.execute('INSERT OR REPLACE INTO Abilities ('+','.join(keys)+') VALUES ('+','.join('?' for _ in keys)+')',[ability[k] for k in keys]);abilities.append(f'{aid}:{loc}')
        # Reuse suitable native artwork; neutral art for genuinely absent images.
        art=template['ArtId'] if r['mapped'] or template.get('Order_Title')==r['name'] else 10_910_000+i
        card.update({k:'' for k in columns if k.startswith('Linked')})
        card.update(GrpId=r['grpId'],ArtId=art,ArtPath='',TitleId=base,AltTitleId=base,InterchangeableTitleId=base,TypeTextId=base+1,SubtypeTextId=base+2,
            IsToken=1,IsPrimaryCard=0,IsDigitalOnly=0,IsRebalanced=0,Power=r['power'] if 'Creature' in r['types'] else '',Toughness=r['toughness'] if 'Creature' in r['types'] else '',
            Types=enums('CardType',r['types']),Subtypes=enums('SubType',r['subtypes']),Supertypes=enums('SuperType',r['supertypes']),
            Colors=enums('CardColor',[{'W':'White','U':'Blue','B':'Black','R':'Red','G':'Green'}[c] for c in r['colors']]),
            AbilityIds=','.join(abilities),HiddenAbilityIds='',AbilityIdToLinkedTokenGrpId='',OldSchoolManaText='',Cmc=0,Order_Title=r['name'],Order_Cmc=0,
            ManaCost='',FlavorTextId=0,ReminderTextId=0,LinkedFaceType=0)
        card.update(FrameColors=card['Colors'],IndicatorColors='',RawFrameDetail=''.join(r['colors']) if r['colors'] else 'A' if 'Artifact' in r['types'] else 'C',
            ArtistCredit=template['ArtistCredit'] if art==template['ArtId'] else '',FlavorTextId=0,TextChangeData='',Tags='',PromoLabel='',Watermark='',
            OldSchoolManaText='',CollectorNumber='T'+str(i),ExpansionCode='VMA',DigitalReleaseSet='VMA')
        if 'Planeswalker' in r['types']:card['Toughness']=r.get('loyalty','0')
        db.execute('INSERT OR REPLACE INTO Cards ('+','.join(columns)+') VALUES ('+','.join('?' for _ in columns)+')',[card[k] for k in columns])
        r['artId']=art
    by_script={r['script']:r['grpId'] for r in data['tokens']}
    for script,back in by_script.items():
        if not script.endswith('__back'):continue
        front=by_script[script.removesuffix('__back')]
        db.execute('UPDATE Cards SET LinkedFaceType=2,LinkedFaceGrpIds=? WHERE GrpId=?',(str(back),front))
        db.execute('UPDATE Cards SET LinkedFaceType=1,LinkedFaceGrpIds=? WHERE GrpId=?',(str(front),back))
    # Native preview reads this relation, including for locally added cards.
    for card in db.execute('SELECT GrpId,TitleId,AbilityIds FROM Cards WHERE IsToken=0').fetchall():
        title=db.execute('SELECT Loc FROM Localizations_enUS WHERE LocId=? LIMIT 1',(card['TitleId'],)).fetchone()
        if not title:continue
        scripts=data['sources'].get(title[0],[])
        if not scripts or not card['AbilityIds']:continue
        ability_ids=[v.split(':')[0] for v in card['AbilityIds'].split(',')]
        descriptions={aid:db.execute('SELECT l.Loc FROM Abilities a JOIN Localizations_enUS l ON a.TextId=l.LocId WHERE a.Id=? LIMIT 1',(aid,)).fetchone() for aid in ability_ids}
        old=db.execute('SELECT AbilityIdToLinkedTokenGrpId FROM Cards WHERE GrpId=?',(card['GrpId'],)).fetchone()[0] or ''
        if card['GrpId']<1_800_000 and old:continue
        links=set(filter(None,old.split(','))) if card['GrpId']<1_800_000 else set()
        for s in scripts:
            token_name=next(r['name'] for r in data['tokens'] if r['script']==s).lower()
            matching=[aid for aid,desc in descriptions.items() if desc and (('create' in desc[0].lower() and token_name in desc[0].lower()) or s=='c_a_clue_draw' and 'investigate' in desc[0].lower())]
            links.add(f'{matching[0] if matching else ability_ids[0]}:{by_script[s]}')
        db.execute('UPDATE Cards SET AbilityIdToLinkedTokenGrpId=? WHERE GrpId=?',(','.join(sorted(links)),card['GrpId']))
    MANIFEST.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf8')

def install_art(directory):
    data=json.loads(MANIFEST.read_text(encoding='utf8'))
    def chunk(kind,body):return struct.pack('>I',len(body))+kind+body+struct.pack('>I',zlib.crc32(kind+body)&0xffffffff)
    # Unity detects PNG content independently of the helper's .jpg filename.
    rows=b''.join(b'\0'+bytes((42,49,54))*512 for _ in range(360))
    neutral=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',512,360,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(rows))+chunk(b'IEND',b'')
    for r in data['tokens']:
        if r.get('artId',0)>=10_910_000:
            path=Path(directory)/f"{r['artId']}.jpg"
            if not path.exists():path.write_bytes(neutral)

if __name__=='__main__':prepare()
