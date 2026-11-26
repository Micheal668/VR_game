"""Bake the Artemis cinematic's static Unity meshes. Requires numpy, Pillow and DracoPy.

Inputs are the cache paths for NASA's assembled Mobile Launcher GLB and EO-1 Cape
Canaveral JPEG, supplied as the two command-line arguments. The game only needs
the resulting assets. Source credits and URLs are in the output directory.
"""
from collections import defaultdict
from pathlib import Path
import io
import json
import math
import re
import struct
import sys

import DracoPy
import numpy as np
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets/_LunarEscape/Art/ArtemisPad"
TEMPLATE = (ROOT / "Assets/_LunarEscape/Art/EarthReturn/Canopy.asset").read_text("utf8")
GROUND = -24.6
DECK = -16.83
HATCH = 82.0
manifest = []
rng = np.random.default_rng(3902)


def vector(v):
    return "{x: %.7g, y: %.7g, z: %.7g}" % tuple(v)


class Model:
    def __init__(self, name, origin=(0, 0, 0)):
        self.name = name
        self.origin = np.asarray(origin)
        self.parts = defaultdict(list)

    def add(self, surface, vertices, normals, uv, indices):
        self.parts[surface].append((np.asarray(vertices) - self.origin, normals, uv, indices))

    def quad(self, surface, a, b, c, d, uv=None):
        v = np.asarray([a, b, c, d], dtype=float)
        n = np.cross(v[1] - v[0], v[2] - v[0])
        n /= max(np.linalg.norm(n), .000001)
        if surface == "Turf" and n[1] < 0:
            v = v[::-1]
            n = -n
        if uv is None:
            w, h = np.linalg.norm(v[1] - v[0]) / 8, np.linalg.norm(v[2] - v[1]) / 8
            uv = [(0, 0), (w, 0), (w, h), (0, h)]
        self.add(surface, v, [n] * 4, uv, [0, 1, 2, 0, 2, 3])

    def box(self, surface, pos, size, yaw=0):
        v = np.asarray([(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)], dtype=float)
        v *= np.asarray(size) / 2
        a = math.radians(yaw)
        v = v @ np.array([[math.cos(a),0,-math.sin(a)],[0,1,0],[math.sin(a),0,math.cos(a)]]) + pos
        for face in [(0,3,2,1),(4,5,6,7),(0,4,7,3),(1,2,6,5),(3,7,6,2),(0,1,5,4)]:
            self.quad(surface, *v[list(face)])

    def tube(self, surface, a, b, radius, end_radius=None, sides=8):
        a, b = np.asarray(a, dtype=float), np.asarray(b, dtype=float)
        d = b - a
        d /= np.linalg.norm(d)
        u = np.cross(d, [0, 1, 0] if abs(d[1]) < .9 else [0, 0, 1])
        u /= np.linalg.norm(u)
        v = np.cross(d, u)
        faces, pos, norms, uv = [], [], [], []
        end_radius = radius if end_radius is None else end_radius
        for i in range(sides + 1):
            phi = 2 * math.pi * i / sides
            n = math.cos(phi) * u + math.sin(phi) * v
            pos.extend((a + radius*n, b + end_radius*n))
            norms.extend([n, n])
            uv.extend([(i/sides, 0), (i/sides, 1)])
            if i < sides:
                k = 2*i
                faces.extend((k,k+2,k+1,k+2,k+3,k+1))
        self.add(surface, pos, norms, uv, faces)

    def round_tank(self, surface, center, profile, sides=32):
        x,y,z = center
        for (height,r), (next_height,next_r) in zip(profile, profile[1:]):
            self.tube(surface, (x,y+height,z), (x,y+next_height,z), r, next_r, sides)

    def line(self, surface, a, b, radius, sag=0, steps=8):
        a, b = np.asarray(a), np.asarray(b)
        last = a
        for i in range(1, steps+1):
            t=i/steps
            p=a+(b-a)*t+[0,-4*sag*t*(1-t),0]
            self.tube(surface,last,p,radius,sides=5)
            last=p

    def bake(self):
        # Use the project's Unity 6 mesh layout. Submeshes are batched per surface.
        verts, indices, pieces, base, first = [], [], [], 0, 0
        for surface, chunks in self.parts.items():
            start = base
            coords = []
            for vs, ns, uv, ids in chunks:
                coords.append(vs)
                verts.append(np.column_stack((vs,ns,uv)).astype("<f4"))
                indices.extend((np.asarray(ids).ravel()+base).tolist())
                base += len(vs)
            coords=np.concatenate(coords)
            low, high=coords.min(0),coords.max(0)
            pieces.append((first,len(indices)-first,start,base-start,low,high))
            first=len(indices)
        stride = 2 if base < 65536 else 4
        bounds = ""
        for first,count,start,size,lo,hi in pieces:
            bounds += (f"  - serializedVersion: 2\n    firstByte: {first*stride}\n    indexCount: {count}\n"
                       f"    topology: 0\n    baseVertex: 0\n    firstVertex: {start}\n    vertexCount: {size}\n    localAABB:\n"
                       f"      m_Center: {vector((lo+hi)/2)}\n      m_Extent: {vector((hi-lo)/2)}\n")
        raw = np.concatenate(verts)
        lo,hi = raw[:,:3].min(0),raw[:,:3].max(0)
        yaml = re.sub(r"(?s)(  m_SubMeshes:\n).*?(  m_Shapes:)",r"\g<1>"+bounds+r"\g<2>",TEMPLATE)
        yaml = re.sub(r"(?m)^  m_Name: .*", "  m_Name: "+self.name, yaml)
        yaml = re.sub(r"(?m)^  m_IndexFormat: \d+", f"  m_IndexFormat: {int(stride==4)}", yaml)
        yaml = re.sub(r"(?m)^  m_IndexBuffer: .*", "  m_IndexBuffer: "+np.asarray(indices,dtype=f"<u{stride}").tobytes().hex(),yaml)
        channels = "".join(f"    - stream: 0\n      offset: {off}\n      format: 0\n      dimension: {dim}\n"
                           for off,dim in [(0,3),(12,3),(0,0),(0,0),(24,2)]+[(0,0)]*9)
        yaml = re.sub(r"(?s)    m_VertexCount:.*?  m_CompressedMesh:",f"    m_VertexCount: {base}\n    m_Channels:\n"
                      +channels+f"    m_DataSize: {raw.nbytes}\n    _typelessdata: {raw.tobytes().hex()}\n  m_CompressedMesh:",yaml)
        yaml = re.sub(r"(?s)  m_LocalAABB:.*?  m_CookingOptions:",f"  m_LocalAABB:\n    m_Center: {vector((lo+hi)/2)}\n    m_Extent: {vector((hi-lo)/2)}\n  m_CookingOptions:",yaml)
        (ART/(self.name+".asset")).write_text(yaml, encoding="utf8",newline="\n")
        manifest.append(dict(name=self.name,offset=self.origin.tolist(),surfaces=list(self.parts)))
        print(self.name, base, "vertices", len(indices)//3, "triangles")


def nasa_launcher(path):
    source=Path(path).read_bytes()
    length=struct.unpack_from("<I",source,12)[0]
    gltf=json.loads(source[20:20+length])
    data=source[28+length:]
    def buffer(i):
        view=gltf["bufferViews"][i]
        start=view.get("byteOffset",0)
        return data[start:start+view["byteLength"]]
    model=Model("Mobile Launcher 1")
    for primitive,material in zip(gltf["meshes"][0]["primitives"],["Launcher","Railings"]):
        compressed=primitive["extensions"]["KHR_draco_mesh_compression"]["bufferView"]
        mesh=DracoPy.decode(buffer(compressed))
        v=np.asarray(mesh.points)
        f=np.asarray(mesh.faces)
        # Replace the source's fixed diagonal access arm with the film's hinged crew bridge.
        p=v[f].mean(axis=1)
        f=f[~((p[:,1]>183)&(p[:,1]<209)&(p[:,0]<29.6))]
        uv=mesh.tex_coord.copy()
        uv[:,1]=1-uv[:,1]
        model.add(material,(v-[0,33,0])*.51, mesh.normals,uv,f)
    model.bake()
    (ART/"Mobile Launcher finish.jpg").write_bytes(buffer(gltf["images"][1]["bufferView"]))
    (ART/"Mobile Launcher railings.png").write_bytes(buffer(gltf["images"][3]["bufferView"]))


def landscape(photo):
    # EO-1 ALI ground sampling is about 10 m in this pan-sharpened scene.
    image=Image.open(photo).convert("RGB")
    image.save(ART/"Kennedy coastline.jpg",quality=91)
    ground=Model("Cape Canaveral coastline")
    x0,z0=-6060,2580
    span=13230
    ground.quad("Coast",(x0,GROUND-.12,z0-span),(x0,GROUND-.12,z0),(x0+span,GROUND-.12,z0),(x0+span,GROUND-.12,z0-span),
                [(0,0),(0,1),(1,1),(1,0)])
    # Continue the coast to the hazy horizon outside the bounds of the photograph.
    ground.quad("Atlantic",(-18000,GROUND-.5,-18000),(-18000,GROUND-.5,18000),(18000,GROUND-.5,18000),(18000,GROUND-.5,-18000))
    ground.bake()


def ground_works():
    m=Model("Pad earthworks and approach")
    # A recessed open trench separates the concrete aprons. Sloped turf berms intersect the flat coast at ground level.
    for side in [-1,1]:
        x=side*44
        m.box("Concrete",(x,DECK-.8,0),(70,1.6,137))
        m.box("Firebrick",(side*8.9,(DECK+GROUND)/2,0),(.55,DECK-GROUND,140))
        outer=side*79
        m.quad("Turf",(outer,DECK-1.5,-69),(outer,DECK-1.5,69),(side*118,GROUND,91),(side*118,GROUND,-91))
        for end in [-1,1]:
            m.quad("Turf",(side*9,DECK-.5,end*69),(outer,DECK-.5,end*69),(side*118,GROUND,end*91),(side*9,GROUND,end*91))
        for z in range(-64,65,8):
            m.box("Joint",(x,DECK+.01,z),(69,.015,.052))
        for xx in [19,36,56,73]:
            m.box("Joint",(side*xx,DECK+.012,0),(.055,.014,137))
        for z in range(-64,65,4):
            for near in [True,False]:
                posx = side*(9.6 if near else 77)
                m.tube("Safety rail",(posx,DECK,z),(posx,DECK+1.05,z),.036,sides=5)
        for h in [.52,1.05]:
            m.tube("Safety rail",(side*9.6,DECK+h,-65),(side*9.6,DECK+h,65),.045,sides=6)
            m.tube("Steel",(side*77,DECK+h,-65),(side*77,DECK+h,65),.045,sides=6)
    m.box("Scorched", (0,GROUND-.02,0),(18,.18,150))
    m.quad("Scorched",(-8.6,GROUND+.18,-16),(-8.6,DECK-1.1,1.5),(8.6,DECK-1.1,1.5),(8.6,GROUND+.18,-16))
    # Two tracks of pale river rock lead away from the concrete ramp.
    for x in [-9.1,9.1]:
        m.quad("Ballast",(x-6,GROUND+.27,-1150),(x-6,GROUND+.27,-173),(x+6,GROUND+.27,-173),(x+6,GROUND+.27,-1150))
        m.quad("Ballast",(x-6,GROUND+.27,-173),(x-6,DECK-.05,-66),(x+6,DECK-.05,-66),(x+6,GROUND+.27,-173))
    # Perimeter service road, arc-shaped shoulders and white edge paint.
    prev=None
    for t in np.linspace(0,2*math.pi,113):
        v=np.array([math.cos(t),0,math.sin(t)])
        centre=np.array([210*math.cos(t),GROUND+.06,185*math.sin(t)])
        if prev is not None:
            old,d=prev
            m.quad("Asphalt",old-4.8*d,centre-4.8*v,centre+4.8*v,old+4.8*d)
            for edge in [-4.1,4.1]:
                lift=np.array([0,.022,0])
                m.quad("Road paint",old+lift+edge*d,centre+lift+edge*v,centre+lift+(edge+.13)*v,old+lift+(edge+.13)*d)
        prev=(centre,v)
    m.bake()


def lightning_masts():
    model=Model("Lightning protection masts")
    tops=[]
    for x,z in [(-104,73),(104,86),(74,-109)]:
        base=GROUND+.15
        model.box("Concrete",(x,base,z),(10,.55,10))
        last=None
        for i in range(15):
            h=i*9.35
            width=3.1-1.65*i/14
            pts=[(x+dx*width,base+h,z+dz*width) for dx,dz in [(-1,-1),(-1,1),(1,1),(1,-1)]]
            for k in range(4):
                model.tube("Pale steel",pts[k],pts[(k+1)%4],.115,sides=5)
                if last:
                    model.tube("Pale steel",last[k],pts[k],.18,sides=6)
                    model.tube("Pale steel",last[k],pts[(k+1)%4],.105,sides=5)
                    model.tube("Pale steel",last[(k+1)%4],pts[k],.105,sides=5)
            if i in [4,8,13]:model.box("Steel",(x,base+h-.2,z),(2*width+1.1,.25,2*width+1.1))
            last=pts
        h=base+131
        model.tube("White enamel",(x,h,z),(x,h+45,z),.95,.38,12)
        model.tube("Beacon",(x,h+45,z),(x,h+46,z),.20,.06,8)
        tops.append((x,h+44,z))
    for k,a in enumerate(tops):
        b=tops[(k+1)%len(tops)]
        model.line("Wire",a,b,.055,6.8,14)
    model.bake()


def plant():
    m=Model("Pad water tower and support facilities")
    # Water tower, with a flared vessel, service balcony, diagonal braces and exposed riser.
    x,z=117,21
    y=GROUND
    for i in range(8):
        a=math.tau*i/8
        b=math.tau*(i+1)/8
        foot=(x+11*math.cos(a),y,z+11*math.sin(a))
        mid=(x+9*math.cos(a),y+25,z+9*math.sin(a))
        crown=(x+7*math.cos(a),y+54,z+7*math.sin(a))
        m.box("Concrete",foot,(2,1,2))
        m.tube("Pale steel",foot,crown,.44,sides=8)
        m.tube("Steel",foot,(x+9*math.cos(b),y+25,z+9*math.sin(b)),.14,sides=6)
        m.tube("Steel",mid,(x+7*math.cos(b),y+54,z+7*math.sin(b)),.14,sides=6)
    m.tube("White enamel",(x,y,z),(x,y+51,z),1.35,sides=18)
    m.round_tank("Tank enamel",(x,y,z),[(50,.1),(51,3.6),(54,7.6),(58,10.1),(63,10.3),(66,8.6),(68,5.0),(69,.1)])
    for h,r in [(59,10.23),(63,10.3),(65,9.3)]:m.round_tank("Pale steel",(x,y,z),[(h,r),(h+.22,r)],40)
    for x,z,r in [(-143,114,16),(-187,106,12),(151,-83,10.5)]:
        yh=y+r+3.3
        profile=[(math.sin(a)*r,max(.03,math.cos(a)*r)) for a in np.linspace(-math.pi/2,math.pi/2,21)]
        m.round_tank("Tank enamel",(x,yh,z),profile,36)
        m.round_tank("Pale steel",(x,yh,z),[(-.1,r+.05),(.1,r+.05)],36)
        for i in range(6):
            a=math.tau*i/6
            pos=(x+r*.62*math.cos(a),y,z+r*.62*math.sin(a))
            m.tube("Pale steel",pos,(pos[0],y+r,pos[2]),.6,sides=8)
        m.tube("White enamel",(x,yh+r,z),(x,yh+r+2,z),.55,sides=10)
    # Low support buildings, louvers, vents, loading bays, yard pipe racks and floodlights break the pad's silhouette.
    for x,z,w,l,h in [(100,-63,15,37,5),(-125,-30,24,32,6),(110,92,21,35,5),(165,66,17,35,4.8),(-94,145,13,30,4.3)]:
        m.box("Light siding",(x,y+h/2,z),(w,h,l))
        m.box("Roof",(x,y+h+.16,z),(w+1.2,.35,l+1))
        for dz in [-l*.29,0,l*.29]:
            m.box("Door",(x-w/2-.04,y+1.8,z+dz),(.13,3.6,3.8))
            m.box("Steel",(x-w/2-.12,y+3.8,z+dz),(.22,.26,4.3))
            m.box("Roof",(x+1,y+h+.75,z+dz),(2.3,1.2,3.2))
        for dx in range(-int(w*.45),int(w*.46),2):
            m.box("Siding seam",(x+dx,y+h*.5,z-l*.5-.04),(.055,h,.07))
    for x,z,dx,dz in [(-116,74,-46,0),(92,-62,0,72),(-72,-72,43,0)]:
        for s in [0,1.3,2.5]:
            offset=np.array([s if dz else 0,0,s if dx else 0])
            start=np.array([x,y+2.35,z])+offset
            end=start+[dx,0,dz]
            m.tube("Pale steel",start,end,.32,sides=9)
        for t in np.linspace(0,1,10):
            m.box("Concrete",(x+dx*t+1,y+1,z+dz*t+1),(3 if dz else .72,2.2,.72 if dz else 3))
    for x,z in [(-60,-52),(51,-55),(-54,54),(57,57),(104,-86),(-119,62)]:
        y0=DECK if abs(x)<80 else GROUND
        m.tube("Steel",(x,y0,z),(x,y0+12,z),.16,.1)
        m.box("Steel",(x,y0+11.9,z),(2.5,.14,.15))
        for dx in [-.88,0,.88]:m.box("Lamp",(x+dx,y0+12.15,z),(.70,.50,.23))
    m.bake()


def access_arm():
    m=Model("Crew Access Arm Pivot",(16.75,HATCH,0))
    m.box("Steel",(9.85,HATCH-.38,0),(15.0,.72,2.28))
    m.box("Tread",(9.85,HATCH-.035,0),(15,.07,2.1))
    for side in [-1,1]:
        for x in np.arange(2.55,17.5,1.0):
            m.tube("Safety rail",(x,HATCH,side*1.05),(x,HATCH+1.08,side*1.05),.032,sides=8)
        for y in [.50,1.07]:
            m.tube("Safety rail",(2.36,HATCH+y,side*1.05),(17.26,HATCH+y,side*1.05),.04,sides=10)
        m.box("Safety rail",(9.85,HATCH+.025,side*.95),(14.8,.012,.095))
        for x in np.arange(2.6,16.6,1.55):
            m.tube("Pale steel",(x,HATCH-.7,side*1.05),(x+1.48,HATCH-1.65,side*1.05),.075,sides=6)
            m.tube("Pale steel",(x+1.48,HATCH-1.65,side*1.05),(x+1.55,HATCH-.7,side*1.05),.08,sides=6)
        m.tube("Steel",(2.55,HATCH-1.62,side*1.05),(17.2,HATCH-1.62,side*1.05),.11,sides=8)
    # Roof and far-side uprights of the access enclosure leave the boarding camera a view of the crew.
    m.box("Light siding",(4.05,HATCH+2.66,0),(3.45,.18,2.45))
    for x in [2.55,4.0,5.5]:
        m.box("White enamel",(x,HATCH+1.33,1.09),(.10,2.65,.10))
    m.box("Light siding",(4,HATCH+1.25,1.15),(3.3,2.45,.09))
    m.box("Door",(4,HATCH+1.55,1.075),(1.7,.63,.032))
    m.box("Lamp",(3.98,HATCH+2.52,.35),(1.38,.05,.2))
    for x in np.arange(2.6,17,.58):
        m.box("Steel",(x,HATCH+.003,0),(.017,.009,1.86))
    m.bake()


def tower_detail():
    m=Model("Mobile launcher service decks")
    # External galleries and utility runs on the original NASA structural mesh.
    for h in [6,16,27,39,51,63,75,82,93]:
        m.box("Steel",(20.7,h-.22,-6.55),(12.6,.4,2.1))
        for side in [-1,1]:
            for x in np.linspace(14.6,26.8,9):
                m.tube("Safety rail",(x,h,-6.55+side*.95),(x,h+1.04,-6.55+side*.95),.035,sides=6)
            for dy in [.52,1.04]:
                m.tube("Safety rail",(14.6,h+dy,-6.55+side*.95),(26.8,h+dy,-6.55+side*.95),.045,sides=6)
    for i in range(5):
        x=25.5+i*.43
        m.tube("Pale steel",(x,-1,-6.8),(x,91.5-i*2,-6.8),.13+.03*(i%2),sides=10)
    for h in [24,50,67]:
        # Parked arm shapes stay on the tower side of the ascending vehicle.
        m.box("Steel",(12.5,h-.35,2.15),(8.4,.72,1.4))
        m.tube("Pale steel",(9,h-.7,2.15),(15.8,h-3.4,2.15),.18)
        m.tube("Pale steel",(8.5,h+.15,2.3),(14.7,h+.15,2.3),.24,sides=10)
    m.bake()


def textures():
    random=np.random.default_rng(39)
    # Tileable finish maps with grain at several scales, combined with tint and roughness in URP.
    for name,amplitude in [("Cement grain",12),("Scrub grass",23),("Road aggregate",19)]:
        size=512
        coarse=Image.fromarray(random.integers(0,256,(34,34),dtype=np.uint8))
        arr=np.asarray(coarse.resize((size,size),Image.Resampling.BICUBIC)).astype(float)
        luminance=np.clip(215+(arr-128)*amplitude/128+random.normal(0,amplitude*.57,(size,size)),0,255).astype('uint8')
        Image.fromarray(luminance).save(ART/(name+".jpg"),quality=87)
    tread=Image.new("RGB",(128,128),(103,110,109))
    draw=ImageDraw.Draw(tread)
    for x in range(-16,144,16):
        for y in range(-16,144,16):
            direction=1 if (x//16+y//16)%2==0 else -1
            draw.line((x-5,y-5*direction,x+5,y+5*direction),fill=(66,73,75),width=5)
            draw.line((x-5,y-5*direction-1,x+5,y+5*direction-1),fill=(154,162,162),width=2)
    tread.save(ART/"Bridge tread.png")


if __name__ == "__main__":
    ART.mkdir(parents=True,exist_ok=True)
    nasa_launcher(sys.argv[1])
    landscape(sys.argv[2])
    ground_works()
    lightning_masts()
    plant()
    access_arm()
    tower_detail()
    textures()
    (ART/"Pad meshes.json").write_text(json.dumps({"parts":manifest},indent=2)+"\n",encoding="utf8")
