"""Build an editable, metre-scale lunar station and an optimized Unity FBX.

Run: C:/Tools/blender.exe -b -t 8 --python Tools/build_lunar_base_blender.py
Design coordinates throughout are the existing Unity scene's world coordinates.
Blender conversion is (x, -z, y); the manifest contains both coordinate systems.
Only modeled geometry and explicit anchors are exported, never the preview studio.
"""
import bpy
import json
import math
import random
from pathlib import Path
from mathutils import Vector
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/_LunarEscape/Art/LunarBaseGeometry'
SOURCE = ROOT / 'Models/LunarBase'
TEX = OUT / 'Textures'
for p in (OUT, SOURCE, TEX): p.mkdir(parents=True, exist_ok=True)
random.seed(1041)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
    for item in list(datablocks):
        if item.users == 0: datablocks.remove(item)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
groups = {}
mats = {}
current = None

def zone(name):
    global current
    current = bpy.data.collections.new(name)
    scene.collection.children.link(current)
    groups[name] = []

def own(obj, name, mat):
    obj.name = name
    for c in list(obj.users_collection): c.objects.unlink(obj)
    current.objects.link(obj)
    groups[current.name].append(obj)
    if mat: obj.data.materials.append(mats[mat])
    return obj

def u(p): return Vector((p[0], -p[2], p[1]))

def box(name, p, s, mat, bevel=.015):
    bpy.ops.mesh.primitive_cube_add(size=1, location=u(p))
    obj=own(bpy.context.object, name, mat)
    obj.dimensions=(s[0],s[2],s[1])
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        m=obj.modifiers.new('Manufactured edge radii','BEVEL');m.width=min(bevel,min(s)*.28);m.segments=2
        m=obj.modifiers.new('Weighted face normals','WEIGHTED_NORMAL');m.keep_sharp=True
    return obj

def rod(name, a, b, r, mat, n=12):
    a,b=u(a),u(b);d=b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=n, radius=r, depth=d.length, location=(a+b)/2)
    obj=own(bpy.context.object,name,mat)
    obj.rotation_euler=d.to_track_quat('Z','Y').to_euler()
    for poly in obj.data.polygons: poly.use_smooth=len(poly.vertices)==4
    m=obj.modifiers.new('Machined edge','BEVEL');m.width=min(.006,r*.12);m.segments=1
    return obj

def torus(name,p,r,t,mat,normal=(1,0,0),segments=48):
    bpy.ops.mesh.primitive_torus_add(major_segments=segments,minor_segments=6,major_radius=r,minor_radius=t,location=u(p))
    obj=own(bpy.context.object,name,mat)
    obj.rotation_euler=u(normal).to_track_quat('Z','Y').to_euler()
    for poly in obj.data.polygons: poly.use_smooth=True
    return obj

def mesh(name,verts,faces,mat):
    m=bpy.data.meshes.new(name);m.from_pydata([u(v) for v in verts],[],faces);m.update()
    obj=bpy.data.objects.new(name,m);current.objects.link(obj);groups[current.name].append(obj);m.materials.append(mats[mat])
    return obj

def label(name,body,p,size,mat,face='south'):
    curve=bpy.data.curves.new(name,'FONT');curve.body=body;curve.align_x='CENTER';curve.size=size;curve.extrude=.0005;curve.resolution_u=4
    obj=bpy.data.objects.new(name,curve);current.objects.link(obj);groups[current.name].append(obj);curve.materials.append(mats[mat]);obj.location=u(p)
    # Font local +Z points toward the observing side. Text baseline stays horizontal.
    if face=='south': obj.rotation_euler=(math.pi/2,0,math.pi)
    elif face=='north': obj.rotation_euler=(math.pi/2,0,0)
    elif face=='east': obj.rotation_euler=(math.pi/2,0,math.pi/2)
    elif face=='west': obj.rotation_euler=(math.pi/2,0,-math.pi/2)
    return obj

def make_mat(name,color,metallic,roughness,emission=0,texture=None):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    # Explicit trees avoid localized startup-node names selecting a second,
    # inactive output when Blender is configured to a non-English UI language.
    m.node_tree.nodes.clear()
    bs=m.node_tree.nodes.new('ShaderNodeBsdfPrincipled');bs.name='Principled BSDF'
    output=m.node_tree.nodes.new('ShaderNodeOutputMaterial');output.is_active_output=True;m.node_tree.links.new(bs.outputs['BSDF'],output.inputs['Surface'])
    bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Metallic'].default_value=metallic;bs.inputs['Roughness'].default_value=roughness
    if emission:
        bs.inputs['Emission Color'].default_value=(*color,1);bs.inputs['Emission Strength'].default_value=emission
    if texture:
        tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(TEX/(texture+'BaseColor.png')));m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
        tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(TEX/(texture+'Normal.png')));tex.image.colorspace_settings.name='Non-Color'
        normal=m.node_tree.nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.07 if name=='LB_PaintedAlloy' else .42;m.node_tree.links.new(tex.outputs['Color'],normal.inputs['Color']);m.node_tree.links.new(normal.outputs['Normal'],bs.inputs['Normal'])
    mats[name]=m
    result={'name':name,'color':[*color,1],'metallic':metallic,'roughness':roughness,'emissionStrength':emission,'texturePrefix':texture}
    if texture:
        result['baseColorTexture']='Textures/'+texture+'BaseColor.png'
        result['normalTexture']='Textures/'+texture+'Normal.png'
    if emission:result['emission']=[c*emission for c in color]
    return result

def textures():
    n=1024;y,x=np.mgrid[0:n,0:n].astype(np.float32)/n
    rng=np.random.default_rng(42)
    wave=np.sin(x*51+y*6)*.22+np.sin(y*87+np.sin(x*19)*2)*.15+np.sin(x*317+y*29)*.025
    weave=(np.sin(x*n*math.pi)*np.sin(y*n*math.pi))*.015+rng.normal(0,.025,(n,n))
    # A subdued fabric quilt field. Its small relief survives VR close inspection.
    h=(wave+weave).astype(np.float32)
    for prefix,base,variation in [('ThermalFabric',(0.78,.77,.71),.09),('PaintedPanel',(.71,.75,.76),.018)]:
        arr=np.ones((n,n,4),np.float32)
        for i in range(3): arr[:,:,i]=np.clip(base[i]+h*variation,0,1)
        img=bpy.data.images.new(prefix+'BaseColor',width=n,height=n,alpha=True);img.pixels.foreach_set(arr.ravel());img.filepath_raw=str(TEX/(prefix+'BaseColor.png'));img.file_format='PNG';img.save()
        gy,gx=np.gradient(h);normal=np.stack((-gx*8,-gy*8,np.ones_like(h)),axis=-1);normal/=np.linalg.norm(normal,axis=-1,keepdims=True)
        arr[:,:,:3]=normal*.5+.5
        img=bpy.data.images.new(prefix+'Normal',width=n,height=n,alpha=True);img.colorspace_settings.name='Non-Color';img.pixels.foreach_set(arr.ravel());img.filepath_raw=str(TEX/(prefix+'Normal.png'));img.file_format='PNG';img.save()

textures()
manifest={'source':'Models/LunarBase/LunarStation_Realistic.blend','units':'metres','unityPlacement':{'position':[0,0,0],'scale':[1,1,1]},'coordinateConvention':'Script uses Unity XYZ; Blender geometry is X,-Z,Y. Verify exported anchors after import.','materials':[]}
for args in [
    ('LB_ThermalFabric',(.78,.77,.71),.06,.76,0,'ThermalFabric'),
    ('LB_PaintedAlloy',(.71,.75,.76),.3,.72,0,'PaintedPanel'),
    ('LB_BrushedTitanium',(.29,.34,.37),.8,.32),
    ('LB_AnodizedGraphite',(.047,.069,.085),.7,.4),
    ('LB_SealRubber',(.018,.023,.024),0,.84),
    ('LB_SolarCells',(.012,.037,.09),.55,.24),
    ('LB_SafetyOchre',(.82,.31,.038),.35,.42),
    ('LB_InstrumentGlass',(.028,.09,.13),.62,.16),
    ('LB_LightDiffuser',(.75,.91,1),.05,.32,2.8),
    ('LB_EmergencyGreen',(.10,.55,.35),.1,.4,.7),
    ('LB_Stencil',(.68,.72,.70),.05,.6),
]: manifest['materials'].append(make_mat(*args))

# PRIMARY HABITAT EXTERIOR. Existing pressure floor and collision walls remain.
zone('01_PrimaryPressureShell')
box('Primary insulated ceiling',(0,3.51,0),(8.45,.22,8.45),'LB_ThermalFabric',.07)
box('Primary underfloor structural cassette',(0,-.18,0),(8.4,.25,8.4),'LB_AnodizedGraphite',.08)
# Continuous pressure liner closes all decorative panel seams. The east liner
# is divided around the gameplay door; its opening remains 1.8 by 2.5 metres.
for z in [-4.035,4.035]:box('Continuous bulkhead pressure liner',(0,1.65,z),(8.12,3.3,.07),'LB_AnodizedGraphite',.007)
box('Continuous west pressure liner',(-4.035,1.65,0),(.07,3.3,8.12),'LB_AnodizedGraphite',.007)
box('East pressure liner south',(4.035,1.65,-1.6),(.07,3.3,4.8),'LB_AnodizedGraphite',.007)
box('East pressure liner north',(4.035,1.65,3.3),(.07,3.3,1.4),'LB_AnodizedGraphite',.007)
box('East pressure liner lintel',(4.035,2.9,1.7),(.07,.8,1.8),'LB_AnodizedGraphite',.007)
# Cladding is divided at the actual east airlock. No wall spans the doorway.
for side in [-1,1]:
    spans=[(-3.5+i*1.4,1.35) for i in range(6)] if side<0 else [(-3.4+i*1.2,1.155) for i in range(4)]+[(3.35,1.42)]
    for z,width in spans:
        box('External removable thermal panel',(side*4.12,1.7,z),(.18,3.3,width),'LB_ThermalFabric',.07)
        for yy in [.32,3.06]:
            for zz in [z-width*.38,z+width*.38]:
                box('Thermal cover retention latch',(side*4.225,yy,zz),(.036,.12,.06),'LB_BrushedTitanium',.005)
    for i in range(6):
        x=-3.5+i*1.4
        box('External bulkhead insulation',(x,1.7,side*4.12),(1.35,3.3,.18),'LB_ThermalFabric',.06)
        for xx in [x-.5,x+.5]:
            box('Thermal blanket edge strap',(xx,1.7,side*4.224),(.033,3,.021),'LB_Stencil',.003)
    for yy in [.16,3.3]:
        box('Pressure shell perimeter beam',(side*4.23,yy,0),(.12,.16,8.4),'LB_BrushedTitanium',.018)
        box('Pressure shell perimeter beam',(0,yy,side*4.23),(8.4,.16,.12),'LB_BrushedTitanium',.018)
    for zz in [-4.05,-2.65,-1.25,.15,2.95,4.05]:
        if side==1 and .8<zz<2.6: continue
        box('Primary exterior structural spine',(side*4.225,1.7,zz),(.10,3.24,.10),'LB_BrushedTitanium',.01)
for x in [-3.8,3.8]:
    for z in [-3.8,3.8]:
        box('Jacking pad',(x,-.28,z),(.68,.12,.68),'LB_BrushedTitanium',.045)
        rod('Levelling jack',(x,-.24,z),(x,.35,z),.105,'LB_AnodizedGraphite',16)
for x in [-2.85,-1.4,0,1.4,2.85]:
    box('Roof blanket seam',(x,3.645,0),(.024,.025,8.2),'LB_Stencil',.002)
for z in [-2.8,-1.4,0,1.4,2.8]:
    box('Roof stiffener',(0,3.68,z),(8.3,.08,.12),'LB_BrushedTitanium',.015)
box('External serial plate',(1.7,2.35,-4.24),(2.1,.45,.025),'LB_AnodizedGraphite',.012)
label('Primary habitat identity','LUNAR  /  SYSTEMS  01',(1.7,2.30,-4.258),.16,'LB_Stencil')

# SIDE HABITATS: segmented cylindrical pressure vessels with domed closures.
def cylinder_band(name,x0,x1,cy,cz,r0,r1,mat,lo=0,hi=math.tau,segments=48):
    verts=[]
    for x,r in [(x0,r0),(x1,r1)]:
        for k in range(segments+1):
            a=lo+(hi-lo)*k/segments;verts.append((x,cy+math.sin(a)*r,cz+math.cos(a)*r))
    # Radius increases outward from the cylinder axis. Axial direction reverses
    # on the left dome, so its winding must reverse relative to the right dome.
    faces=[(k,k+segments+1,k+segments+2,k+1) if x1>x0 else
           (k,k+1,k+segments+2,k+segments+1) for k in range(segments)]
    obj=mesh(name,verts,faces,mat)
    uv=obj.data.uv_layers.new(name='Fabric unwrap')
    for p in obj.data.polygons:
        for li in p.loop_indices:
            vi=obj.data.loops[li].vertex_index;uv.data[li].uv=(vi%(segments+1)/segments,vi//(segments+1))
    for p in obj.data.polygons:p.use_smooth=True
    return obj

for sign in [-1,1]:
    zone('02_HabitatSouth' if sign<0 else '03_HabitatNorth')
    cy,cz=1.95,sign*8.5
    cylinder_band('Pressure liner',-7.95,.95,cy,cz,1.85,1.85,'LB_AnodizedGraphite')
    for bay in range(7):
        a=-7.86+bay*1.25;b=a+1.21
        for j in range(12):
            angle=j*math.tau/12
            cylinder_band('MLI blanket cassette',a,b,cy,cz,1.886,1.886,'LB_ThermalFabric',angle+.012,angle+math.tau/12-.012,5)
    for x in [-7.94,-6.625,-5.375,-4.125,-2.875,-1.625,-.375,.96]:
        torus('Habitat circumferential retention band',(x,cy,cz),1.92,.042,'LB_BrushedTitanium')
        for j in range(12):
            a=j*math.tau/12;yy=cy+math.sin(a)*1.948;zz=cz+math.cos(a)*1.948
            rod('Band lock pin',(x-.07,yy,zz),(x+.07,yy,zz),.035,'LB_AnodizedGraphite',8)
    for end,sg in [(-7.95,-1),(.96,1)]:
        # Flattened ellipsoidal bulkhead with independent insulation gores.
        for k in range(6):
            t0=k*math.pi/12;t1=(k+1)*math.pi/12
            cylinder_band('Domed pressure bulkhead',end+sg*.65*math.sin(t0),end+sg*.65*math.sin(t1),cy,cz,max(.01,1.85*math.cos(t0)),max(.01,1.85*math.cos(t1)),'LB_ThermalFabric')
        xx=end+sg*.652
        rod('End service hatch',(xx-.02,cy,cz),(xx+.02,cy,cz),.63,'LB_BrushedTitanium',48)
        rod('Hatch inner plate',(xx+sg*.027,cy,cz),(xx+sg*.04,cy,cz),.54,'LB_PaintedAlloy',48)
        torus('Hatch resilient seal',(xx+sg*.045,cy,cz),.55,.02,'LB_SealRubber')
        for j in range(12):
            a=j*math.tau/12
            rod('Captive hatch bolt',(xx,cy+math.sin(a)*.59,cz+math.cos(a)*.59),(xx+sg*.07,cy+math.sin(a)*.59,cz+math.cos(a)*.59),.029,'LB_AnodizedGraphite',6)
        # Triple pane observation port with a thick recessed pressure ring.
        wx=xx+sg*.065;wy=cy+.13
        rod('Observation port bezel',(wx,wy,cz),(wx+sg*.045,wy,cz),.30,'LB_AnodizedGraphite',48)
        rod('Observation pressure glass',(wx+sg*.047,wy,cz),(wx+sg*.056,wy,cz),.253,'LB_InstrumentGlass',48)
        torus('Inner glass ring',(wx+sg*.059,wy,cz),.263,.011,'LB_BrushedTitanium')
    for x in [-6.9,-.1]:
        for dz in [-1.15,1.15]:
            rod('Habitat A frame',(x,1.0,cz+dz*.75),(x,-.2,cz+dz*1.35),.065,'LB_BrushedTitanium')
            box('Habitat foundation shoe',(x,-.22,cz+dz*1.35),(.6,.12,.7),'LB_AnodizedGraphite',.05)
    # Pressurized connector ends at the existing main hull, leaving its wall intact.
    for yy in [.45,2.9]:
        box('Connector perimeter rail',(-2,yy,sign*5.35),(2.7,.12,2.55),'LB_BrushedTitanium',.015)
    box('Pressurized connector',(-2,1.7,sign*5.35),(2.56,2.52,2.52),'LB_ThermalFabric',.16)
    for zz in [sign*4.3,sign*4.95,sign*5.6,sign*6.25]:
        box('Connector collar top',(-2,3.02,zz),(2.76,.09,.09),'LB_AnodizedGraphite',.012)
        for xx in [-3.34,-.66]:box('Connector collar side',(xx,1.7,zz),(.09,2.64,.09),'LB_AnodizedGraphite',.01)
    # Externally routed power / thermal loops above ground.
    for offset in [0,.16]:
        rod('Thermal service loop',(-5,.42,cz-sign*1.86),(-5,.42,sign*4.28),.045,'LB_BrushedTitanium')
        rod('Habitat external umbilical',(-7.8,.5+offset,cz-sign*1.75),(.7,.5+offset,cz-sign*1.75),.033,'LB_SealRubber')

# AIRLOCK. Exact 1.8 m x 2.5 m door opening and open eastern exit are preserved.
zone('04_AirlockPassage')
for z in [.70,2.70]:
    box('Airlock layered pressure wall',(6,1.65,z),(4.18,3.3,.18),'LB_PaintedAlloy',.025)
    box('Airlock exterior thermal cover',(6,1.70,z+(-.13 if z<1 else .13)),(4.2,3.4,.12),'LB_ThermalFabric',.04)
    for x in [4.3,5.45,6.6,7.85]:
        box('Airlock lower access panel',(x,.77,z+(.106 if z<1 else -.106)),(1.05,1.32,.035),'LB_AnodizedGraphite',.03)
        box('Airlock interior upper cassette',(x,2.2,z+(.106 if z<1 else -.106)),(1.04,1.30,.045),'LB_PaintedAlloy',.04)
box('Airlock ceiling',(6,3.39,1.7),(4.3,.18,2.24),'LB_PaintedAlloy',.025)
box('Airlock walking deck',(6,-.043,1.7),(4.3,.08,2.2),'LB_AnodizedGraphite',.012)
for x in [4.02,5.43,6.78,8.10]:
    for z in [.85,2.55]:box('Airlock rib jamb',(x,1.27,z),(.13,2.54,.10),'LB_BrushedTitanium',.015)
    box('Airlock rib lintel',(x,2.56,1.7),(.13,.13,1.8),'LB_BrushedTitanium',.015)
for x in [4.48+i*.26 for i in range(14)]:
    box('Airlock anti-slip tread',(x,.004,1.7),(.19,.013,1.50),'LB_BrushedTitanium',.002)
for z in [.98,2.42]:
    rod('Airlock EVA assist handrail',(4.38,1.04,z),(7.95,1.04,z),.024,'LB_SafetyOchre',12)
    for x in [4.38,5.55,6.72,7.95]:rod('Handrail standoff',(x,1.04,z),(x,1.04,.80 if z<1 else 2.60),.018,'LB_BrushedTitanium')
    box('Airlock lighting diffuser',(6,3.235,z),(3.82,.04,.07),'LB_LightDiffuser',.008)
box('Door jamb upper pressure shell',(4.11,3.0,1.7),(.21,.93,1.82),'LB_ThermalFabric',.025)
for z in [.79,2.61]:
    box('Primary doorway armored jamb',(4.07,1.27,z),(.33,2.54,.10),'LB_BrushedTitanium',.015)
    box('Primary doorway gasket',(3.895,1.27,z),(.021,2.54,.033),'LB_SealRubber',.008)
box('Primary doorway lintel',(4.07,2.57,1.7),(.33,.14,1.9),'LB_BrushedTitanium',.02)
for z in [.65,2.75]:
    box('External airlock exit bumper',(8.17,1.7,z),(.16,3.38,.15),'LB_AnodizedGraphite',.03)
box('External airlock canopy',(8.24,3.52,1.7),(.62,.13,2.4),'LB_PaintedAlloy',.035)
box('External airlock ID backing',(8.265,2.93,1.7),(.028,.48,1.7),'LB_AnodizedGraphite',.009)
label('External airlock ID','EVA  /  01',(8.285,2.85,1.7),.23,'LB_Stencil','east')

# SCIENCE / SYSTEMS BAY INTERIOR. Hardware stays within the original wall volume.
zone('05_InteriorPanels')
box('Interior floating deck',(0,-.045,0),(7.84,.08,7.84),'LB_AnodizedGraphite',.01)
for x in [-3.1,-1.86,-.62,.62,1.86,3.1]:
    for z in [-3.1,-1.86,-.62,.62,1.86,3.1]:
        box('Removable deck panel',(x,-.004,z),(1.21,.055,1.21),'LB_BrushedTitanium',.012)
        for d in [-.51,.51]:
            box('Deck captive fastener',(x+d,.026,z+.51),(.025,.002,.025),'LB_AnodizedGraphite',.002)
        for d in [-.36,0,.36]:box('Deck anti slip insert',(x+d,.026,z),(.20,.003,.82),'LB_AnodizedGraphite',.002)
for side in [-1,1]:
    # Back and systems walls retain their full visual backing behind all existing UI.
    for i in range(6):
        x=-3.2+i*1.28
        box('Bulkhead lining panel',(x,1.67,side*3.865),(1.25,3.16,.058),'LB_PaintedAlloy',.025)
        for xx in [x-.50,x+.50]:
            for yy in [.2,3.09]:rod('Flush quarter turn fastener',(xx,yy,side*3.825),(xx,yy,side*3.82),.014,'LB_AnodizedGraphite',6)
    for yy in [.14,3.17]:box('Bulkhead equipment rail',(0,yy,side*3.812),(7.75,.065,.045),'LB_BrushedTitanium',.007)
    spans=[(-3.2+i*1.28,1.25) for i in range(6)] if side<0 else [(-3.305+i*1.16,1.13) for i in range(4)]+[(3.285,1.15)]
    for zz,width in spans:
        box('Sidewall service cassette',(side*3.865,1.68,zz),(.058,3.14,width),'LB_PaintedAlloy',.024)
        for yy in [.24,3.08]:
            for z in [zz-width*.40,zz+width*.40]:rod('Sidewall captive bolt',(side*3.825,yy,z),(side*3.819,yy,z),.014,'LB_AnodizedGraphite',6)
    for z in [-3.45,-1.70,.05,3.45]:
        if side==1 and .8<z<2.6:continue
        box('Interior modular structural rib',(side*3.76,1.57,z),(.095,3.10,.105),'LB_BrushedTitanium',.012)
    for yy in [.24,2.95]:
        # Right utility raceway is interrupted by the actual egress opening.
        if side<0:box('Interior utility raceway',(side*3.76,yy,0),(.16,.15,7.6),'LB_AnodizedGraphite',.024)
        else:
            box('Interior utility raceway',(side*3.76,yy,-1.56),(.16,.15,4.55),'LB_AnodizedGraphite',.024)
            box('Interior utility raceway',(side*3.76,yy,3.21),(.16,.15,1.12),'LB_AnodizedGraphite',.024)
box('Acoustic ceiling cassette',(0,3.335,0),(7.86,.07,7.86),'LB_PaintedAlloy',.012)
for z in [-3.45,-1.7,.05,1.8,3.45]:
    box('Exposed ceiling beam',(0,3.22,z),(7.62,.16,.13),'LB_BrushedTitanium',.018)
    for x in [-3.5,3.5]:
        rod('Ceiling corner gusset',(x,3.22,z),(math.copysign(3.76,x),2.94,z),.075,'LB_AnodizedGraphite',6)
for x in [-2.4,2.4]:
    box('Recessed light housing',(x,3.215,0),(.26,.095,7.20),'LB_AnodizedGraphite',.03)
    box('Frosted LED diffuser',(x,3.155,0),(.155,.025,7.06),'LB_LightDiffuser',.01)
for x in [-.80,.80]:
    for z in [-2.7,0,2.7]:
        box('Ceiling air return box',(x,3.245,z),(.48,.11,.72),'LB_AnodizedGraphite',.028)
        for k in range(8):box('Air return louvre',(x,3.175,z-.30+k*.085),(.42,.019,.025),'LB_BrushedTitanium',.003)
for x in [-2.62,2.62]:
    box('Deck guidance recess',(x,.027,0),(.064,.009,7.54),'LB_SealRubber',.002)
    box('Low level route light',(x,.033,0),(.024,.004,7.48),'LB_EmergencyGreen',.001)

# West-wall service racks fit within the original storage panel footprint.
zone('06_InteriorFixedEquipment')
for idx,z in enumerate([-1.5,0,1.5]):
    box('Life support equipment rack',(-3.71,1.13,z),(.26,1.95,1.14),'LB_AnodizedGraphite',.025)
    for yy in [.22,2.04]:box('Equipment rack end cap',(-3.56,yy,z),(.05,.12,1.04),'LB_BrushedTitanium',.01)
    for zz in [z-.49,z+.49]:box('Equipment rack rail',(-3.55,1.13,zz),(.07,1.8,.04),'LB_BrushedTitanium',.006)
    for j in range(4):
        yy=.51+j*.39
        box('Replaceable equipment drawer',(-3.55,yy,z),(.044,.355,.92),'LB_PaintedAlloy',.014)
        rod('Rack drawer pull',(-3.49,yy-.08,z-.27),(-3.49,yy-.08,z+.27),.014,'LB_AnodizedGraphite')
        for zz in [-.29,-.20,-.11,-.02,.07,.16,.25]:
            box('Rack cooling aperture',(-3.522,yy+.072,z+zz),(.011,.075,.033),'LB_AnodizedGraphite',.006)
        box('Rack power indicator',(-3.514,yy-.052,z+.38),(.008,.025,.025),'LB_EmergencyGreen',.003)
    label('Life support rack stencil',['ECLSS / AIR','POWER / DC','THERMAL / H2O'][idx],(-3.512,2.12,z),.095,'LB_Stencil','east')
# Low profile instrument recesses are confined to unused south wall.
for x in [-2.55,2.55]:
    box('Interior instrument bezel',(x,1.74,-3.794),(1.6,.98,.11),'LB_AnodizedGraphite',.055)
    box('Interior instrument glass',(x,1.79,-3.728),(1.39,.64,.018),'LB_InstrumentGlass',.015)
    for i in range(7):box('Instrument vector plot',(x-.58+i*.18,1.68+random.random()*.20,-3.713),(.07,.07+random.random()*.15,.006),'LB_EmergencyGreen',.003)
    for i in range(5):rod('Instrument rotary switch',(x-.45+i*.225,1.36,-3.73),(x-.45+i*.225,1.36,-3.69),.027,'LB_BrushedTitanium',12)
    label('Instrument identification','HABITAT SERVICES',(x,2.35,-3.80),.12,'LB_AnodizedGraphite','north')
for z in [-2.85,2.85]:
    rod('Interior assist rail',(-3.58,1.07,z-.34),(-3.58,1.07,z+.34),.021,'LB_SafetyOchre')
    for dz in [-.34,.34]:rod('Interior rail mount',(-3.79,1.07,z+dz),(-3.58,1.07,z+dz),.014,'LB_BrushedTitanium')

# Furniture honors original contact heights and item placement anchors exactly.
zone('07_WorkbenchAndCargoShelf')
box('Repair bench stainless top',(0,.942,.55),(2.20,.116,.85),'LB_PaintedAlloy',.025)
box('Repair bench protective front',(0,.928,.112),(2.16,.064,.035),'LB_AnodizedGraphite',.010)
for x in [-.92,.92]:
    for z in [.25,.86]:box('Bench bolted extrusion',(x,.45,z),(.073,.87,.073),'LB_BrushedTitanium',.009)
    box('Bench side panel',(x,.46,.56),(.032,.65,.56),'LB_AnodizedGraphite',.02)
    box('Bench base rail',(x,.08,.55),(.13,.07,.74),'LB_BrushedTitanium',.012)
box('Bench under-shelf',(0,.23,.57),(1.84,.04,.60),'LB_BrushedTitanium',.015)
for x in [-.61,0,.61]:
    box('Bench shallow drawer',(x,.71,.43),(.565,.27,.46),'LB_AnodizedGraphite',.018)
    box('Bench drawer front',(x,.71,.182),(.56,.265,.025),'LB_PaintedAlloy',.012)
    rod('Bench drawer pull',(x-.17,.72,.152),(x+.17,.72,.152),.012,'LB_BrushedTitanium')
box('Tool antistatic work mat',(-.30,1.003,.49),(1.07,.005,.57),'LB_SealRubber',.025)
for x in [-1.03,1.03]:box('Bench corner safety insert',(x,.945,.09),(.115,.05,.03),'LB_SafetyOchre',.005)
box('Cargo shelf main surface',(0,.85,3.1),(6.50,.14,.80),'LB_BrushedTitanium',.015)
box('Cargo shelf front extrusion',(0,.807,2.684),(6.52,.12,.047),'LB_AnodizedGraphite',.01)
box('Cargo shelf lower storage',(0,.19,3.19),(6.36,.06,.57),'LB_AnodizedGraphite',.01)
for x in [-3.14,-1.07,1.07,3.14]:
    for z in [2.79,3.42]:box('Cargo rack structural leg',(x,.43,z),(.064,.82,.064),'LB_BrushedTitanium',.006)
for i in range(7):
    x=-2.7+i*.9
    box('Cargo non slip locator',(x,.925,3.1),(.55,.009,.52),'LB_SealRubber',.025)
    box('Cargo shelf label holder',(x,.68,2.656),(.78,.36,.028),'LB_AnodizedGraphite',.018)
for x in [-2.25,-1.35,-.45,.45,1.35,2.25]:box('Cargo position divider',(x,.921,3.1),(.018,.012,.66),'LB_SafetyOchre',.002)

# Exterior power and communications uses original station positions.
zone('08_PowerAndComms')
for z in [-8.5,8.5]:
    box('Solar support frame',(-10,1.27,z),(5.06,.15,5.56),'LB_BrushedTitanium',.025)
    for x in [-12.2,-7.8]:
        for zz in [z-2.25,z+2.25]:
            rod('Solar array adjustable leg',(x,-.2,zz),(x,1.22,zz),.043,'LB_BrushedTitanium')
            box('Solar array shoe',(x,-.23,zz),(.44,.10,.44),'LB_AnodizedGraphite',.025)
    for a in range(8):
        for b in range(9):
            xx=-12.16+a*.62;zz=z-2.45+b*.61
            box('Photovoltaic cell laminate',(xx,1.355,zz),(.592,.028,.580),'LB_SolarCells',.008)
            box('Photovoltaic conductive bus',(xx,1.372,zz),(.007,.004,.55),'LB_Stencil',.001)
    for xx in [-12.52,-7.48]:box('Solar perimeter frame',(xx,1.37,z),(.045,.1,5.58),'LB_AnodizedGraphite',.008)
    for zz in [z-2.78,z+2.78]:box('Solar perimeter frame',(-10,1.37,zz),(5.08,.1,.045),'LB_AnodizedGraphite',.008)
    rod('Array power conduit',(-7.4,.13,z),(-6,.13,z),.036,'LB_SealRubber')
rod('Station communications mast',(-7,0,0),(-7,5.75,0),.085,'LB_PaintedAlloy',16)
for angle in [0,math.tau/3,2*math.tau/3]:
    dx,dz=math.sin(angle),math.cos(angle)
    rod('Mast tripod brace',(-7+dx*1.10,-.15,dz*1.10),(-7,2.45,0),.043,'LB_BrushedTitanium')
    box('Mast tripod foot',(-7+dx*1.1,-.17,dz*1.1),(.35,.08,.35),'LB_AnodizedGraphite',.025)
box('Mast service electronics',(-6.84,1.2,0),(.35,.64,.48),'LB_PaintedAlloy',.035)
# Parabolic reflector opens upward/east with ribbed reverse surface.
center=Vector((-7,5.85,0));normal=Vector((.68,.70,-.2)).normalized();tangent=normal.cross(Vector((0,0,1))).normalized();bit=normal.cross(tangent).normalized()
verts=[];rings=8;segs=48
for ri in range(rings+1):
    r=1.05*ri/rings
    for j in range(segs):
        a=j*math.tau/segs;v=center+tangent*(r*math.cos(a))+bit*(r*math.sin(a))+normal*(r*r*.32)
        verts.append(tuple(v))
faces=[]
for ri in range(rings):
    for j in range(segs):faces.append((ri*segs+j,ri*segs+(j+1)%segs,(ri+1)*segs+(j+1)%segs,(ri+1)*segs+j))
dish=mesh('Parabolic station reflector',verts,faces,'LB_PaintedAlloy')
mod=dish.modifiers.new('Reflector skin thickness','SOLIDIFY');mod.thickness=.025
for p in dish.data.polygons:p.use_smooth=True
focus=center+normal*.84
for j in range(3):
    a=j*math.tau/3;p=center+tangent*(.91*math.cos(a))+bit*(.91*math.sin(a))+normal*.275
    rod('Reflector feed support',p,focus,.019,'LB_BrushedTitanium')
rod('RF feed horn',focus-normal*.03,focus+normal*.14,.09,'LB_AnodizedGraphite',16)
for j in range(12):
    a=j*math.tau/12
    end=center+tangent*(1.04*math.cos(a))+bit*(1.04*math.sin(a))+normal*.32
    rod('Reflector backing rib',center-normal*.026,end-normal*.026,.018,'LB_BrushedTitanium')

# Clear the domed endcaps with the large solar frames rather than interpenetrating
# the pressure vessels at the old primitive blockout's approximate array position.
for obj in groups['08_PowerAndComms']:
    if obj.name.startswith(('Solar ','Photovoltaic ')):obj.location.x-=1.3

anchors_unity={
    'Origin':[0,0,0],'AxisX':[1,0,0],'AxisY':[0,1,0],'AxisZ':[0,0,1],
    'Floor':[0,0,0],'AirlockDoor':[4,1.25,1.7],'AirlockExit':[8.1,0,1.7],
    'RepairContact':[.58,1.22,.49],'ToolSpawn':[-.45,1.09,.45],
    'SupplyShelf':[0,.92,3.1],'MissionPanel':[0,2.18,1.3],
}
manifest['anchorsUnity']=anchors_unity
manifest['anchorsBlender']={k:list(u(v)) for k,v in anchors_unity.items()}
manifest['hideExistingRendererNames']=[
    'Station Habitat Shell','Station Connector','Habitat Structural Band','Solar Array','Solar Cell Stripe','Antenna Mast','Station Communications Dish',
    'Floor - teleport surface','Wall Rear','Wall Systems','Wall Left','Wall Right Front','Wall Right Rear','Ceiling','Ceiling Rib','Floor Seam','Left Rib','Right Rib','Floor Guidance','Ceiling Light Strip','Storage Panel','Systems Display',
    'Workbench Surface','Workbench Base','Workbench Safety Edge','Tool Placement Mat','Supply Shelf',
    'Airlock Lintel','Corridor Floor','Corridor Wall South','Corridor Wall North','Corridor Ceiling','Corridor Light Strip','Corridor Guidance',
]
manifest['preserveExisting']= ['All colliders, teleportation areas, gameplay components, UI and labels.','Evacuation Door and Exit Beacon are dynamic and remain untouched.','Repair Point, Oxygen Unit and interactable cargo/tool are not part of this static FBX.']

# Save editable source with studio in a clearly separate non-exported collection.
source_objects=[obj for arr in groups.values() for obj in arr]
zone('99_PreviewStudio_DO_NOT_EXPORT')
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.35));floor=own(bpy.context.object,'Preview lunar ground only','LB_AnodizedGraphite')
ground=bpy.data.materials.new('Preview regolith');ground.diffuse_color=(.15,.145,.13,1);ground.use_nodes=True
ground.node_tree.nodes.clear()
bs=ground.node_tree.nodes.new('ShaderNodeBsdfPrincipled');bs.name='Principled BSDF'
output=ground.node_tree.nodes.new('ShaderNodeOutputMaterial');output.is_active_output=True;ground.node_tree.links.new(bs.outputs['BSDF'],output.inputs['Surface'])
bs.inputs['Base Color'].default_value=(.15,.145,.13,1);bs.inputs['Roughness'].default_value=.92
noise=ground.node_tree.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=100
bump=ground.node_tree.nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.2;bump.inputs['Distance'].default_value=.13;ground.node_tree.links.new(noise.outputs['Fac'],bump.inputs['Height']);ground.node_tree.links.new(bump.outputs['Normal'],bs.inputs['Normal']);floor.data.materials.clear();floor.data.materials.append(ground)
def camera(name,p,target,lens):
    data=bpy.data.cameras.new(name);obj=bpy.data.objects.new(name,data);current.objects.link(obj);obj.location=u(p);obj.rotation_euler=(u(target)-obj.location).to_track_quat('-Z','Y').to_euler();data.lens=lens;data.clip_end=500
    return obj
cam_ext=camera('Exterior overview',(24,17,-25),(-2.4,1.2,0),46)
cam_int=camera('Systems bay interior',(2.82,2.35,-3.22),(-.7,1.6,2.40),20)
def light(name,kind,p,energy,color,size=5,target=(0,0,0)):
    data=bpy.data.lights.new(name,kind);obj=bpy.data.objects.new(name,data);current.objects.link(obj);obj.location=u(p);data.energy=energy;data.color=color
    if kind=='AREA':data.shape='DISK';data.size=size
    obj.rotation_euler=(u(target)-obj.location).to_track_quat('-Z','Y').to_euler();return obj
sun=light('Preview sun','SUN',(8,13,-10),3.4,(1,.94,.82),target=(0,0,0));sun.data.angle=.009
ext_fill=light('Preview exterior bounce','AREA',(-2,18,-2),1900,(.7,.79,1),20)
int_lights=[]
for x in [-2.3,2.3]:
    int_lights.append(light('Preview cabin practical','AREA',(x,3.08,0),120,(.78,.89,1),4,target=(x,.1,0)))
world=bpy.data.worlds.new('Preview space') if not bpy.data.worlds else bpy.data.worlds[0];scene.world=world;world.use_nodes=True
world.node_tree.nodes.clear()
background=world.node_tree.nodes.new('ShaderNodeBackground');background.name='Background'
output=world.node_tree.nodes.new('ShaderNodeOutputWorld');output.is_active_output=True;world.node_tree.links.new(background.outputs[0],output.inputs['Surface'])
background.inputs[0].default_value=(.035,.045,.07,1);background.inputs[1].default_value=.2
scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True
scene.render.resolution_x=1600;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
scene.camera=cam_ext
for img in bpy.data.images:
    if img.filepath and img.name!='Render Result':
        try:img.pack()
        except Exception:pass
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'LunarStation_Realistic.blend'))

# Export copies merged by zone and material. Originals remain editable in the blend.
export_objects=[]
export_parts={'exterior':[],'interior':[]}
for group,objs in list(groups.items()):
    if group.startswith('99'):continue
    bymat={}
    for original in objs:
        material=original.data.materials[0].name
        obj=original.copy();obj.data=original.data.copy();scene.collection.objects.link(obj)
        # Source labels are readable in Blender's camera convention. The Unity
        # anchor mapping changes the viewer's horizontal handedness: compensate
        # glyph X only on export copies, retaining editable, readable source text.
        if original.type=='FONT':obj.scale.x*=-1
        bymat.setdefault(material,[]).append(obj)
    for material,parts in bymat.items():
        bpy.ops.object.select_all(action='DESELECT')
        for obj in parts:obj.select_set(True)
        bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.convert(target='MESH')
        if len(parts)>1:bpy.ops.object.join()
        obj=bpy.context.object;obj.name=group+'__'+material
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        export_objects.append(obj)
        export_parts['interior' if group[:2] in {'05','06','07'} else 'exterior'].append(obj)
anchor_objects=[]
for key,point in anchors_unity.items():
    obj=bpy.data.objects.new('Anchor_'+key,None);scene.collection.objects.link(obj);obj.location=u(point);export_objects.append(obj);anchor_objects.append(obj)
manifest['models']=[]
for part,objects in export_parts.items():
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects+anchor_objects:obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    filename='LunarStation'+part.capitalize()+'.fbx'
    bpy.ops.export_scene.fbx(filepath=str(OUT/filename),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=False,use_mesh_modifiers=True,mesh_smooth_type='OFF',add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
    manifest['models'].append({'id':part,'fbx':filename,'meshCount':len(objects)})
for obj in export_objects:
    if obj.type=='MESH':obj.data.calc_loop_triangles()
manifest['meshCount']=sum(o.type=='MESH' for o in export_objects)
manifest['triangleCount']=sum(len(o.data.loop_triangles) for o in export_objects if o.type=='MESH')
manifest['editableObjectCount']=len(source_objects)
(OUT/'materials.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
(OUT/'base-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
(SOURCE/'integration_manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print('LUNAR_BASE_EXPORT '+json.dumps({'meshes':manifest['meshCount'],'triangles':manifest['triangleCount'],'editableObjects':len(source_objects)}),flush=True)
# Drop export copies from the preview to avoid coincident surfaces.
for obj in export_objects:bpy.data.objects.remove(obj,do_unlink=True)
scene.camera=cam_ext;scene.render.filepath=str(SOURCE/'LunarStation_Exterior.png');bpy.ops.render.render(write_still=True)
scene.camera=cam_int;scene.render.filepath=str(SOURCE/'LunarStation_Interior.png');scene.cycles.samples=64
sun.hide_render=True;ext_fill.hide_render=True;world.node_tree.nodes['Background'].inputs[1].default_value=.13
bpy.ops.render.render(write_still=True)
print('LUNAR_BASE_COMPLETE',flush=True)
