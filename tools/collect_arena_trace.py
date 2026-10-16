"""Collect gameplay protocol examples from your own local Arena Player.log.

Reads local files only. Does not contact Wizards servers, collect login packets,
or download assets. Run after a normal match with detailed logging enabled.
"""
import argparse
import collections
import datetime
import json
import os
from pathlib import Path

def collect(text):
    decoder=json.JSONDecoder()
    messages=[]
    lines=text.splitlines(keepends=True)
    for offset,line in enumerate(lines):
        # JSON decode only known gameplay envelopes; never export the full log.
        if not line.lstrip().startswith('{'):
            continue
        yield_line=line
        # Pretty client envelopes extend over multiple lines.
        if line.strip()=='{':
            yield_line=''.join(lines[offset:offset+250])
        try:
            envelope,_=decoder.raw_decode(yield_line.lstrip())
        except (ValueError,TypeError):
            continue
        for message in envelope.get('greToClientEvent',{}).get('greToClientMessages',[]):
            if isinstance(message,dict): messages.append({'direction':'server','message':message})
        kind=envelope.get('clientToMatchServiceMessageType','')
        payload=envelope.get('payload')
        if kind.endswith(('ClientToGREMessage','ClientToGREUIMessage')) and isinstance(payload,dict):
            messages.append({'direction':'client','message':payload})
    return messages

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    default=Path(os.environ.get('USERPROFILE','~'))/'AppData/LocalLow/Wizards Of The Coast/MTGA/Player.log'
    parser.add_argument('--log',type=Path,default=default)
    parser.add_argument('--output',type=Path)
    args=parser.parse_args()
    output=args.output or Path(__file__).resolve().parents[1]/'analysis/arena-traces'/datetime.datetime.now().strftime('%Y%m%d-%H%M%S')
    messages=collect(args.log.read_text(encoding='utf8',errors='replace'))
    output.mkdir(parents=True,exist_ok=True)
    (output/'gameplay.jsonl').write_text(''.join(json.dumps(m)+'\n' for m in messages),encoding='utf8')
    counts=collections.Counter(m['message'].get('type','unknown') for m in messages)
    annotations=collections.Counter(t for m in messages for a in m['message'].get('gameStateMessage',{}).get('annotations',[]) for t in a.get('type',[]))
    report={'messages':len(messages),'types':dict(counts),'annotations':dict(annotations),
        'limitation':'Summarized game states cannot be recovered from logs. Only your logged match events are collected; this is not all Arena animations.'}
    (output/'summary.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    print(f'Collected {len(messages)} gameplay messages into {output}')
    print(report['limitation'])

if __name__=='__main__': main()
