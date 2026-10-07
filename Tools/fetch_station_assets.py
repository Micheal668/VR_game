"""Download CC0 Poly Haven station props, preserving verifiable provenance."""
from pathlib import Path
import concurrent.futures, hashlib, json, urllib.request, time

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Models/LunarBase/Downloads'
IDS = ['combination_wrench', 'metal_toolbox', 'medical_box', 'circuit_board', 'power_box_01', 'portable_welding_cart']
OUT.mkdir(parents=True, exist_ok=True)

def get(url):
    for attempt in range(4):
        try:
            req = urllib.request.Request(url, headers={'User-Agent':'LunarEscapeVR asset import (CC0 attribution retained)'})
            with urllib.request.urlopen(req, timeout=90) as r:
                return r.read()
        except Exception:
            if attempt == 3: raise
            time.sleep(attempt + 1)

def download(job):
    target, record = job
    target.parent.mkdir(parents=True, exist_ok=True)
    data = target.read_bytes() if target.exists() else get(record['url'])
    if hashlib.md5(data).hexdigest() != record['md5']:
        raise ValueError('MD5 mismatch: ' + str(target))
    target.write_bytes(data)
    return {'path': str(target.relative_to(ROOT)).replace('\\','/'), 'url': record['url'], 'bytes': len(data),
            'md5': record['md5'], 'sha256': hashlib.sha256(data).hexdigest()}

def fetch_asset(asset_id):
    directory = OUT / asset_id
    directory.mkdir(exist_ok=True)
    info = json.loads(get('https://api.polyhaven.com/info/' + asset_id))
    files = json.loads(get('https://api.polyhaven.com/files/' + asset_id))
    (directory/'source-info.json').write_text(json.dumps(info, indent=2), encoding='utf-8')
    (directory/'source-files.json').write_text(json.dumps(files, indent=2), encoding='utf-8')
    jobs = []
    for fmt in ['fbx', 'blend']:
        record = files[fmt]['2k'][fmt]
        jobs.append((directory / (asset_id + '_2k.' + fmt), record))
    for category, ext in [('Diffuse','jpg'),('nor_gl','png'),('arm','png')]:
        record = files[category]['2k'][ext]
        name = record['url'].rsplit('/',1)[-1]
        jobs.append((directory/'textures'/name, record))
    if 'Alpha' in files:
        record = files['Alpha']['2k']['png']
        jobs.append((directory/'textures'/record['url'].rsplit('/',1)[-1], record))
    with concurrent.futures.ThreadPoolExecutor(max_workers=5) as pool:
        downloaded = list(pool.map(download, jobs))
    # Original Blender files use EXR paths; these remain source references. The prepared
    # Unity material manifest points to exact downloaded/converted PNG/JPG textures.
    result = {'id':asset_id,'name':info['name'],'source_page':'https://polyhaven.com/a/'+asset_id,
              'info_api':'https://api.polyhaven.com/info/'+asset_id,'files_api':'https://api.polyhaven.com/files/'+asset_id,
              'author':list(info['authors']), 'license':'CC0-1.0', 'license_page':'https://polyhaven.com/license',
              'license_text':'https://creativecommons.org/publicdomain/zero/1.0/',
              'source_dimensions_mm':info.get('dimensions'), 'source_triangles':info.get('polycount'),
              'downloaded_at_utc':time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime()), 'files':downloaded}
    (directory/'source-manifest.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(asset_id, 'OK', info.get('polycount'), 'tris', info.get('dimensions'), flush=True)
    return result

if __name__ == '__main__':
    results = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        for result in pool.map(fetch_asset, IDS):
            results.append(result)
    manifest_path = OUT/'source-manifest.json'
    if manifest_path.exists():
        prior = json.loads(manifest_path.read_text(encoding='utf-8'))
        results.extend(a for a in prior.get('assets',[]) if a['id'] not in IDS)
    manifest_path.write_text(json.dumps({'assets':results},indent=2),encoding='utf-8')
    print('All source model MD5 hashes verified.', flush=True)
