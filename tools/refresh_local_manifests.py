"""Align loopback content manifests with a hash observed in the installed client.

No account endpoints or credentials are used. Existing configuration is backed up.
"""
import argparse
from datetime import datetime, timezone
import gzip
import hashlib
import json
from pathlib import Path
import re
import urllib.request

ROOT=Path(__file__).resolve().parents[1]

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('installation',type=Path)
    parser.add_argument('--general-hash',required=True)
    args=parser.parse_args()
    if not re.fullmatch('[a-f0-9]{32}',args.general_hash):raise ValueError('Invalid manifest hash')
    installation=args.installation.resolve()
    version=json.loads((installation/'version').read_text(encoding='utf-8-sig'))['Versions']
    assets=json.loads((installation/'MTGA_Data/StreamingAssets/assets.conf').read_text(encoding='utf-8-sig'))
    with urllib.request.urlopen('https://assets.mtgarena.wizards.com/Manifest_'+args.general_hash+'.mtga',timeout=30) as response:
        payload=response.read(30_000_001)
    if len(payload)>30_000_000:raise ValueError('Manifest too large')
    if payload[:2]==b'\x1f\x8b':payload=gzip.decompress(payload)
    manifest=json.loads(payload)
    if not isinstance(manifest.get('Assets'),list) or not manifest['Assets']:raise ValueError('Empty or unsupported asset manifest')
    config_path=ROOT/'data/local-client/bootstrap.json'
    config=json.loads(config_path.read_text(encoding='utf-8-sig'))
    manifests=[dict(priority=50,category='',hash=args.general_hash)]
    # The installed assets.conf is authoritative for localization. Preserve
    # separately configured categories when no replacement is known.
    native=[m for m in assets.get('manifests',[]) if m.get('category')]
    replaced={m['category'] for m in native}|{''}
    manifests.extend(m for m in config.get('manifests',[]) if m['category'] not in replaced)
    manifests.extend(native)
    from server.bootstrap import Bootstrap
    Bootstrap._manifests(manifests)
    stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    backup=config_path.with_name('bootstrap.before-manifest-fix-'+stamp+'.json')
    backup.write_bytes(config_path.read_bytes())
    config['manifests']=manifests
    temporary=config_path.with_suffix('.tmp')
    temporary.write_text(json.dumps(config,indent=2)+'\n',encoding='utf8')
    temporary.replace(config_path)
    (ROOT/'data/local-client/manifest-version.json').write_text(json.dumps(dict(
        versions=version,generalHash=args.general_hash,manifestSha256=hashlib.sha256(payload).hexdigest(),
        source='Installed client Automatic manifest reference; official asset CDN',backup=str(backup)),indent=2)+'\n',encoding='utf8')
    print('Updated local manifest configuration for installed client; previous configuration backed up.')

if __name__=='__main__':
    import sys
    sys.path.insert(0,str(ROOT))
    main()
