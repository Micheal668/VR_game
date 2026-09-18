"""Convert the bundled NASA static GLB into Unity mesh data, without a runtime glTF package.
Run with Python 3.10, DracoPy 2.0.0 and numpy 2.2.6. PNG texture bytes are extracted unchanged.
"""
from pathlib import Path
import struct, json, hashlib, argparse
import numpy as np
import DracoPy
parser=argparse.ArgumentParser()
parser.add_argument('--asset-subdir',default='ApolloLM')
parser.add_argument('--filename',default='ApolloLunarModule.glb')
parser.add_argument('--output',default='ApolloMeshes.bytes')
parser.add_argument('--source-url',default='https://assets.science.nasa.gov/content/dam/science/cds/3d/resources/model/apollo-lunar-module/Apollo%20Lunar%20Module.glb')
parser.add_argument('--credit',default='NASA/Michael D. Carbajal')
args=parser.parse_args()
ROOT = Path(__file__).resolve().parents[1] / 'Assets/_LunarEscape/Art' / args.asset_subdir
source = ROOT / args.filename
b = source.read_bytes()
assert b[:4] == b'glTF'
n, kind = struct.unpack_from('<II', b, 12)
d = json.loads(b[20:20+n])
bn, bt = struct.unpack_from('<II', b, 20+n)
raw = b[28+n:28+n+bn]
def view(index):
    v = d['bufferViews'][index]
    return raw[v.get('byteOffset', 0):v.get('byteOffset', 0)+v['byteLength']]
for i, image in enumerate(d.get('images', [])):
    if image['mimeType'] == 'image/png':
        (ROOT / f'texture-{i}.png').write_bytes(view(image['bufferView']))
groups = {i: [] for i in range(len(d['materials']))}
def matrix(node):
    if 'matrix' in node: return np.array(node['matrix']).reshape(4, 4).T
    x,y,z,w = node.get('rotation', [0,0,0,1])
    r = np.array([[1-2*y*y-2*z*z,2*x*y-2*z*w,2*x*z+2*y*w], [2*x*y+2*z*w,1-2*x*x-2*z*z,2*y*z-2*x*w], [2*x*z-2*y*w,2*y*z+2*x*w,1-2*x*x-2*y*y]])
    out = np.eye(4); out[:3,:3] = r @ np.diag(node.get('scale', [1,1,1])); out[:3,3] = node.get('translation',[0,0,0]); return out
def visit(index, parent):
    node=d['nodes'][index]; world=parent @ matrix(node)
    if 'mesh' in node:
        for p in d['meshes'][node['mesh']]['primitives']:
            assert p.get('mode',4)==4
            c=p['extensions']['KHR_draco_mesh_compression']; mesh=DracoPy.decode(view(c['bufferView']))
            points=mesh.points @ world[:3,:3].T + world[:3,3]
            normals=mesh.normals @ np.linalg.inv(world[:3,:3])
            normals /= np.linalg.norm(normals,axis=1)[:,None]
            points[:,2]*=-1; normals[:,2]*=-1
            uv=mesh.tex_coord.copy() if mesh.tex_coord is not None else np.zeros((len(points),2)); uv[:,1]=1-uv[:,1]
            faces=mesh.faces[:,[0,2,1]]
            if np.linalg.det(world[:3,:3])<0: faces=faces[:,[0,2,1]]
            groups[p.get('material',0)].append((points,normals,uv,faces))
    for child in node.get('children',[]): visit(child,world)
for node in d['scenes'][d.get('scene',0)]['nodes']: visit(node,np.eye(4))
allpoints=np.concatenate([part[0] for parts in groups.values() for part in parts]);print('Unity-handed bounds:',allpoints.min(axis=0),allpoints.max(axis=0),'vertices',len(allpoints))
with (ROOT/args.output).open('wb') as f:
    f.write(b'LML1');f.write(struct.pack('<i',len(d['materials'])))
    for m in d['materials']:
        p=m.get('pbrMetallicRoughness',{});tex=p.get('baseColorTexture',{}).get('index',-1)
        image=d['textures'][tex]['source'] if tex>=0 else -1
        f.write(struct.pack('<6fi',*p.get('baseColorFactor',[1,1,1,1]),p.get('metallicFactor',1),p.get('roughnessFactor',1),image))
    f.write(struct.pack('<i',len(groups)))
    for i,parts in groups.items():
        offset=0;faces=[]
        for vertices,_,_,tri in parts: faces.append(tri+offset);offset+=len(vertices)
        v=np.concatenate([p[0] for p in parts]);norm=np.concatenate([p[1] for p in parts]);uv=np.concatenate([p[2] for p in parts]);tri=np.concatenate(faces)
        f.write(struct.pack('<iii',i,len(v),tri.size))
        for arr,dtype in [(v,'<f4'),(norm,'<f4'),(uv,'<f4'),(tri,'<i4')]:f.write(arr.astype(dtype).tobytes())
summary={'source_url':args.source_url,'source_credit':args.credit,'sha256':hashlib.sha256(b).hexdigest(),'vertices':len(allpoints),'triangles':sum(len(p[3]) for parts in groups.values() for p in parts),'material_groups':len(groups),'bounds_min':allpoints.min(axis=0).tolist(),'bounds_max':allpoints.max(axis=0).tolist(),'texture_processing':'PNG bytes extracted unchanged; UV V converted to Unity convention'}
(ROOT/'source.json').write_text(json.dumps(summary,indent=2)+'\n')
print(summary)
