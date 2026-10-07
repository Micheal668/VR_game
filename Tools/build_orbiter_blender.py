"""Engineering-detail orbital vehicle for the existing docking gameplay.

Run: C:/Tools/blender.exe -b -t 8 --python Tools/build_orbiter_blender.py
Original game design, informed by NASA Apollo CSM/docking-system references;
the retained deployable solar wings are not an Apollo historical reconstruction.
Unity design coordinates: docking origin (0,0,0), port toward -Z, body toward +Z.
Blender coordinates are (x,-z,y); exported unit anchors define the conversion.
"""
import bpy, bmesh, json, math, random
from pathlib import Path
from mathutils import Vector, Quaternion
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/_LunarEscape/Art/LunarOrbiter'
SOURCE=ROOT/'Models/LunarOrbiter'
TEX=OUT/'Textures'
for p in (OUT,SOURCE,TEX):p.mkdir(parents=True,exist_ok=True)
random.seed(661)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
raw=bpy.data.collections.new('01_Orbiter_LOD0_Editable');scene.collection.children.link(raw)
capture=bpy.data.collections.new('04_CaptureIndicator');scene.collection.children.link(capture)
current=raw;objects=[];mats={};materials=[]

def u(p):return Vector((p[0],-p[2],p[1]))
def own(obj,name,material,detail=2):
    obj.name=name
    for c in list(obj.users_collection):c.objects.unlink(obj)
    current.objects.link(obj);obj['KeepThroughLOD']=detail
    if material:obj.data.materials.append(mats[material])
    if current==raw:objects.append(obj)
    return obj
def box(name,p,s,mat,bevel=.006,detail=2,rz=0):
    bpy.ops.mesh.primitive_cube_add(size=1,location=u(p));obj=own(bpy.context.object,name,mat,detail)
    obj.dimensions=(s[0],s[2],s[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if rz:obj.rotation_euler=Quaternion(u((0,0,1)),rz).to_euler()
    if bevel:
        mod=obj.modifiers.new('Machined edge','BEVEL');mod.width=min(bevel,min(s)*.24);mod.segments=2
        mod=obj.modifiers.new('Weighted normals','WEIGHTED_NORMAL');mod.keep_sharp=True
    return obj
def rod(name,a,b,r,mat,n=12,detail=1):
    a,b=u(a),u(b);d=b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=n,radius=r,depth=d.length,location=(a+b)/2)
    obj=own(bpy.context.object,name,mat,detail);obj.rotation_euler=d.to_track_quat('Z','Y').to_euler()
    for p in obj.data.polygons:p.use_smooth=len(p.vertices)==4
    return obj
def torus(name,z,r,t,mat,seg=64,minor=6,detail=1):
    bpy.ops.mesh.primitive_torus_add(major_segments=seg,minor_segments=minor,major_radius=r,minor_radius=t,location=u((0,0,z)))
    obj=own(bpy.context.object,name,mat,detail);obj.rotation_euler=Vector((0,0,1)).rotation_difference(u((0,0,1))).to_euler()
    for p in obj.data.polygons:p.use_smooth=True
    return obj
def mesh(name,verts,faces,mat,detail=2,closed=False):
    data=bpy.data.meshes.new(name);data.from_pydata([u(v) for v in verts],[],faces);data.update()
    if closed:
        bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.normal_update()
        if bm.calc_volume(signed=True)<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
        bm.to_mesh(data);bm.free();data.update()
    obj=bpy.data.objects.new(name,data);current.objects.link(obj);obj.data.materials.append(mats[mat]);obj['KeepThroughLOD']=detail
    if current==raw:objects.append(obj)
    return obj
def lathe(name,profile,mat,seg=64,detail=2):
    verts=[(r*math.cos(k*math.tau/seg),r*math.sin(k*math.tau/seg),z) for r,z in profile for k in range(seg)]
    faces=[]
    for i in range(len(profile)):
        nxt=(i+1)%len(profile)
        for k in range(seg):faces.append((i*seg+k,i*seg+(k+1)%seg,nxt*seg+(k+1)%seg,nxt*seg+k))
    obj=mesh(name,verts,faces,mat,detail,True)
    uv=obj.data.uv_layers.new(name='Cylindrical UV')
    for p in obj.data.polygons:
        p.use_smooth=True
        for li in p.loop_indices:
            v=obj.data.loops[li].vertex_index;uv.data[li].uv=(v%seg/seg,v//seg/max(1,len(profile)-1))
    return obj
def patch(name,a0,a1,profile,mat,n=8,detail=2,thick=.008):
    verts=[(r*math.cos(a0+(a1-a0)*k/n),r*math.sin(a0+(a1-a0)*k/n),z) for r,z in profile for k in range(n+1)]
    faces=[(i*(n+1)+k,i*(n+1)+k+1,(i+1)*(n+1)+k+1,(i+1)*(n+1)+k) for i in range(len(profile)-1) for k in range(n)]
    obj=mesh(name,verts,faces,mat,detail)
    uv=obj.data.uv_layers.new(name='Panel UV')
    for p in obj.data.polygons:
        p.use_smooth=True
        radial=Vector((p.center.x,0,p.center.z))
        if radial.length and p.normal.dot(radial)<0:
            raise RuntimeError('Unexpected inward spacecraft panel: '+name)
        for li in p.loop_indices:
            v=obj.data.loops[li].vertex_index;uv.data[li].uv=(v%(n+1)/n,v//(n+1)/max(1,len(profile)-1))
    if thick:
        mod=obj.modifiers.new('Panel sheet thickness','SOLIDIFY');mod.thickness=thick
    return obj
def text(name,body,p,size,mat,detail=0):
    curve=bpy.data.curves.new(name,'FONT');curve.body=body;curve.align_x='CENTER';curve.size=size;curve.extrude=.0008;curve.resolution_u=3
    obj=bpy.data.objects.new(name,curve);current.objects.link(obj);curve.materials.append(mats[mat]);obj.location=u(p);obj.rotation_euler=(math.pi/2,0,math.pi);obj['KeepThroughLOD']=detail
    if current==raw:objects.append(obj)
    return obj
def surface_image(name,values,normal=False):
    n=values.shape[0];rgba=np.ones((n,n,4),np.float32);rgba[:,:,:3]=values
    img=bpy.data.images.new(name,width=n,height=n,alpha=True)
    if normal:img.colorspace_settings.name='Non-Color'
    img.pixels.foreach_set(rgba.ravel());img.filepath_raw=str(TEX/(name+'.png'));img.file_format='PNG';img.save()
def normal_field(height,strength):
    gy,gx=np.gradient(height);norm=np.stack((-gx*strength,-gy*strength,np.ones_like(height)),axis=-1);norm/=np.linalg.norm(norm,axis=-1,keepdims=True)
    return norm*.5+.5
def make_textures():
    n=1024;y,x=np.mgrid[0:n,0:n].astype(np.float32)/n;rng=np.random.default_rng(21)
    grain=rng.normal(0,.007,(n,1)).repeat(n,axis=1)+rng.normal(0,.003,(n,n))
    brushed=np.stack([np.clip(c+grain,0,1) for c in (.48,.53,.57)],axis=-1)
    surface_image('BrushedAlloy_BaseColor',brushed);surface_image('BrushedAlloy_Normal',normal_field(grain,3),True)
    h=.11*np.sin(64*x+9*np.sin(7*y))+.075*np.sin(117*y+13*np.sin(17*x))+.055*np.cos(243*x+89*y)+rng.normal(0,.008,(n,n))
    foil=np.stack([np.clip(c+h*v,0,1) for c,v in [(.54,.19),(.285,.13),(.052,.055)]],axis=-1)
    surface_image('GoldMLI_BaseColor',foil);surface_image('GoldMLI_Normal',normal_field(h,12),True)
    xx=x*6;yy=y*18;edge=(np.mod(xx,1)<.022)|(np.mod(yy,1)<.025)
    fine=(np.mod(xx*11,1)<.018);cells=np.stack([np.full_like(x,c) for c in (.018,.045,.095)],axis=-1)
    jitter=(np.sin(np.floor(xx)*2.4+np.floor(yy)*1.7)*.006)
    cells+=jitter[:,:,None];cells[edge]=(.12,.16,.2);cells[fine&~edge]=(.065,.11,.15)
    surface_image('SolarLaminate_BaseColor',cells);surface_image('SolarLaminate_Normal',normal_field(edge.astype(np.float32)*.08,1.5),True)
make_textures()
def mat(name,color,metal,rough,tex=None,bump=.15,emit=None):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;m.node_tree.nodes.clear()
    p=m.node_tree.nodes.new('ShaderNodeBsdfPrincipled');p.name='Principled BSDF';p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    out=m.node_tree.nodes.new('ShaderNodeOutputMaterial');m.node_tree.links.new(p.outputs['BSDF'],out.inputs['Surface'])
    entry={'name':name,'color':[*color,1],'metallic':metal,'roughness':rough,'normalScale':bump}
    if tex:
        for suffix,srgb in [('BaseColor',True),('Normal',False)]:
            image=bpy.data.images.load(str(TEX/(tex+'_'+suffix+'.png')));node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=image
            if srgb:m.node_tree.links.new(node.outputs['Color'],p.inputs['Base Color']);entry['baseColorTexture']='Textures/'+tex+'_BaseColor.png'
            else:
                image.colorspace_settings.name='Non-Color';b=m.node_tree.nodes.new('ShaderNodeNormalMap');b.inputs['Strength'].default_value=bump;m.node_tree.links.new(node.outputs['Color'],b.inputs['Color']);m.node_tree.links.new(b.outputs['Normal'],p.inputs['Normal']);entry['normalTexture']='Textures/'+tex+'_Normal.png'
    if emit:
        p.inputs['Emission Color'].default_value=(*emit,1);p.inputs['Emission Strength'].default_value=1;entry['emission']=list(emit)
    mats[name]=m;materials.append(entry)
mat('Orb_Silver',(.48,.53,.57),.88,.29,'BrushedAlloy',.12)
mat('Orb_White',(.74,.76,.77),.18,.66)
mat('Orb_GoldMLI',(.54,.285,.052),.83,.47,'GoldMLI',.26)
mat('Orb_Graphite',(.026,.037,.046),.42,.57)
mat('Orb_Solar',(.018,.045,.095),.66,.32,'SolarLaminate',.08)
mat('Orb_Amber',(.90,.37,.055),.28,.51)
mat('Orb_Glass',(.018,.070,.095),.57,.17)
mat('Orb_Beacon',(.26,.64,.47),.04,.35,emit=(.45,1.1,.72))

# DOCKING SYSTEM: a genuine open annulus, recessed funnel and deep pressure hatch.
lathe('Machined docking flange',[(.601,-.015),(.852,-.015),(.882,.013),(.882,.09),(.843,.135),(.610,.135)],'Orb_Silver',128)
lathe('Docking flange recessed seal',[(.578,.023),(.606,.023),(.606,.12),(.578,.12)],'Orb_Graphite',96)
lathe('Docking ring aft structural barrel',[(.66,.13),(.82,.13),(.82,.45),(.71,.50),(.65,.50)],'Orb_White',96)
lathe('Open conical capture funnel',[(.575,.065),(.601,.065),(.55,.36),(.513,.54),(.487,.54),(.52,.34)],'Orb_Silver',96)
lathe('Recessed dark transfer tunnel',[(.477,.45),(.506,.45),(.506,1.11),(.477,1.11)],'Orb_Graphite',96)
for z,r in [(.32,.540),(.56,.495),(.85,.487)]:torus('Transfer tunnel retaining bead',z,r,.012,'Orb_Silver',64,6,1)
rod('Recessed crew pressure hatch',(0,0,1.115),(0,0,1.142),.47,'Orb_Graphite',64,2)
lathe('Pressure hatch perimeter',[(.415,1.094),(.448,1.094),(.448,1.123),(.415,1.123)],'Orb_Silver',64,1)
rod('Hatch central spindle',(0,0,1.048),(0,0,1.105),.09,'Orb_Silver',24,1)
for a in [math.pi/2,math.pi/2+math.tau/3,math.pi/2+2*math.tau/3]:
    rod('Hatch recessed wheel spoke',(0,0,1.055),(.31*math.cos(a),.31*math.sin(a),1.055),.018,'Orb_Silver',8,1)
for j in range(12):
    a=(j+.5)*math.tau/12;c,s=math.cos(a),math.sin(a)
    box('Docking latch pocket',(.76*c,.76*s,-.020),(.166,.104,.060),'Orb_Graphite',.008,1,a)
    box('Docking capture latch jaw',(.773*c,.773*s,-.050),(.121,.061,.048),'Orb_Silver',.006,1,a)
    for r in [.705,.832]:rod('Docking flange captive hex bolt',(r*c,r*s,-.019),(r*c,r*s,-.044),.024,'Orb_Silver',6,0)
    rod('Latch pivot pin',(.772*c-.048*s,.772*s+.048*c,-.057),(.772*c+.048*s,.772*s-.048*c,-.057),.014,'Orb_Graphite',10,0)
    if j%3==0:box('Docking clocking index',(.907*c,.907*s,-.018),(.06,.17,.025),'Orb_Amber',.002,1,a)
for j in range(3):
    a=math.radians(30+120*j);r=Vector((math.cos(a),math.sin(a),0));t=Vector((-math.sin(a),math.cos(a),0))
    coords=[]
    for z,ri,ro,w in [(-.095,.83,1.005,.075),(.12,.76,1.005,.11)]:
        for rr,tt in [(ri,-w),(ro,-w),(ro,w),(ri,w)]:
            p=r*rr+t*tt;p.z=z;coords.append(p)
    obj=mesh('Three point capture guide',coords,[(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],'Orb_Silver',2,True)
    mod=obj.modifiers.new('Guide contact radius','BEVEL');mod.width=.014;mod.segments=2
    box('Capture guide wear insert',(r.x*.94,r.y*.94,-.108),(.06,.10,.024),'Orb_Graphite',.005,1,a)
for a in [0,math.pi,math.pi*1.5]:
    c,s=math.cos(a),math.sin(a)
    rod('Recessed docking navigation light',(.966*c,.966*s,-.037),(.966*c,.966*s,-.060),.031,'Orb_Beacon',16,1)
box('Capture indicator protective socket',(0,.90,-.060),(.55,.13,.070),'Orb_Graphite',.013,2)
box('Docking identification plaque',(0,1.08,.018),(.65,.14,.026),'Orb_Graphite',.01,1)
text('Docking identification','DOCK  /  01',(0,1.055,-.002),.084,'Orb_White')
mesh('Up alignment chevron',[(-.07,1.17,.011),(0,1.26,.011),(.07,1.17,.011),(.034,1.17,.011),(0,1.211,.011),(-.034,1.17,.011)],[(0,5,4,3,2,1)],'Orb_Amber',1)
for x in [-1.045,1.045]:
    box('Roll index white',(x,0,.020),(.12,.20,.026),'Orb_White',.003,1)
    box('Roll index dark',(x,0,-.002),(.12,.064,.018),'Orb_Graphite',.001,1)

# COMMAND PRESSURE VESSEL, clad in individual metallic/thermal protection panels.
cone=[(.88,.42),(.89,.77),(1.13,1.35),(1.39,1.96),(1.69,2.66),(1.955,3.29)]
lathe('Command module pressure shell',cone+[(r-.026,z) for r,z in reversed(cone)],'Orb_Silver',128)
for j in range(16):
    a=j*math.tau/16
    skin=[(r+.012,z) for r,z in cone[1:]]
    patch('Command module removable skin',a+.012,a+math.tau/16-.012,skin,'Orb_Silver' if j%5 else 'Orb_White',8,1,.005)
    for r,z in [cone[2],cone[4]]:
        r+=.026
        rod('Command skin quarter turn',(r*math.cos(a+.08),r*math.sin(a+.08),z-.008),(r*math.cos(a+.08),r*math.sin(a+.08),z+.014),.015,'Orb_Graphite',6,0)
torus('Command heat shield separation rim',3.30,1.948,.048,'Orb_Graphite',96,8,2)
lathe('CM SM separation adapter',[(1.91,3.32),(1.91,3.41),(1.77,3.60),(1.73,3.60),(1.83,3.37)],'Orb_Silver',96)
def cmr(z):
    for (r0,z0),(r1,z1) in zip(cone[:-1],cone[1:]):
        if z0<=z<=z1:return r0+(r1-r0)*(z-z0)/(z1-z0)
    return cone[-1][0]
for center in [math.radians(57),math.radians(123)]:
    patch('Forward rendezvous window bezel',center-.165,center+.165,[(cmr(z)+.027,z) for z in [1.37,1.93]],'Orb_White',5,2,.019)
    patch('Forward window resilient seal',center-.137,center+.137,[(cmr(z)+.052,z) for z in [1.415,1.88]],'Orb_Graphite',5,2,.010)
    patch('Smoked multilayer command window',center-.112,center+.112,[(cmr(z)+.065,z) for z in [1.455,1.837]],'Orb_Glass',5,2,.006)
for center in [math.radians(12),math.radians(168)]:
    patch('Side observation port frame',center-.14,center+.14,[(cmr(z)+.025,z) for z in [2.15,2.69]],'Orb_Graphite',5,1,.024)
    patch('Side observation pressure glass',center-.11,center+.11,[(cmr(z)+.053,z) for z in [2.20,2.64]],'Orb_Glass',5,2,.009)
# An independently outlined side hatch with hinge blocks and a protected handle.
hc=math.radians(-65)
patch('Crew access hatch pressure frame',hc-.23,hc+.23,[(cmr(z)+.025,z) for z in [1.63,2.85]],'Orb_Graphite',8,1,.024)
patch('Crew access hatch cover',hc-.205,hc+.205,[(cmr(z)+.056,z) for z in [1.69,2.79]],'Orb_Silver',8,1,.015)
for z in [1.87,2.59]:
    r=cmr(z)+.09;a=hc+.21
    box('Access hatch hinge',(r*math.cos(a),r*math.sin(a),z),(.10,.12,.21),'Orb_White',.009,1,a)
    a=hc-.12;r=cmr(z)+.088
    rod('Hatch locking dog',(r*math.cos(a),r*math.sin(a),z-.04),(r*math.cos(a),r*math.sin(a),z+.04),.029,'Orb_Graphite',8,0)

# SERVICE MODULE: segmented white radiators, gold MLI equipment bays and piping.
lathe('Service module structure',[(1.73,3.56),(1.75,3.76),(1.75,7.10),(1.68,7.39),(1.65,7.39),(1.71,3.58)],'Orb_Silver',96)
for j in range(12):
    a=j*math.tau/12;material='Orb_GoldMLI' if j in [0,3,6,9] else 'Orb_White'
    patch('Service module thermal sector',a+.018,a+math.tau/12-.018,[(1.772,3.88),(1.779,4.55),(1.781,5.2),(1.773,5.95),(1.770,7.04)],material,6,2,.012)
    if material=='Orb_White':
        for k in range(4):
            angle=a+.075+k*.12
            rod('Radiator bonded heat pipe',(1.799*math.cos(angle),1.799*math.sin(angle),4.0),(1.799*math.cos(angle),1.799*math.sin(angle),6.90),.012,'Orb_Silver',8,0)
    else:
        for z in [4.05,6.85]:
            for angle in [a+.08,a+.44]:
                r=1.795;rod('Thermal blanket fastener',(r*math.cos(angle),r*math.sin(angle),z-.015),(r*math.cos(angle),r*math.sin(angle),z+.015),.019,'Orb_Silver',6,0)
for z in [3.74,7.12]:torus('Service bay circumferential band',z,1.782,.038,'Orb_Silver',96,6,1)
for j in range(24):
    a=j*math.tau/24
    for z in [3.72,7.14]:rod('Circumferential assembly bolt',(1.797*math.cos(a),1.797*math.sin(a),z-.012),(1.797*math.cos(a),1.797*math.sin(a),z+.012),.022,'Orb_Graphite',6,0)
lathe('Aft MLI equipment skirt',[(1.69,7.17),(1.64,7.42),(.64,7.49),(.61,7.45),(1.60,7.37),(1.65,7.17)],'Orb_GoldMLI',96,2)
for side in [-1,1]:
    for q in range(3):
        a=math.radians((260 if side<0 else 80)+q*5);r=1.818
        rod('External insulated coolant line',(r*math.cos(a),r*math.sin(a),4.04),(r*math.cos(a),r*math.sin(a),6.94),.021,'Orb_Graphite',10,1)
        for z in [4.35,5.1,5.85,6.6]:
            rod('Coolant line retention clamp',(r*math.cos(a)-.038,r*math.sin(a),z),(r*math.cos(a)+.038,r*math.sin(a),z),.013,'Orb_Silver',8,0)

def small_nozzle(name,base,direction,length=.30,width=.11,detail=1):
    profile=[(.043,0),(.049,.08),(.065,length*.58),(width,length), (width-.011,length),(.053,length*.58),(.035,.08),(.032,0)]
    obj=lathe(name,profile,'Orb_Graphite',24,detail);obj.location=u(base);obj.rotation_euler=Vector((0,-1,0)).rotation_difference(u(direction)).to_euler()
    return obj
for j in range(4):
    a=math.pi/4+j*math.pi/2;r=Vector((math.cos(a),math.sin(a),0));t=Vector((-math.sin(a),math.cos(a),0));base=r*1.90;base.z=4.15
    box('RCS quad structural mount',base,(.40,.32,.55),'Orb_Silver',.02,2,a)
    cover=base+r*.035;box('RCS quad gold thermal cover',cover,(.28,.27,.46),'Orb_GoldMLI',.018,1,a)
    for side in [-1,1]:
        p=base+t*(side*.15);small_nozzle('Tangential RCS vacuum nozzle',p,t*side,.29,.10,1)
        p=base+Vector((0,0,side*.25));small_nozzle('Axial RCS vacuum nozzle',p,(0,0,side),.32,.12,1)
    rod('RCS propellant feed',(r.x*1.77,r.y*1.77,4.55),(base.x,base.y,4.33),.035,'Orb_Silver',12,1)

# Long vacuum engine bell with a modeled inner wall and an open throat.
bell=[(.31,7.38),(.34,7.68),(.39,7.94),(.53,8.28),(.72,8.65),(.955,9.04)]
lathe('Main propulsion vacuum nozzle',bell+[(r-.030,z) for r,z in reversed(bell)],'Orb_Graphite',128,2)
torus('Nozzle rolled exit lip',9.04,.944,.026,'Orb_Silver',96,6,2)
for j in range(15):
    z=7.77+j*.085
    for (r0,z0),(r1,z1) in zip(bell[:-1],bell[1:]):
        if z0<=z<=z1:r=r0+(r1-r0)*(z-z0)/(z1-z0);break
    torus('Engine regenerative cooling hoop',z,r+.016,.013,'Orb_Silver',64,4,0)
for j in range(4):
    a=math.pi/4+j*math.pi/2
    rod('Engine gimbal support',(.90*math.cos(a),.90*math.sin(a),7.34),(.36*math.cos(a),.36*math.sin(a),7.71),.049,'Orb_Silver',12,1)

# Deployable solar wings: existing overall layout retained; active cells face -Z.
for sign in [-1,1]:
    rod('Solar deployment boom',(sign*1.70,0,5.32),(sign*8.10,0,5.32),.069,'Orb_Silver',12,2)
    rod('Solar root rotary joint',(sign*1.72,0,5.32),(sign*2.27,0,5.32),.21,'Orb_Graphite',32,2)
    for i in range(4):
        x=sign*(3.25+1.4*i)
        box('Solar wing structural sandwich',(x,0,5.22),(1.30,3.60,.060),'Orb_Graphite',.010,2)
        # Front laminate uses a planar UV rectangle for one compact repeated PBR map.
        o=mesh('Photovoltaic laminate',[(x-.608,-1.737,5.183),(x+.608,-1.737,5.183),(x+.608,1.737,5.183),(x-.608,1.737,5.183)],[(0,3,2,1)],'Orb_Solar',2)
        uv=o.data.uv_layers.new(name='Solar laminate UV')
        for li,co in zip(o.data.polygons[0].loop_indices,[(0,0),(0,1),(1,1),(1,0)]):uv.data[li].uv=co
        for xx in [x-.642,x+.642]:box('Wing longitudinal edge extrusion',(xx,0,5.18),(.024,3.60,.072),'Orb_Silver',.004,2)
        for yy in [-1.785,1.785]:box('Wing transverse edge extrusion',(x,yy,5.18),(1.30,.030,.072),'Orb_Silver',.004,2)
        if i<3:
            for yy in [-1.37,1.37]:rod('Solar panel folding hinge',(x+sign*.66,yy,5.24),(x+sign*.75,yy,5.24),.046,'Orb_Graphite',12,1)
        for yy in [-1.35,-.45,.45,1.35]:box('Solar panel rear stiffener',(x,yy,5.268),(1.24,.027,.025),'Orb_Silver',.003,1)
        if i in [0,3]:text('Wing panel serial','SA '+('L' if sign<0 else 'R')+' / '+str(i+1),(x,1.56,5.164),.063,'Orb_White')
        for xx in [x-.62,x+.62]:
            for yy in [-1.65,1.65]:rod('Solar frame captive fastener',(xx,yy,5.166),(xx,yy,5.154),.013,'Orb_Graphite',6,0)

# Independent capture lamp. OrbiterView toggles only this renderer, never an LOD.
current=capture
box('Capture Indicator',(0,.90,-.102),(.435,.058,.025),'Orb_Beacon',.008,2)
current=raw
anchors={'Origin':[0,0,0],'AxisX':[1,0,0],'AxisY':[0,1,0],'AxisZ':[0,0,1],
         'DockingCenter':[0,0,0],'DockingPort':[0,0,0],'PortNormal':[0,0,-1],
         'CaptureIndicator':[0,.9,-.1],'EngineExit':[0,0,9.04],
         'WingTipLeft':[-8.10,0,5.32],'WingTipRight':[8.10,0,5.32]}
manifest={'name':'Lunar Orbiter - original game spacecraft','source':'Models/LunarOrbiter/LunarOrbiter_Realistic.blend',
          'units':'metres','coordinateConvention':'Unity XYZ design coordinates mapped to Blender (X,-Z,Y). Use all four axis anchors; do not scale by bounds.',
          'materials':materials,'anchorsUnity':anchors,'models':[],'materialCount':len(materials),'textureCount':6,
          'designNote':'Original two-solar-wing game design. Apollo CSM and docking hardware are engineering references, not a claim of historical reconstruction.',
          'references':[
              {'title':'Apollo experience report: The docking system, NASA TN D-6854','url':'https://ntrs.nasa.gov/citations/19720018207','use':'Engineering reference for recessed crew-transfer tunnel, probe/drogue capture geometry and ring latches; no mesh or texture downloaded.'},
              {'title':'NASA Apollo spacecraft diagrams','url':'https://www.nasa.gov/history/SP-4225/diagrams/apollo/apollo-diagram.htm','use':'Reference for command/service module proportions, RCS quads and engine placement; no downloadable art included.'},
              {'title':'Apollo 9 command/service module photograph, NASA/JSC','url':'https://science.nasa.gov/resource/apollo-9-mission-image-command-module/','use':'Reference for silver CSM surface, white radiators and service module details; no photograph used as texture.'}],
          'downloadedAssets':[],'authoredAssets':'All geometry and six compact material textures are generated by this project script.'}

def merged_copies(source_objects,label,minimum):
    bymat={}
    for original in source_objects:
        if original.get('KeepThroughLOD',2)<minimum:continue
        obj=original.copy();obj.data=original.data.copy();scene.collection.objects.link(obj)
        if original.type=='FONT':obj.scale.x*=-1 # compensate Unity view handedness; source font stays readable
        bymat.setdefault(original.data.materials[0].name,[]).append(obj)
    result=[]
    for material,parts in bymat.items():
        bpy.ops.object.select_all(action='DESELECT')
        for obj in parts:obj.select_set(True)
        bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.convert(target='MESH')
        if len(parts)>1:bpy.ops.object.join()
        obj=bpy.context.object;obj.name=label+'__'+material;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);obj.data.calc_loop_triangles();result.append(obj)
    return result
def triangles(parts):
    for obj in parts:obj.data.calc_loop_triangles()
    return sum(len(obj.data.loop_triangles) for obj in parts)
def reduce(parts,target):
    count=triangles(parts)
    if count<=target:return
    ratio=target/count*.97
    for obj in parts:
        bpy.context.view_layer.objects.active=obj;mod=obj.modifiers.new('Distance LOD reduction','DECIMATE');mod.ratio=ratio;mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name)
    assert triangles(parts)<=target*1.04,(triangles(parts),target)
def export(parts,id,filename):
    empties=[]
    for name,p in anchors.items():
        obj=bpy.data.objects.new('Anchor_'+name,None);scene.collection.objects.link(obj);obj.location=u(p);empties.append(obj)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts+empties:obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    bpy.ops.export_scene.fbx(filepath=str(OUT/filename),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=False,use_mesh_modifiers=True,mesh_smooth_type='OFF',add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
    verts=np.array([(v.co.x,v.co.z,-v.co.y) for obj in parts for v in obj.data.vertices],dtype=np.float64)
    lo=verts.min(axis=0);hi=verts.max(axis=0)
    entry={'id':id,'fbx':filename,'meshCount':len(parts),'triangleCount':triangles(parts),
           'boundsUnity':{'minimum':lo.tolist(),'maximum':hi.tolist()}}
    assert lo[0]>=-8.3 and hi[0]<=8.3 and lo[1]>=-2.2 and hi[1]<=2.2 and lo[2]>=-.13 and hi[2]<=9.1,entry
    manifest['models'].append(entry)
    print('ORBITER_MODEL '+json.dumps(entry),flush=True)
    for obj in empties:bpy.data.objects.remove(obj,do_unlink=True)
lod_parts=[]
for level,budget in [(0,94000),(1,23000),(2,7400)]:
    parts=merged_copies(objects,'LOD'+str(level),level);reduce(parts,budget)
    export(parts,'lod'+str(level),'OrbiterLOD'+str(level)+'.fbx');lod_parts.extend(parts)
    if level>0:
        col=bpy.data.collections.new('0'+str(level+1)+'_Orbiter_LOD'+str(level));scene.collection.children.link(col)
        for obj in parts:
            obj.select_set(False)
            for c in list(obj.users_collection):c.objects.unlink(obj)
            col.objects.link(obj)
        col.hide_render=True;col.hide_viewport=True
parts=merged_copies(list(capture.objects),'Capture Indicator',0)
parts[0].name='Capture Indicator';export(parts,'capture','CaptureIndicator.fbx')
for obj in parts+[p for p in lod_parts if p.name.startswith('LOD0__')]:bpy.data.objects.remove(obj,do_unlink=True)
manifest['boundsUnity']=manifest['models'][0]['boundsUnity']
manifest['textureResolution']=1024
manifest['textureFileBytes']=sum(p.stat().st_size for p in TEX.glob('*.png'))
manifest['editableLOD0Objects']=len(objects)
(OUT/'orbiter-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
(SOURCE/'sources-and-statistics.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')

# Separate studio; cameras, lighting and backdrop are never exported to Unity.
studio=bpy.data.collections.new('99_PreviewStudio_DO_NOT_EXPORT');scene.collection.children.link(studio)
def camera(name,p,target,lens):
    data=bpy.data.cameras.new(name);obj=bpy.data.objects.new(name,data);studio.objects.link(obj);obj.location=u(p);obj.rotation_euler=(u(target)-obj.location).to_track_quat('-Z','Y').to_euler();data.lens=lens;return obj
overview=camera('Orbiter engineering overview',(13.4,7.8,-20),(0,0,4.0),49)
closeup=camera('Docking interface closeup',(2,1.4,-4.8),(0,.1,.45),49)
aft=camera('Orbiter engine and service bay',(-11,6,16),(0,0,5.2),49)
def area(name,p,target,power,color,size):
    data=bpy.data.lights.new(name,'AREA');obj=bpy.data.objects.new(name,data);studio.objects.link(obj);obj.location=u(p);obj.rotation_euler=(u(target)-obj.location).to_track_quat('-Z','Y').to_euler();data.energy=power;data.color=color;data.shape='DISK';data.size=size
area('Preview solar key',(7,10,-5),(0,0,3.5),2000,(1,.91,.77),6)
area('Preview cool structural fill',(-8,2,-2),(0,0,3),1400,(.56,.72,1),8)
area('Preview aft rim',(0,6,10),(0,0,4),1900,(.85,.92,1),7)
area('Preview port bounce',(0,0,-4),(0,0,.5),160,(.8,.9,1),3)
world=bpy.data.worlds.new('Orbital preview black');scene.world=world;world.use_nodes=True;world.node_tree.nodes.clear()
bg=world.node_tree.nodes.new('ShaderNodeBackground');bg.inputs[0].default_value=(.012,.018,.035,1);bg.inputs[1].default_value=.26
output=world.node_tree.nodes.new('ShaderNodeOutputWorld');world.node_tree.links.new(bg.outputs[0],output.inputs[0])
scene.render.engine='CYCLES';scene.cycles.samples=40;scene.cycles.use_denoising=True
scene.render.resolution_x=1800;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.camera=overview
for image in bpy.data.images:
    if image.filepath:
        try:image.pack()
        except Exception:pass
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'LunarOrbiter_Realistic.blend'))
scene.render.filepath=str(SOURCE/'Orbiter_Overview.png');bpy.ops.render.render(write_still=True)
scene.camera=closeup;scene.render.filepath=str(SOURCE/'Orbiter_DockingPort.png');bpy.ops.render.render(write_still=True)
scene.camera=aft;scene.render.filepath=str(SOURCE/'Orbiter_Aft.png');bpy.ops.render.render(write_still=True)
print('ORBITER_BUILD_COMPLETE',flush=True)
