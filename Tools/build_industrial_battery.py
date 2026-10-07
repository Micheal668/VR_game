"""Original Luna Escape portable industrial battery, modeled in Blender.

The Victron service manual is structural reference only, not a source model.
No third-party images, logos, labels or geometry are copied into this asset.
"""
import bpy, bmesh, math, json, hashlib, numpy as np
from pathlib import Path
from mathutils import Vector

ROOT=Path('C:/Coding/Projects/VR_game')
OUT=ROOT/'Assets/_LunarEscape/Art/StationProps/industrial_battery'
TEX=OUT/'textures';TEX.mkdir(parents=True,exist_ok=True)
MODEL=ROOT/'Models/LunarBase/IndustrialBattery.blend'
PREVIEW=ROOT/'Models/LunarBase/Downloads/Previews/industrial_battery.png'
REFERENCE='https://www.victronenergy.com/media/pg/Lithium_Battery_Smart_-_Circuit_Board_Replacement_Instructions/en/replacing-the-circuit-board.html'
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
parts=[];materials=[];material_rows=[]
def rel(p):return str(p.relative_to(ROOT)).replace('\\','/')

def png(name,pixels,linear=True):
    h,w=pixels.shape[:2];im=bpy.data.images.new(name,width=w,height=h,alpha=True)
    if linear:im.colorspace_settings.name='Non-Color'
    im.pixels.foreach_set(np.asarray(pixels,dtype=np.float32).ravel())
    im.file_format='PNG';im.filepath_raw=str(TEX/(name+'.png'));im.save()
    return im

# Fine injection-molded polymer grain, deterministic and tileable. 1K is enough
# for a ~30 cm shell; the metalwork uses physically distinct scalar materials.
rng=np.random.default_rng(25102026);n=1024
grain=rng.random((n,n)).astype(np.float32)
soft=sum(np.roll(np.roll(grain,dy,0),dx,1) for dx in [-1,0,1] for dy in [-1,0,1])/9
tone=.020+(soft-.5)*.010
base=np.ones((n,n,4),dtype=np.float32)
base[:,:,0]=tone*.90;base[:,:,1]=tone*.99;base[:,:,2]=tone*1.07
shell_base=png('industrial_battery_polymer_base_1k',base,False)
gx=(np.roll(soft,-1,1)-np.roll(soft,1,1))*.16
gy=(np.roll(soft,-1,0)-np.roll(soft,1,0))*.16
normal=np.ones((n,n,4),dtype=np.float32);normal[:,:,0]=.5-gx;normal[:,:,1]=.5-gy;normal[:,:,2]=1
shell_normal=png('industrial_battery_polymer_normal_1k',normal,True)
ms=np.zeros((n,n,4),dtype=np.float32);ms[:,:,0]=.015;ms[:,:,3]=.28+(soft-.5)*.12
shell_ms=png('industrial_battery_polymer_metallic_smoothness_1k',ms,True)

def material(name,color,metallic,roughness,textured=False):
    mat=bpy.data.materials.new('industrial_battery_'+name);mat.diffuse_color=color;mat.use_nodes=True
    nodes=mat.node_tree.nodes;nodes.clear();links=mat.node_tree.links
    bs=nodes.new('ShaderNodeBsdfPrincipled');bs.inputs['Base Color'].default_value=color
    bs.inputs['Metallic'].default_value=metallic;bs.inputs['Roughness'].default_value=roughness
    output=nodes.new('ShaderNodeOutputMaterial');links.new(bs.outputs[0],output.inputs['Surface'])
    row={'name':mat.name,'base_color':'','normal':'','metallic_smoothness':'','occlusion':'',
         'color':list(color),'metallic':metallic,'roughness':roughness,'smoothness':1-roughness,'alpha_clip':False}
    if textured:
        tx=nodes.new('ShaderNodeTexImage');tx.image=shell_base;links.new(tx.outputs['Color'],bs.inputs['Base Color'])
        nm=nodes.new('ShaderNodeTexImage');nm.image=shell_normal
        convert=nodes.new('ShaderNodeNormalMap');links.new(nm.outputs['Color'],convert.inputs['Color']);links.new(convert.outputs[0],bs.inputs['Normal'])
        mask=nodes.new('ShaderNodeTexImage');mask.image=shell_ms
        sep=nodes.new('ShaderNodeSeparateColor');links.new(mask.outputs['Color'],sep.inputs[0]);links.new(sep.outputs['Red'],bs.inputs['Metallic'])
        inv=nodes.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1
        links.new(mask.outputs['Alpha'],inv.inputs[1]);links.new(inv.outputs[0],bs.inputs['Roughness'])
        row.update({'base_color':rel(Path(shell_base.filepath_raw)),'normal':rel(Path(shell_normal.filepath_raw)),
                    'metallic_smoothness':rel(Path(shell_ms.filepath_raw)),'color':[1,1,1,1],'smoothness':1})
    materials.append(mat);material_rows.append(row);return mat

polymer=material('polymer',(0.02,.023,.027,1),.015,.72,True)
aluminum=material('anodized_aluminum',(.36,.405,.435,1),.88,.32)
steel=material('stainless_fasteners',(.49,.52,.54,1),.95,.25)
rubber=material('rubber_seals',(.008,.009,.010,1),0,.85)
ivory=material('printed_plate',(.67,.69,.64,1),.08,.53)
ink=material('plate_ink',(.015,.018,.019,1),.02,.65)
amber=material('safety_amber',(.68,.34,.035,1),.08,.43)
red=material('positive_terminal',(.35,.02,.012,1),.025,.48)
green=material('status_green',(.025,.48,.20,1),.1,.27)

def finish(obj,name,mat):
    obj.name=name;obj.data.materials.append(mat);parts.append(obj);return obj
def box(name,loc,scale,mat,bevel=.0015,segments=2):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.dimensions=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    finish(o,name,mat)
    if bevel:
        b=o.modifiers.new('Manufactured edge radius','BEVEL');b.width=bevel;b.segments=segments
        b.affect='EDGES'
        o.modifiers.new('Weighted flat normals','WEIGHTED_NORMAL')
    return o
def cylinder(name,loc,radius,depth,mat,verts=16,rotation=(0,0,0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts,radius=radius,depth=depth,location=loc,rotation=rotation)
    o=finish(bpy.context.object,name,mat)
    b=o.modifiers.new('Fastener edge chamfer','BEVEL');b.width=min(.0006,radius*.1);b.segments=1
    o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');return o
def text(name,body,loc,size,mat,rotation=(math.pi/2,0,0),align='LEFT'):
    c=bpy.data.curves.new(name,'FONT');c.body=body;c.size=size;c.align_x=align;c.resolution_u=3;c.extrude=0
    o=bpy.data.objects.new(name,c);bpy.context.collection.objects.link(o);o.location=loc;o.rotation_euler=rotation
    o.data.materials.append(mat);parts.append(o);return o
def bolt(name,x,y,z,front=False):
    rot=(math.pi/2,0,0) if front else (0,0,0)
    cylinder(name,(x,y,z),.0033,.0019,steel,12,rot)
    if front:box(name+' driver slot',(x,y-.0011,z),(.0038,.0002,.0006),ink,0)
    else:box(name+' driver slot',(x,y,z+.0011),(.0038,.0006,.0002),ink,0)

# Sealed composite case and separate lid reveal.
box('Main molded composite battery housing',(0,0,.111),(.308,.196,.202),polymer,.009,3)
box('Lid perimeter elastomer gasket',(0,0,.215),(.314,.203,.008),rubber,.005,2)
box('Replaceable top cover',(0,0,.225),(.320,.208,.016),polymer,.005,3)
box('Lower service seam',(0,0,.018),(.313,.201,.008),rubber,.004,2)

# Protection rails and feet are geometric, not painted-on corner details.
for sx in [-1,1]:
    for sy in [-1,1]:
        box('Corner extrusion', (sx*.159,sy*.098,.118),(.020,.022,.216),aluminum,.003,2)
        box('Replaceable rubber corner shoe',(sx*.157,sy*.096,.012),(.027,.028,.024),rubber,.004,2)
    box('Upper side impact rail',(sx*.160,0,.217),(.020,.212,.019),aluminum,.003,2)
    box('Lower side impact rail',(sx*.158,0,.020),(.020,.210,.021),aluminum,.003,2)
    for y in [-.077,.077]:
        bolt('Cover screw',sx*.146,y,.235)
    # Shallow stiffening fins on each side; no vents into the sealed cell cavity.
    for y in [-.06,-.02,.02,.06]:
        box('Molded side stiffener',(sx*.1555,y,.121),(.006,.012,.144),polymer,.002,2)

# Real open-grip handle. Low rails keep the grip inside the maximum height.
for sx in [-1,1]:
    box('Carry handle hinge block',(sx*.093,0,.238),(.026,.033,.012),aluminum,.003,2)
    cylinder('Handle hinge pin',(sx*.093,0,.245),.006,.029,steel,16,(math.pi/2,0,0))
    box('Handle upright',(sx*.093,0,.261),(.014,.018,.040),aluminum,.003,3)
box('Handle upper bridge',(0,0,.278),(.190,.021,.014),aluminum,.005,3)
box('Molded handle grip',(0,0,.278),(.128,.027,.018),rubber,.006,3)
for x in np.linspace(-.049,.049,8):
    box('Grip rib',(float(x),-.014,.277),(.005,.002,.012),polymer,.0005,1)

# Insulated, separately shrouded terminal wells; metallic posts remain visible.
for sx,terminal_mat in [(-1,red),(1,rubber)]:
    x=sx*.112;y=.063
    cylinder('Terminal insulated base',(x,y,.237),.018,.010,terminal_mat,24)
    cylinder('Terminal brass contact',(x,y,.246),.006,.014,steel,16)
    cylinder('Terminal hex retaining nut',(x,y,.251),.010,.004,steel,6)
    box('Terminal raised rear shroud',(x,.087,.248),(.040,.006,.027),terminal_mat,.002,2)
    for side in [-1,1]:box('Terminal side protection',(x+side*.019,.067,.246),(.006,.041,.024),terminal_mat,.002,2)
    text('Terminal polarity','+' if sx<0 else '-',(x,.037,.236),.017,ivory,(0,0,0),'CENTER')

# Front steel over-center latches and their base plates.
for x in [-.110,.110]:
    box('Latch mounting plate',(x,-.103,.205),(.027,.004,.038),steel,.003,2)
    box('Over-center draw latch',(x,-.108,.203),(.015,.006,.030),aluminum,.003,2)
    box('Latch finger recess',(x,-.1115,.203),(.007,.001,.013),rubber,.001,2)
    cylinder('Latch hinge barrel',(x,-.108,.220),.0035,.023,steel,12,(0,math.pi/2,0))
    for z in [.191,.218]:bolt('Latch base rivet',x,-.107,z,True)

# Engraved printed rating plate with fictional project identification.
box('Front rating plate',(0,-.1008,.126),(.238,.0020,.106),ivory,.003,3)
box('Header stripe',(0,-.1021,.164),(.228,.0006,.022),ink,.0004,1)
text('Battery title','LUNAR  /  FIELD POWER',(-.106,-.1026,.160),.0100,ivory)
text('Battery voltage','25.6 V',(-.105,-.1026,.137),.019,ink)
text('Capacity line','20 Ah   /   512 Wh',(-.105,-.1026,.120),.0105,ink)
text('Chemistry line','LiFePO4  |  SEALED POWER MODULE',(-.105,-.1026,.104),.0067,ink)
text('Serial number','LP-24    S/N 004-2198',(-.105,-.1026,.091),.0065,ink)
for x in [-.111,.111]:
    for z in [.080,.172]:bolt('Rating plate screw',x,-.1024,z,True)
for i in range(29):
    x=.037+i*.0024
    box('Serial barcode',(x,-.1027,.090),(.0006 if i%3 else .0011,.00025,.009),ink,0)

# State-of-charge window and press-to-test button integrated below the handle.
box('Charge display bezel',(0,-.044,.235),(.111,.029,.007),rubber,.004,3)
box('Charge display smoked window',(-.006,-.044,.239),(.078,.018,.002),ink,.002,2)
for i in range(4):
    box('Charge level segment',( -.035+i*.018,-.044,.2404),(.011,.011,.0008),green,.001,2)
cylinder('Battery test button',(.044,-.044,.240),.006,.003,amber,20)
text('Test button label','TEST',(.044,-.064,.235),.005,ivory,(0,0,0),'CENTER')

# Caution strip is visibly a product label, without third-party branding.
box('Caution label',(0,-.102,.049),(.215,.001,.020),amber,.001,2)
text('Caution label text','DO NOT OPEN  /  ISOLATE BEFORE SERVICE',(-.098,-.1028,.046),.0060,ink)
for x in [-.149,.149]:
    for z in [.046,.184]:bolt('Frame countersunk screw',x,-.111,z,True)

# Keep individually named, editable components in the source blend. The source
# file owns no implementation of runtime interactions or electrical behavior.
meshes=[]
for obj in parts:
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    if obj.type=='FONT':bpy.ops.object.convert(target='MESH');obj=bpy.context.object
    if obj.type=='MESH':
        if not obj.data.uv_layers:
            bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.cube_project(cube_size=.25);bpy.ops.object.mode_set(mode='OBJECT')
        meshes.append(obj)

# Measured total bounds, then scale Y (Blender Z) very slightly so exact overall
# target height includes the rubber grip. This preserves a manufacturable shape.
dg=bpy.context.evaluated_depsgraph_get()
coords=[obj.matrix_world@Vector(v) for obj in meshes for v in obj.evaluated_get(dg).bound_box]
lo=Vector([min(v[i] for v in coords) for i in range(3)]);hi=Vector([max(v[i] for v in coords) for i in range(3)])
mid=(lo+hi)*.5
for obj in meshes:obj.location-=mid
source_dims=hi-lo
bpy.context.view_layer.update()
dg=bpy.context.evaluated_depsgraph_get()

# Export a single mesh with named submesh materials to keep Unity's draw count
# predictable. These joined export objects are removed before saving .blend.
clones=[]
for obj in meshes:
    eo=obj.evaluated_get(dg);mesh=bpy.data.meshes.new_from_object(eo,preserve_all_data_layers=True,depsgraph=dg)
    mesh.transform(obj.matrix_world)
    clone=bpy.data.objects.new('Export '+obj.name,mesh);bpy.context.collection.objects.link(clone);clones.append(clone)
bpy.ops.object.select_all(action='DESELECT')
for obj in clones:obj.select_set(True)
bpy.context.view_layer.objects.active=clones[0];bpy.ops.object.join()
combined=bpy.context.object;combined.name='industrial_battery'
bm=bmesh.new();bm.from_mesh(combined.data)
bmesh.ops.triangulate(bm,faces=list(bm.faces))
bm.to_mesh(combined.data);bm.free()
combined.data.calc_loop_triangles();tris=len(combined.data.loop_triangles)
if tris>20000:raise RuntimeError('Triangle budget exceeded: '+str(tris))
fbx=OUT/'industrial_battery.fbx'
bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,
 apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=True,
 use_mesh_modifiers=True,mesh_smooth_type='OFF',use_tspace=True,path_mode='RELATIVE',add_leaf_bones=False,bake_anim=False)
bpy.data.objects.remove(combined,do_unlink=True)

scene.render.engine='CYCLES';scene.cycles.samples=48
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True
background=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND')
background.inputs[0].default_value=(.22,.25,.30,1);background.inputs[1].default_value=.7
scene.view_settings.view_transform='AgX'
for name,loc,power,size in [('Key',(1.5,-2,3),500,2),('Fill',(-2,-1,1.3),350,2),('Rim',(.8,1.5,2),450,1.5)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=power*.34*.34;data.shape='DISK';data.size=size*.34
    light=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(light);light.location=Vector(loc)*.34
    light.rotation_euler=(-light.location).to_track_quat('-Z','Y').to_euler()
camera=bpy.data.objects.new('Camera',bpy.data.cameras.new('Camera'));bpy.context.collection.objects.link(camera)
camera.location=Vector((1.55,-2.9,1.8))*.34;camera.rotation_euler=(-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO';camera.data.ortho_scale=.47;scene.camera=camera
scene.render.filepath=str(PREVIEW)
bpy.ops.wm.save_as_mainfile(filepath=str(MODEL))
bpy.ops.render.render(write_still=True)

provenance={'id':'industrial_battery','model_origin':'original_project_model','fbx':rel(fbx),'editable_blend':rel(MODEL),
 'source_id':'original_industrial_battery','source_url':REFERENCE,
 'reference_urls':[REFERENCE],
 'author':'Lunar Escape project - original procedural Blender asset',
 'license':'Original project asset; no downloaded third-party model or image content',
 'bounds_m':[source_dims.x,source_dims.z,source_dims.y],'triangles':tris,'materials':material_rows,
 'notes':'Newly modeled game prop, not a downloaded battery. Manufacturer service manual was consulted for separate protected terminals, fasteners and sealed case construction only. All geometry, polymer textures, rating plate, project labels and colors were created for this project. Rating text is fictional game-prop labeling; no manufacturer branding or reference images are included. Source blend retains separate editable components. Single FBX mesh with named material slots; centered bounds pivot.',
 'sha256':hashlib.sha256(fbx.read_bytes()).hexdigest()}
(OUT/'provenance.json').write_text(json.dumps(provenance,indent=2),encoding='utf-8')
(MODEL.with_suffix('.provenance.json')).write_text(json.dumps(provenance,indent=2),encoding='utf-8')
mf=OUT.parent/'material-manifest.json';manifest=json.loads(mf.read_text(encoding='utf-8'))
manifest['assets']=[a for a in manifest['assets'] if a['id']!='industrial_battery']+[provenance]
mf.write_text(json.dumps(manifest,indent=2),encoding='utf-8')
source_manifest=ROOT/'Models/LunarBase/Downloads/source-manifest.json'
record={k:v for k,v in provenance.items() if k not in ['materials','sha256','bounds_m','triangles']}
record['files']=[{'path':rel(p),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in [MODEL,fbx,*TEX.glob('*.png')]]
source=json.loads(source_manifest.read_text(encoding='utf-8'));source['assets']=[a for a in source['assets'] if a['id']!='industrial_battery']+[record]
source_manifest.write_text(json.dumps(source,indent=2),encoding='utf-8')
print('INDUSTRIAL_BATTERY_COMPLETE',json.dumps({'triangles':tris,'bounds':provenance['bounds_m'],'fbx':str(fbx)}),flush=True)
