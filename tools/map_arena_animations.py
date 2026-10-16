"""Discover Arena protocol -> rules -> visual playback references locally.

Produces a static evidence map, not executable adapters. No official server access
or client modification. Unknown semantics are left for runtime validation.
"""
import argparse
import collections
import concurrent.futures
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]

def run(command):
    result=subprocess.run([str(x) for x in command],capture_output=True,text=True,encoding='utf8',errors='replace',timeout=180)
    if result.returncode: raise RuntimeError(result.stderr.strip()[:500] or f'Decompiler exited {result.returncode}')
    return result.stdout

def types(tool,assembly):
    return [line[6:].strip() for line in run([tool,'-l','c',assembly]).splitlines() if line.startswith('Class ') and '+' not in line]

def analyze_parser(name,source):
    annotations=sorted(set(re.findall(r'AnnotationType\.(\w+)',source)))
    keys=sorted(set(re.findall(r'const string \w+\s*=\s*"([^"\n]+)"',source)+
        re.findall(r'\.Key\s*==\s*"([^"\n]+)"',source)+re.findall(r'\.Key\.Equals\("([^"\n]+)"\)',source)))
    created=sorted(set(re.findall(r'new\s+([A-Z]\w*)\s*(?:\(|\{)',source)))
    return {'class':name,'annotations':annotations,'detail_key_candidates':keys,'created_types':created}

def analyze_translator(name,source,rule_types):
    primary=set(re.findall(r'allChanges\[changeIndex\]\s+(?:as|is)\s+([A-Z]\w*)',source))
    consumed=sorted((primary or set(re.findall(r'\b(?:as|is)\s+([A-Z]\w*)',source))) & rule_types)
    created=sorted(set(re.findall(r'new\s+([A-Z]\w*)\s*(?:\(|\{)',source)))
    return {'class':name,'rule_type_candidates':consumed,'created_types':created}

def emitted_annotations(sources):
    """Find explicit emission sites, including conditional and journal adapters.

    A reference in an assertion, comparison or comment is not an emission site.
    This remains a static inventory; it does not prove the branch executes.
    """
    emitted=set()
    for source in sources:
        source=re.sub(r'/\*.*?\*/|//[^\n]*', '', source, flags=re.S)
        sites=re.findall(r'\bType\s*=\s*\{([^{}]*)\}',source)
        sites+=re.findall(r'\.Type\.Add\s*\(([^;]*)\)\s*;',source)
        for site in sites:
            emitted.update(re.findall(r'AnnotationType\.(\w+)',site))
    return emitted

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--managed',type=Path,default=Path('C:/Program Files/Wizards of the Coast/MTGA/MTGA_Data/Managed'))
    parser.add_argument('--output',type=Path,default=ROOT/'analysis/animation-map')
    parser.add_argument('--jobs',type=int,default=4)
    parser.add_argument('--refresh',action='store_true')
    args=parser.parse_args()
    tool=ROOT/'.tools/ilspycmd.exe'
    if not tool.exists(): raise SystemExit('Missing .tools/ilspycmd.exe')
    assemblies={name:args.managed/f'{name}.dll' for name in ['SharedClientCore','Core']}
    output=args.output.resolve();output.mkdir(parents=True,exist_ok=True)
    fingerprints={name:hashlib.sha256(path.read_bytes()).hexdigest() for name,path in assemblies.items()}
    catalogues={name:types(tool,path) for name,path in assemblies.items()}
    jobs=[(name,type_name) for name,names in catalogues.items() for type_name in names if
        name=='SharedClientCore' and '.Annotations.' in type_name and ('Parser' in type_name or 'Paraser' in type_name)
        or name=='Core' and '.DuelScene.UXEvents.' in type_name and 'Translator' in type_name]
    sources={};failures=[]
    def decompile(job):
        assembly,name=job
        path=output/'sources'/fingerprints[assembly][:12]/(name+'.cs')
        if args.refresh or not path.exists():
            source=run([tool,'-t',name,assemblies[assembly]])
            path.parent.mkdir(parents=True,exist_ok=True);path.write_text(source,encoding='utf8')
        else: source=path.read_text(encoding='utf8')
        return job,source,str(path.relative_to(output))
    def batch(work):
        with concurrent.futures.ThreadPoolExecutor(max_workers=max(1,min(args.jobs,8))) as pool:
            futures={pool.submit(decompile,job):job for job in work}
            for count,future in enumerate(concurrent.futures.as_completed(futures),1):
                job=futures[future]
                try:
                    _,source,path=future.result();sources[job]=(source,path)
                except Exception as error: failures.append({'assembly':job[0],'class':job[1],'error':str(error)})
                if count%20==0 or count==len(work):print(f'Decompiled {count}/{len(work)} types',flush=True)
    print(f'Inspecting {len(jobs)} annotation parsers and visual translators',flush=True)
    batch(jobs)
    parsers=[analyze_parser(name,source)|{'source':path} for (assembly,name),(source,path) in sources.items() if assembly=='SharedClientCore' and not name.endswith(('Factory','Aggregate'))]
    registrations=collections.defaultdict(set)
    for (assembly,name),(source,_) in sources.items():
        if assembly=='SharedClientCore' and name.endswith('Factory'):
            for annotation,implementation in re.findall(r'AnnotationType\.(\w+)\s*,\s*new\s+(\w+)',source):
                registrations[implementation].add(annotation)
    for p in parsers:
        p['annotations']=sorted(set(p['annotations']) | registrations[p['class'].rsplit('.',1)[-1]])
    rules={kind for p in parsers for kind in p['created_types']}
    translators=[analyze_translator(name,source,rules)|{'source':path} for (assembly,name),(source,path) in sources.items() if assembly=='Core']
    visual_names={kind for t in translators for kind in t['created_types']}
    visual_jobs=[('Core',name) for name in catalogues['Core'] if
        (name.rsplit('.',1)[-1] in visual_names or '.DuelScene.UXEvents.' in name and 'UXEvent' in name.rsplit('.',1)[-1] and 'Translator' not in name)
        and ('Core',name) not in sources]
    print(f'Inspecting {len(visual_jobs)} referenced visual playback types',flush=True)
    batch(visual_jobs)
    visuals=[]
    for job in visual_jobs:
        if job not in sources:continue
        source,path=sources[job]
        visuals.append({'class':job[1],'source':path,
            'audio_events':sorted(set(re.findall(r'WwiseEvents\.(\w+)',source))),
            'asset_lookup_types':sorted(set(re.findall(r'(?:LoadTree|AssetLookupTree)<([^>]+)>',source))),
            'asset_payload_references':sorted(set(re.findall(r'\b\w+\.(\w*(?:Ref|AudioEvent|Animation|EffectPrefab)\w*)\b',source))),
            'animation_references':sorted(set(re.findall(r'\b\w*(?:Animation|Effect|Spline)\w*\b',source))),
            'string_reference_candidates':sorted(set(re.findall(r'"([^"\n]{1,200})"',source)))})
    visual_by_name={v['class'].rsplit('.',1)[-1]:v for v in visuals}
    emitted=emitted_annotations(p.read_text(encoding='utf8') for p in (ROOT/'protocol-host').glob('*.cs')
        if p.name!='Program.cs' and not p.stem.endswith('Tests'))
    mappings=[]
    for p in parsers:
        for annotation in p['annotations']:
            candidates=[t for t in translators if set(t['rule_type_candidates']) & set(p['created_types'])]
            mappings.append({'annotation':annotation,'parser':p,'translator_candidates':candidates,
                'visual_candidates':[visual_by_name[k] for t in candidates for k in t['created_types'] if k in visual_by_name],
                'bridge_emits_annotation':annotation in emitted,'validation':'static references; runtime semantics unverified'})
    assets_root=args.managed.parent
    assets=[{'path':str(p.relative_to(assets_root)),'bytes':p.stat().st_size} for p in assets_root.rglob('*') if p.is_file() and p.suffix.lower() in ['.bundle','.unity3d','.asset','.assets','.anim','.controller','.mtga']]
    report={'dll_sha256':fingerprints,'parsers':parsers,'translators':translators,'visuals':visuals,'mappings':mappings,'asset_files':assets,'failures':failures,
        'ux_type_inventory': [name for name in catalogues['Core'] if '.DuelScene.UXEvents.' in name],
        'limitations':['Static candidates are not a complete call graph.','Playback inventory includes every nonnested UXEvent-named class in the DuelScene.UXEvents namespace plus referenced playback types; it includes base classes and tutorial UI.','Dynamic asset lookups and Unity bundle contents are not enumerated.','A Forge adapter must supply truthful event source, targets, ordering and details.','This report does not change the private server or enable animations automatically.']}
    (output/'animation-map.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    lines=['# Arena animation mapping inventory','','Generated from installed client DLLs. These are static reference candidates, requiring gameplay validation.','',
        '| Annotation | Detail-key candidates | Visual path candidates | Bridge explicit annotation |','|---|---|---|---|']
    for m in sorted(mappings,key=lambda m:m['annotation']):
        paths=', '.join(sorted(set(v['class'].rsplit('.',1)[-1] for v in m['visual_candidates']))) or 'No direct visual path discovered'
        lines.append(f"| {m['annotation']} | {', '.join(m['parser']['detail_key_candidates'])} | {paths} | {'Yes' if m['bridge_emits_annotation'] else 'Not emitted explicitly'} |")
    lines+=['','## Limits','']+['- '+item for item in report['limitations']]
    lines+=['','Some playback is generated from state differences without an explicit annotation. A missing annotation entry does not by itself prove a missing feature.']
    (output/'animation-map.md').write_text('\n'.join(lines)+'\n',encoding='utf8')
    playback_lines=['# Arena visual playback contracts','',
        'Recovered with ILSpy from the installed client. Includes all nonnested UXEvent-named classes in DuelScene.UXEvents plus playback classes reached by annotation/event mapping. Base classes, scheduling and tutorial events are included; these are not a count of unique Unity clips or cosmetic variations.', '',
        '| Playback class | Asset lookup types | Asset/audio field references | Animation/effect symbol candidates | Direct audio events | Decompiled source |',
        '|---|---|---|---|---|---|']
    for v in sorted(visuals,key=lambda v:v['class']):
        playback_lines.append('| '+v['class'].rsplit('.',1)[-1]+' | '+', '.join(v['asset_lookup_types'])+' | '+', '.join(v['asset_payload_references'])+' | '+', '.join(v['animation_references'])+' | '+', '.join(v['audio_events'])+' | [source]('+v['source']+') |')
    playback_lines+=['','The asset field names are code references. Their concrete Unity bundle payloads, runtime-selected variants and visible timing remain unverified. See animation-map.md for message-to-playback candidates.']
    (output/'playback-contracts.md').write_text('\n'.join(playback_lines)+'\n',encoding='utf8')
    print(f'Mapped {len(mappings)} annotation/parser relationships; {len(visuals)} playback types; {len(failures)} failures. Reports: {output}',flush=True)
    if failures:raise SystemExit(1)

if __name__=='__main__':main()
