"""Export display allocations, art and public trust material; exclude server credentials."""
import hashlib
import json
from pathlib import Path
import sys
import zipfile

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from server import rulebreakers


def main():
    pack=ROOT/'data/custom-arena-cards'
    selected=[]
    metadata={'manifest.json','tokens.json','planeswalker-subtypes.json','emblem-display-abilities.json'}
    for path in pack.iterdir():
        if path.is_file() and (path.name in metadata or path.stem.isdigit() and path.suffix.lower() in ('.json','.jpg','.png')):
            selected.append((path,path.relative_to(ROOT).as_posix()))
    records=json.loads((pack/'manifest.json').read_text(encoding='utf8'))
    allowed=[pack.resolve(),(ROOT/'data/magic-import/art').resolve(),(ROOT/'data/magic-import/cards').resolve()]
    for record in records:
        for key in ('metadata','art'):
            path=(ROOT/record[key].replace('\\','/')).resolve(strict=True)
            if not any(path.is_relative_to(base) for base in allowed) or path.suffix.lower() not in ('.json','.jpg','.png'):
                raise RuntimeError('Unexpected display-pack asset path')
            selected.append((path,path.relative_to(ROOT).as_posix()))
    for name in ['data/local-client/tls/localhost.cer','data/local-client/tls/localhost.root.cer',
                 'data/mode-labels.json','Install-VPN-Client.ps1','Join-VPN-Server.ps1','Join-VPN-Server.bat',
                 'tools/vpn_relay.py','server/rulebreakers.py','tools/export_rulebreakers.py']:
        path=ROOT/name
        if path.exists():selected.append((path,name))
    selected.append((ROOT/'data/catalogue.json','data/vpn-server-catalogue.json'))
    selected=list({name:(path,name) for path,name in selected}.values())
    versions=json.loads((ROOT/'client-inspection/version').read_text(encoding='utf-8-sig'))['Versions']
    if len(versions)!=1:raise RuntimeError('Ambiguous host Arena version')
    version=next(iter(versions))
    target=ROOT/'data/exports'/('Arena-VPN-Client-'+version+'.zip')
    target.parent.mkdir(parents=True,exist_ok=True)
    entries=[]
    with zipfile.ZipFile(target.with_suffix('.tmp'), 'w', compression=zipfile.ZIP_STORED,allowZip64=True) as output:
        for index,(path,name) in enumerate(selected):
            content=path.read_bytes()
            output.writestr(name,content)
            entries.append({'path':name,'sha256':hashlib.sha256(content).hexdigest()})
            if index and index%5000==0:print('Packaged',index,'display files',flush=True)
        output.writestr('data/rulebreaker-definitions.json',json.dumps(rulebreakers.definitions(),ensure_ascii=False))
        output.writestr('data/vpn-client-pack.json',json.dumps({'version':version,'files':entries,
            'note':'Display data and public certificates only. Server accounts and private keys are excluded.'},indent=2))
    target.with_suffix('.tmp').replace(target)
    print('Client display pack:',target,flush=True)


if __name__=='__main__':main()
