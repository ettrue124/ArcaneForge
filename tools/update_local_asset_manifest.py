"""Describe modified local card files with local content-addressed manifests."""
import base64
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))


def main():
    state = ROOT/'data/local-client'
    installation = Path(json.loads((state/'configuration-state.json').read_text(encoding='utf-8-sig'))['installation'])
    downloads = installation/'MTGA_Data/Downloads'
    config_path = state/'bootstrap.json'
    original = config_path.read_bytes()
    config = json.loads(original.decode('utf-8-sig'))
    prefixes = ('Raw_CardDatabase_', 'Raw_ClientLocalization_', 'ALT_Card_')
    assets_path = installation/'MTGA_Data/StreamingAssets/assets.conf'
    assets_bytes = assets_path.read_bytes()
    assets_config = json.loads(assets_bytes.decode('utf-8-sig'))
    manifest_cache = state/'asset-manifests'
    manifest_cache.mkdir(exist_ok=True)
    changed = 0
    for item in config['manifests']:
        category = item.get('category', '')
        if category not in ('', 'Localization'): continue
        stem = 'Manifest_' + (category+'_' if category else '')
        source = downloads/(stem+item['hash']+'.mtga')
        if not source.exists():
            retained = manifest_cache/source.name
            if retained.exists(): source.write_bytes(retained.read_bytes())
            else:
                native = next((m for m in assets_config.get('manifests', []) if m.get('category', '') == category), None)
                fallback = downloads/(stem+native['hash']+'.mtga') if native else None
                if fallback is None or not fallback.exists():
                    raise ValueError('Configured local manifest is missing: '+source.name)
                source = fallback
        document = json.loads(source.read_text(encoding='utf-8-sig'))
        modified = False
        for asset in document.get('Assets', []):
            name = asset.get('Name', '')
            if not name.startswith(prefixes): continue
            local = downloads/asset['AssetType']/name
            if not local.is_file(): raise ValueError('Installed local asset is missing: '+name)
            digest = hashlib.sha256()
            with local.open('rb') as stream:
                for chunk in iter(lambda: stream.read(1024*1024), b''): digest.update(chunk)
            size = local.stat().st_size
            checksum = base64.b64encode(digest.digest()).decode('ascii')
            if asset.get('Length') == size and asset.get('sha256') == checksum: continue
            asset.update(Length=size, sha256=checksum)
            modified = True
            changed += 1
        if not modified:
            (manifest_cache/source.name).write_bytes(source.read_bytes())
            continue
        payload = (json.dumps(document, ensure_ascii=False, separators=(',', ':'))+'\n').encode('utf-8')
        identity = hashlib.md5(payload).hexdigest()
        target = downloads/(stem+identity+'.mtga')
        target.write_bytes(payload)
        (manifest_cache/target.name).write_bytes(payload)
        item['hash'] = identity
    # Localization is selected during early startup from assets.conf, before
    # the local account bootstrap can supply its manifest list.
    for native in assets_config.get('manifests', []):
        if native.get('category') != 'Localization': continue
        local = next((m for m in config['manifests'] if m.get('category') == 'Localization'), None)
        if local: native['hash'] = local['hash']
    updated_assets = (json.dumps(assets_config, indent=2)+'\n').encode('utf-8')
    if updated_assets != assets_bytes:
        backup_assets = state/('assets.before-local-manifests-'+hashlib.sha256(assets_bytes).hexdigest()[:16]+'.json')
        if not backup_assets.exists(): backup_assets.write_bytes(assets_bytes)
        assets_path.write_bytes(updated_assets)
    if changed:
        backup = state/('bootstrap.before-local-assets-'+hashlib.sha256(original).hexdigest()[:16]+'.json')
        if not backup.exists(): backup.write_bytes(original)
        temporary = config_path.with_suffix('.tmp')
        temporary.write_text(json.dumps(config, indent=2)+'\n', encoding='utf-8')
        temporary.replace(config_path)
    print(f'Local asset manifests aligned: {changed} file records updated.')


if __name__ == '__main__': main()
