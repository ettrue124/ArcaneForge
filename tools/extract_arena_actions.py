"""Extract Arena's declared protocol and incoming game-message handlers locally.

Reads installed DLL descriptors and decompiles registered client handlers. Writes
a versioned inventory; does not send messages, alter the client, or contact servers.
"""
import argparse
import concurrent.futures
import csv
from datetime import datetime, timezone
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
REQUIRED=('Google.Protobuf.dll','Wizards.MDN.GreProtobuf.dll','Wizards.Arena.Models.Protobuf.dll','SharedClientCore.dll','Core.dll')

def run(command, timeout=180):
    result=subprocess.run([str(v) for v in command],capture_output=True,text=True,encoding='utf8',errors='replace',timeout=timeout)
    if result.returncode:
        raise RuntimeError((result.stdout+'\n'+result.stderr).strip()[-5000:])
    return result.stdout

def normalize(value):
    return re.sub('[^a-z0-9]','',value.lower())

def schema_index(descriptor_set):
    """Keep exact wire names/numbers, nested types, oneofs and enum aliases."""
    messages={};enums={}
    def enum(item,prefix,file):
        name=prefix+item['name']
        enums[name]={'name':name,'file':file,'values':[{'name':v['name'],'value':int(v.get('number',0))} for v in item.get('value',[])]}
    def message(item,prefix,file):
        name=prefix+item['name'];oneofs=[v['name'] for v in item.get('oneofDecl',[])]
        fields=[]
        for field in item.get('field',[]):
            number=int(field['number']);oneof=field.get('oneofIndex')
            fields.append({'name':field['name'],'json_name':field.get('jsonName',field['name']),'number':number,
                'type':field.get('type','TYPE_DOUBLE'),'type_name':field.get('typeName','').lstrip('.'),
                'label':field.get('label','LABEL_OPTIONAL'),'oneof':oneofs[int(oneof)] if oneof is not None else None,
                'default':field.get('defaultValue'),'proto3_optional':field.get('proto3Optional',False)})
        messages[name]={'name':name,'file':file,'fields':fields,'oneofs':oneofs,
            'reserved_ranges':item.get('reservedRange',[]),'reserved_names':item.get('reservedName',[])}
        for child in item.get('nestedType',[]):message(child,name+'.',file)
        for child in item.get('enumType',[]):enum(child,name+'.',file)
    for file in descriptor_set['file']:
        prefix=file.get('package','');prefix=prefix+'.' if prefix else ''
        for item in file.get('messageType',[]):message(item,prefix,file['name'])
        for item in file.get('enumType',[]):enum(item,prefix,file['name'])
    return messages,enums

def unique_name(items,short):
    matches=[v for k,v in items.items() if k.rsplit('.',1)[-1]==short]
    if len(matches)!=1:raise ValueError(f'Expected one {short} descriptor, found {len(matches)}')
    return matches[0]

def registrations(source):
    return [{'message':msg,'handler':handler} for msg,handler in
        re.findall(r'GREMessageType\.(\w+)\s*,\s*new\s+([\w.]+)\s*\(',source)]

def incoming_catalogue(messages,enums,registered,evidence=()):
    envelope=unique_name(messages,'GREToClientMessage');types=unique_name(enums,'GREMessageType')
    # Match wire field names to enum suffixes. A field match is structural;
    # registration is separate evidence that the client actually handles it.
    fields={normalize(f['name']):f for f in envelope['fields']}
    handlers={normalize(r['message']):r['handler'] for r in registered}
    handler_sources={r['class'].rsplit('.',1)[-1]:r for r in evidence}
    entries=[]
    for value in types['values']:
        short=value['name'].removeprefix('GREMessageType_');key=normalize(short)
        field=fields.get(key);handler=handlers.get(key)
        if short in ('GameStateMessage','QueuedGameStateMessage'):
            field=fields.get(normalize('gameStateMessage'))
            handler='GreClient.Rules.GreInterface.ProcessMessage' if any(r['class']=='GreClient.Rules.GreInterface' for r in evidence) else None
        record=handler_sources.get((handler or '').rsplit('.',1)[-1],{})
        members=record.get('message_members',[])
        observed=[fields[normalize(member)] for member in members if normalize(member) in fields
            and fields[normalize(member)]['type']=='TYPE_MESSAGE']
        if not field and len(observed)==1:field=observed[0]
        entries.append({'name':short,'wire_name':value['name'],'value':value['value'],
            'direction':'server_to_client','payload_field':field,'payload_type':field['type_name'] if field else None,
            'handler_message_fields':observed,'handler_source':record.get('source'),
            'client_handler':handler,'registered':handler is not None,
            'evidence':'registered client handler' if handler else 'declared enum; handler not recovered',
            'runtime_verified':False})
    return entries

def source_evidence(name,source,path):
    return {'class':name,'source':path,
        'incoming_messages':sorted(set(re.findall(r'GREMessageType\.(\w+)',source))),
        'outgoing_responses':sorted(set(re.findall(r'ClientMessageType\.(\w+)',source))),
        'action_types':sorted(set(re.findall(r'ActionType\.(\w+)',source))),
        'annotation_types':sorted(set(re.findall(r'AnnotationType\.(\w+)',source))),
        'detail_key_candidates':sorted(set(re.findall(r'const string \w+\s*=\s*"([^"\n]+)"',source)
            +re.findall(r'\.Key\s*==\s*"([^"\n]+)"',source)+re.findall(r'\.Key\.Equals\("([^"\n]+)"\)',source))),
        'casting_options':sorted(set(re.findall(r'CastingTimeOptionType\.(\w+)',source))),
        'message_members':sorted(set(re.findall(r'\b(?:msg|message|inMsg)\.(\w+)',source))),
        'created_types':sorted(set(re.findall(r'new\s+([\w.]+)\s*(?:\(|\{)',source)))}

def decompile_handlers(tool,managed,output,jobs,refresh):
    assembly=managed/'SharedClientCore.dll';fingerprint=hashlib.sha256(assembly.read_bytes()).hexdigest()[:16]
    cache=output/'sources'/fingerprint;cache.mkdir(parents=True,exist_ok=True)
    names=[line[6:].strip() for line in run([tool,'-l','c',assembly]).splitlines() if line.startswith('Class ') and '+' not in line]
    by_short={}
    for name in names:by_short.setdefault(name.rsplit('.',1)[-1],[]).append(name)
    def extract(name):
        path=cache/(name+'.cs')
        if refresh or not path.exists():path.write_text(run([tool,'-t',name,assembly]),encoding='utf8')
        return name,path.read_text(encoding='utf8'),str(path.relative_to(output))
    initial=extract('GreClient.Rules.GreInterface');registered=registrations(initial[1])
    evidence=[source_evidence(initial[0],initial[1],initial[2])];done={initial[0]};failures=[]
    todo={r['handler'] if '.' in r['handler'] else name for r in registered for name in by_short.get(r['handler'],[])}
    for created in evidence[0]['created_types']:
        if any(word in created for word in ('Request','Translator','Parser')):
            todo.update(by_short.get(created.rsplit('.',1)[-1],[]))
    for short in ('ActionInfo','ActionExtensions','ActionInfoExtensions','CastingTimeOptionRequest','AnnotationParserFactory','StateParserFactory'):
        todo.update(by_short.get(short,[]))
    # Follow construction of request subclasses/translation helpers until the
    # closure is exhausted. No arbitrary list of supported requests is needed.
    with concurrent.futures.ThreadPoolExecutor(max_workers=jobs) as pool:
        while todo:
            batch=sorted(todo-done);todo=set()
            if not batch:break
            futures={pool.submit(extract,name):name for name in batch};done.update(batch)
            for future in concurrent.futures.as_completed(futures):
                name=futures[future]
                try:
                    name,source,path=future.result();record=source_evidence(name,source,path);evidence.append(record)
                    for created in record['created_types']:
                        if any(word in created for word in ('Request','Translator','Parser')):
                            todo.update(by_short.get(created.rsplit('.',1)[-1],[]))
                except Exception as error:failures.append({'class':name,'error':str(error)})
            print(f'Inspected {len(evidence)} client handlers/request classes',flush=True)
    # Factories often associate an enum with a parser without repeating that
    # enum inside the parser itself. Preserve those registrations separately.
    annotation_registrations=[]
    for record in evidence:
        if record['class'].endswith(('AnnotationParserFactory','StateParserFactory')):
            source=(output/record['source']).read_text(encoding='utf8')
            for annotation,handler in re.findall(r'AnnotationType\.(\w+)\s*,\s*new\s+([\w.]+)\s*\(',source):
                annotation_registrations.append({'annotation':annotation,'handler':handler,'factory_source':record['source']})
    return registered,sorted(evidence,key=lambda r:r['class']),annotation_registrations,failures

def write_reports(output,report):
    (output/'actions.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf8')
    with (output/'incoming-messages.csv').open('w',newline='',encoding='utf-8-sig') as file:
        writer=csv.writer(file);writer.writerow(['Message','Wire value','Payload type','Client handler','Registered','Runtime verified'])
        for row in report['incoming_messages']:writer.writerow([row['name'],row['value'],row['payload_type'] or '',row['client_handler'] or '',row['registered'],False])
    lines=['# Arena client action inventory','',f"Extracted at {report['generated_utc']} from `{report['managed_directory']}`.",'',
        'This is the declared protocol and static handler evidence for this client version. It does not prove that every enum value is usable, implement Forge adapters, or recover official server logic.','',
        '## Incoming game messages','', '| Message | Value | Payload | Handler found |','|---|---:|---|---|']
    for row in report['incoming_messages']:lines.append(f"| {row['name']} | {row['value']} | {row['payload_type'] or 'No unique field match'} | {'Yes' if row['registered'] else 'No'} |")
    for short in ('ActionType','CastingTimeOptionType','AnnotationType','ClientMessageType'):
        enum=unique_name(report['enums'],short);lines+=['',f'## {short}','', '| Wire enum | Value |','|---|---:|']
        lines += [f"| {v['name']} | {v['value']} |" for v in enum['values']]
    lines+=['','## Files','',
        '- `actions.json`: complete message/field/enum catalogue, handler references, fingerprints and limitations.',
        '- `incoming-messages.csv`: filterable server-to-client message list.',
        '- `arena-descriptor-set.pb` / `.json`: exact recovered protobuf descriptors.',
        '- `sources/`: decompiled handlers and their request classes.',
        '', 'Gameplay messages are wrapped in `GREToClientMessage`, then `GreToClientEvent`, then `MatchServiceToClientMessage`. State IDs, message IDs, game objects, seats and response correlation must agree. See the exact fields in the JSON catalogue.',
        '', 'ActionType describes entries offered in ActionsAvailableReq; animation/game-state annotations are separate. ClientMessageType contains replies sent by Arena, not additional server-to-client commands.','',
        '## Extraction limitations','']+[f'- {item}' for item in report['limitations']]
    if report['failures']:lines+=['','## Failures','']+[f"- {item['class']}: {item['error']}" for item in report['failures']]
    (output/'README.md').write_text('\n'.join(lines)+'\n',encoding='utf8')

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--managed',type=Path,default=Path('C:/Program Files/Wizards of the Coast/MTGA/MTGA_Data/Managed'))
    parser.add_argument('--output',type=Path,default=ROOT/'analysis/action-inventory')
    parser.add_argument('--jobs',type=int,default=4,choices=range(1,9))
    parser.add_argument('--refresh',action='store_true')
    parser.add_argument('--schema-only',action='store_true',help='Skip decompilation; handler support will be marked unknown')
    args=parser.parse_args();managed=args.managed.resolve();output=args.output.resolve()
    for name in REQUIRED:
        if not (managed/name).is_file():raise SystemExit(f'Missing client assembly: {managed/name}')
    if output==managed or managed in output.parents:raise SystemExit('Output must be outside the client Managed directory')
    output.mkdir(parents=True,exist_ok=True)
    fingerprints={name:hashlib.sha256((managed/name).read_bytes()).hexdigest() for name in REQUIRED}
    helper=ROOT/'tools/action-schema/ActionSchema.csproj';build=ROOT/'.tools/action-schema-bin'
    print('Reading exact protobuf descriptors from the selected client...',flush=True)
    run(['dotnet','build',helper,'-o',build,f'-p:ArenaManaged={managed}'])
    print(run(['dotnet',build/'ActionSchema.dll',managed,output]).strip(),flush=True)
    messages,enums=schema_index(json.loads((output/'arena-descriptor-set.json').read_text(encoding='utf8')))
    registered=[];evidence=[];annotation_registrations=[];failures=[]
    if not args.schema_only:
        tool=ROOT/'.tools/ilspycmd.exe'
        if not tool.is_file():raise SystemExit('Missing .tools/ilspycmd.exe; use --schema-only or restore the decompiler')
        registered,evidence,annotation_registrations,failures=decompile_handlers(tool,managed,output,args.jobs,args.refresh)
    incoming=incoming_catalogue(messages,enums,registered,evidence)
    action=unique_name(messages,'Action')
    if fingerprints!={name:hashlib.sha256((managed/name).read_bytes()).hexdigest() for name in REQUIRED}:
        raise RuntimeError('Client assemblies changed during extraction. Rerun against one stable installation.')
    report={'generated_utc':datetime.now(timezone.utc).isoformat(),'managed_directory':str(managed),
        'assembly_sha256':fingerprints,
        'incoming_messages':incoming,'action_message':action['name'],
        'action_types':unique_name(enums,'ActionType')['values'],
        'messages':messages,'enums':enums,'client_handler_evidence':evidence,'annotation_registrations':annotation_registrations,'failures':failures,
        'transport_envelopes':{name:unique_name(messages,name) for name in ('GREToClientMessage','GreToClientEvent','MatchServiceToClientMessage')},
        'limitations':['Enum declarations include unused/reserved/unsupported entries; handler presence is recorded separately.',
            'Payload fields use structural name matches or member accesses recovered from the handler; unmatched entries require inspection.',
            'Decompiler references are static evidence, not proof of successful live gameplay.',
            'Each command needs truthful rules state, legal choices, sequencing and matching responses.',
            'Unity assets, all animation variants and official server implementations are outside this inventory.']}
    write_reports(output,report)
    print(f"Done: {len(incoming)} incoming message enum entries, {len(report['action_types'])} action types, {len(messages)} messages, {len(enums)} enums; {len(failures)} failures.\nReports: {output}",flush=True)
    if failures:raise SystemExit(1)

if __name__=='__main__':
    try:main()
    except (RuntimeError,OSError,ValueError,subprocess.TimeoutExpired) as error:raise SystemExit(str(error))
