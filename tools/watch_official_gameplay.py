"""Continuously save new gameplay envelopes from Arena's detailed client log."""
import argparse, collections, datetime, json, time
from pathlib import Path
from collect_arena_trace import collect

parser=argparse.ArgumentParser()
parser.add_argument('--output',type=Path,required=True)
args=parser.parse_args()
log=Path.home()/'AppData/LocalLow/Wizards Of The Coast/MTGA/Player.log'
args.output.mkdir(parents=True,exist_ok=True)
offset=log.stat().st_size if log.exists() else 0
buffer=''; previous=[]; modified=log.stat().st_mtime_ns if log.exists() else 0
(args.output/'session.json').write_text(json.dumps({'startedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'source':'Official Arena client detailed log','status':'recording','log':str(log)},indent=2),encoding='utf8')
while not (args.output/'stop-recording').exists():
    try:
        info=log.stat()
        if info.st_size<offset or info.st_size==offset and info.st_mtime_ns!=modified:
            previous.extend(collect(buffer));buffer='';offset=0
        with log.open('rb') as stream:
            stream.seek(offset);chunk=stream.read();offset=stream.tell()
        modified=info.st_mtime_ns
        if chunk:
            buffer+=chunk.decode('utf8',errors='replace')
            messages=previous+collect(buffer)
            temporary=args.output/'gameplay.tmp'
            temporary.write_text(''.join(json.dumps(m,ensure_ascii=False)+'\n' for m in messages),encoding='utf8')
            temporary.replace(args.output/'gameplay.jsonl')
            counts=collections.Counter(m['message'].get('type','unknown') for m in messages)
            (args.output/'summary.json').write_text(json.dumps({'status':'recording','messages':len(messages),
                'types':dict(counts),'updatedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()},indent=2),encoding='utf8')
    except (FileNotFoundError,PermissionError,OSError):
        pass
    time.sleep(2)
