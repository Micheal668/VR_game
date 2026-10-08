"""Original capsule interior and station work uniform, in metre-scale Unity coordinates.

Run with the project's installed Blender. No downloaded geometry or textures.
The capsule uses Crew Dragon's visual language, adapted to the existing lunar lander.
"""
import bpy, bmesh, json, math, sys
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/_LunarEscape/Art/LifeSupport'
SOURCE = ROOT / 'Models/LifeSupport'
for folder in (OUT, SOURCE): folder.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'; scene.unit_settings.scale_length = 1
materials = []; mats = {}; parts = []; groups = {}

def u(p): return Vector((p[0], -p[2], p[1]))
def material(name, color, metal=0, rough=.5, emission=None):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    m.node_tree.nodes.clear();bs=m.node_tree.nodes.new('ShaderNodeBsdfPrincipled');output=m.node_tree.nodes.new('ShaderNodeOutputMaterial');m.node_tree.links.new(bs.outputs['BSDF'],output.inputs['Surface']);bs.inputs['Base Color'].default_value=(*color,1)
    bs.inputs['Metallic'].default_value=metal; bs.inputs['Roughness'].default_value=rough
    if emission:
        bs.inputs['Emission Color'].default_value=(*emission,1); bs.inputs['Emission Strength'].default_value=1
    mats[name]=m; materials.append(dict(name=name,color=[*color,1],metallic=metal,roughness=rough,emission=emission or [0,0,0]))
    return m
material('LS_PearlShell',(.69,.72,.73),.15,.34)
material('LS_WhiteFabric',(.47,.50,.53),0,.84)
material('LS_Graphite',(.018,.024,.031),.28,.38)
material('LS_BrushedAlloy',(.32,.37,.40),.82,.31)
material('LS_Rubber',(.009,.011,.013),0,.92)
material('LS_SeatFabric',(.028,.042,.055),0,.8)
material('LS_Light',(.55,.74,.87),0,.25,(1.7,2.3,2.7))
material('LS_Amber',(.8,.29,.045),.05,.38)
material('LS_NavyCloth',(.026,.064,.102),0,.86)
material('LS_ClothSeam',(.08,.13,.17),0,.9)
material('LS_Skin',(.43,.255,.16),0,.68)
material('LS_Hair',(.025,.017,.013),0,.94)
material('LS_WhiteMarking',(.77,.82,.83),0,.55)

current='cabin'
def own(obj,name,mat):
    obj.name=name; obj.data.materials.append(mats[mat]); parts.append(obj); groups.setdefault(current,[]).append(obj); return obj
def box(name,p,s,mat,bevel=.01,rotation=None):
    bpy.ops.mesh.primitive_cube_add(size=1,location=u(p)); obj=own(bpy.context.object,name,mat)
    obj.dimensions=(s[0],s[2],s[1]); bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if rotation: obj.rotation_euler=rotation
    if bevel:
        mod=obj.modifiers.new('Soft manufactured edge','BEVEL'); mod.width=min(bevel,min(s)*.23); mod.segments=3
        obj.modifiers.new('Area weighted normals','WEIGHTED_NORMAL')
    return obj
def rod(name,a,b,r,mat,n=12):
    a,b=u(a),u(b); d=b-a; bpy.ops.mesh.primitive_cylinder_add(vertices=n,radius=r,depth=d.length,location=(a+b)/2)
    obj=own(bpy.context.object,name,mat); obj.rotation_euler=d.to_track_quat('Z','Y').to_euler()
    for face in obj.data.polygons: face.use_smooth=len(face.vertices)==4
    return obj
def ellipsoid(name,p,s,mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=16,radius=1,location=u(p))
    obj=own(bpy.context.object,name,mat); obj.scale=(s[0],s[2],s[1]); bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    for face in obj.data.polygons: face.use_smooth=True
    return obj
def tube(name,points,r,mat):
    curve=bpy.data.curves.new(name,'CURVE'); curve.dimensions='3D'; curve.resolution_u=2; curve.bevel_depth=r; curve.bevel_resolution=2
    spline=curve.splines.new('POLY'); spline.points.add(len(points)-1)
    for point,co in zip(spline.points,points): point.co=(*u(co),1)
    obj=bpy.data.objects.new(name,curve); scene.collection.objects.link(obj); return own(obj,name,mat)
def rounded_frame(name,p,w,h,thick,mat,corner=.11,plane='front'):
    points=[]
    for cx,cy,begin in [(w/2-corner,h/2-corner,0),(-w/2+corner,h/2-corner,90),(-w/2+corner,-h/2+corner,180),(w/2-corner,-h/2+corner,270)]:
        for k in range(9):
            a=math.radians(begin+k*90/8); xx=cx+corner*math.cos(a); yy=cy+corner*math.sin(a)
            points.append((p[0]+xx,p[1]+yy,p[2]) if plane=='front' else (p[0],p[1]+yy,p[2]+xx))
    points.append(points[0]); return tube(name,points,thick,mat)
def mesh(name,vertices,faces,mat):
    data=bpy.data.meshes.new(name); data.from_pydata([u(v) for v in vertices],[],faces); data.update()
    bm=bmesh.new(); bm.from_mesh(data); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(data); bm.free()
    obj=bpy.data.objects.new(name,data); scene.collection.objects.link(obj); return own(obj,name,mat)

# A faceted, padded pressure shell; real open spaces are left for all windows.
box('Structural keel',(0,-.1,0),(4.4,.18,4.8),'LS_Graphite')
for x in [-1.55,-.75,.05,.85,1.65]:
    for z in [-1.8,-1,-.2,.6,1.4]:
        box('Non-slip floor tile',(x,.008,z),(.77,.026,.77),'LS_SeatFabric',.014)
box('Rear pressure bulkhead',(0,1.5,-2.36),(4.35,3.03,.12),'LS_PearlShell',.08)
rounded_frame('Rear hatch pressure seal',(0,1.52,-2.27),1.27,2.13,.055,'LS_Rubber',.2)
box('Rear hatch leaf',(0,1.52,-2.28),(1.19,2.05,.045),'LS_WhiteFabric',.15)
for sign in [-1,1]:
    rod('Rear hatch pull grip',(sign*.46,1.1,-2.19),(sign*.46,1.7,-2.19),.026,'LS_BrushedAlloy')
    # Panels below the side portholes, upper coves, and no wall behind the aperture.
    box('Lower side liner',(sign*2.17,.45,-.05),(.12,.88,4.65),'LS_PearlShell',.06)
    box('Upper side liner',(sign*2.17,2.74,-.05),(.12,.54,4.65),'LS_WhiteFabric',.045)
    for z in [-1.82,.02,1.88]:
        box('Window structural pillar',(sign*2.15,1.72,z),(.14,1.75,.18),'LS_PearlShell',.055)
    for z in [-.94,.94]:
        rounded_frame('Side window seal',(sign*2.11,1.72,z),1.64,1.53,.033,'LS_Rubber',.2,'side')
        rounded_frame('Side window rim',(sign*2.10,1.72,z),1.72,1.61,.048,'LS_BrushedAlloy',.23,'side')
    # Slanted roof cove made as an extrusion with actual thickness.
    for z in [-1.76,-.59,.59,1.76]:
        vertices=[(sign*2.16,2.46,z-.54),(sign*1.54,3.08,z-.54),(sign*1.54,3.08,z+.54),(sign*2.16,2.46,z+.54),
                  (sign*2.23,2.5,z-.54),(sign*1.58,3.15,z-.54),(sign*1.58,3.15,z+.54),(sign*2.23,2.5,z+.54)]
        mesh('Segmented shoulder liner',vertices,[(0,1,2,3),(4,7,6,5),(0,4,5,1),(3,2,6,7),(1,5,6,2),(0,3,7,4)],'LS_PearlShell')
    rod('Overhead light rail',(sign*1.37,3.025,-2.13),(sign*1.37,3.025,2.07),.021,'LS_Light',16)
    for z in [-1.6,-.7,.2,1.1]:
        rod('Cabin hand rail',(sign*1.89,2.43,z),(sign*1.89,2.43,z+.43),.021,'LS_BrushedAlloy')
        for end in [z,z+.43]: rod('Hand rail stand-off',(sign*1.89,2.43,end),(sign*2.07,2.53,end),.023,'LS_Graphite')
for x in [-.96,0,.96]: box('Roof quilt panel',(x,3.1,0),(.92,.12,4.54),'LS_WhiteFabric',.045)
for z in [-2.14,-1.03,.11,1.25,2.15]: box('Roof rib',(0,3.055,z),(2.85,.08,.047),'LS_BrushedAlloy')
box('Forward lower bulkhead',(0,.40,2.31),(4.32,.80,.12),'LS_PearlShell',.05)
box('Forward header',(0,2.99,2.31),(3.1,.31,.12),'LS_PearlShell',.07)
for side in [-1,1]: box('Forward corner pillar',(side*2.02,1.77,2.28),(.25,2.30,.18),'LS_PearlShell',.085)
rounded_frame('Forward viewport seal',(0,2.37,2.25),2.95,.80,.035,'LS_Rubber',.2)
rounded_frame('Forward viewport surround',(0,2.37,2.26),3.05,.90,.052,'LS_PearlShell',.23)
# Suspended three-screen black console, with mechanical controls underneath.
for x in [-1.5,1.5]:
    rod('Dashboard lower support',(x,.40,1.89),(x,1.13,1.43),.045,'LS_BrushedAlloy')
    rod('Dashboard overhead suspension',(x,2.89,1.94),(x,2.07,1.42),.032,'LS_Graphite')
screens=[('Operations',(-1.38,1.57,1.40),(1.06,.92)),('Navigation',(-.05,1.57,1.40),(1.43,.92)),('Resources',(1.23,1.57,1.40),(.84,.92))]
for label,p,(w,h) in screens:
    box(label+' monitor housing',(p[0],p[1],p[2]+.055),(w+.11,h+.11,.13),'LS_Graphite',.045)
    box(label+' monitor gasket',(p[0],p[1],p[2]-.014),(w+.026,h+.026,.01),'LS_Rubber',.02)
    for sx in [-1,1]:
        for sy in [-1,1]: rod('Display bezel screw',(p[0]+sx*(w/2+.027),p[1]+sy*(h/2+.027),p[2]-.017),(p[0]+sx*(w/2+.027),p[1]+sy*(h/2+.027),p[2]-.009),.012,'LS_BrushedAlloy',8)
box('Lower switch console',(0,.985,1.36),(3.55,.19,.30),'LS_Graphite',.04)
for x in [-1.55,-1.18,-.81,.32,.64,.96,1.28,1.60]:
    box('Mechanical control socket',(x,.98,1.197),(.24,.11,.033),'LS_BrushedAlloy',.01)
    box('Mechanical control face',(x,.98,1.173),(.204,.084,.028),'LS_Graphite',.009)
    rod('Switch amber telltale',(x+.08,1.045,1.188),(x+.08,1.045,1.17),.012,'LS_Amber')
# Two identifiable restrained crew stations, placed behind the player's tracking origin.
for x in [-.90,1.02]:
    box('Crew seat pedestal',(x,.23,-.76),(.7,.46,.76),'LS_Graphite',.045)
    box('Crew seat cushion',(x,.48,-.78),(.72,.14,.74),'LS_SeatFabric',.085)
    box('Crew back shell',(x,1.01,-1.20),(.87,1.33,.19),'LS_PearlShell',.075)
    box('Crew back cushion',(x,1.03,-1.075),(.69,1.16,.10),'LS_SeatFabric',.045)
    box('Crew head restraint',(x,1.75,-1.16),(.50,.40,.19),'LS_SeatFabric',.08)
    for side in [-1,1]:
        box('Seat shoulder wing',(x+side*.42,1.43,-1.035),(.13,.58,.35),'LS_Graphite',.045)
        box('Seat arm support',(x+side*.48,.91,-.66),(.15,.095,.82),'LS_SeatFabric',.035)
        tube('Crew harness',[(x+side*.27,1.62,-1.004),(x+side*.19,1.14,-.973),(x+side*.05,.63,-.35)],.026,'LS_Rubber')
    box('Harness buckle',(x,.64,-.33),(.16,.10,.04),'LS_BrushedAlloy',.01)
    for i in range(5): box('Seat fabric stitched channel',(x,.83+i*.13,-1.013),(.61,.008,.008),'LS_ClothSeam',.001)
for x in [-1.63,1.70]:
    box('Equipment locker',(x,.68,-1.9),(.72,1.30,.38),'LS_WhiteFabric',.045)
    for y in [.35,1.03]:
        box('Locker recessed handle',(x,y,-1.69),(.25,.055,.016),'LS_Graphite',.01)
    for dx in [-.23,.23]: rod('Locker restraint',(x+dx,.15,-1.67),(x+dx,1.19,-1.67),.018,'LS_ClothSeam')

# Station clothing: fitted navy workwear, boots, exposed hands, head and hair.
# Pieces keep explicit anatomical groups for editor-side rigid skin binding.
current='uniform'
def uniform_part(name,fn,*args,**kwargs):
    before=len(groups.get('uniform',[])); obj=fn(name,*args,**kwargs); obj['bodyPart']=name.split('_')[0]; return obj
uniform_part('Hips_pelvis',ellipsoid,(0,.86,0),(.20,.20,.105),'LS_NavyCloth')
uniform_part('Chest_workshirt',ellipsoid,(0,1.20,-.015),(.238,.31,.13),'LS_NavyCloth')
box('Chest_shirt_placket',(0,1.205,.115),(.025,.39,.012),'LS_ClothSeam',.004)
box('Chest_name_patch',(-.105,1.34,.111),(.12,.036,.012),'LS_WhiteMarking',.004)
box('Chest_service_patch',(.119,1.32,.113),(.058,.061,.014),'LS_Amber',.007)
for x in [-.115,.115]:
    box('Chest_chest_pocket',(x,1.17,.111),(.126,.113,.016),'LS_ClothSeam',.018)
    box('Chest_pocket_flap',(x,1.224,.124),(.135,.033,.015),'LS_NavyCloth',.008)
rod('Hips_work_belt',(-.17,.94,.104),(.17,.94,.104),.024,'LS_Rubber',16)
box('Hips_belt_buckle',(0,.94,.130),(.06,.045,.017),'LS_BrushedAlloy',.004)
for side,name in [(-1,'Left'),(1,'Right')]:
    uniform_part('Hips_'+name+'_trouser',ellipsoid,(side*.113,.46,-.015),(.106,.39,.11),'LS_NavyCloth')
    box('Hips_'+name+'_cargo_pocket',(side*.196,.59,.016),(.047,.17,.128),'LS_ClothSeam',.015)
    ellipsoid('Hips_'+name+'_shoe',(side*.113,.072,.065),(.113,.067,.21),'LS_Rubber')
    box('Hips_'+name+'_sole',(side*.113,.026,.059),(.218,.029,.397),'LS_Graphite',.009)
    upper=(side*.248,1.35,-.025); elbow=(side*.321,1.145,.014); wrist=(side*.331,1.014,.135)
    rod(name+'Upper_sleeve',upper,elbow,.077,'LS_NavyCloth',20)
    ellipsoid(name+'Upper_shoulder',upper,(.086,.092,.085),'LS_NavyCloth')
    ellipsoid(name+'Fore_elbow',elbow,(.071,.074,.072),'LS_NavyCloth')
    rod(name+'Fore_sleeve',elbow,wrist,.063,'LS_NavyCloth',20)
    rod(name+'Fore_cuff',(side*.330,1.030,.119),wrist,.064,'LS_ClothSeam',20)
    ellipsoid(name+'Hand_palm',(side*.331,.991,.168),(.049,.026,.064),'LS_Skin')
    for finger in range(4):
        fx=side*(.301+finger*.018)
        rod(name+'Hand_finger',(fx,.981,.195),(fx,.956,.235+(1-abs(1.5-finger)/2)*.016),.009,'LS_Skin',10)
    rod(name+'Hand_thumb',(side*.294,.992,.168),(side*.280,.960,.207),.014,'LS_Skin',12)
rod('Head_neck',(0,1.42,-.005),(0,1.53,-.005),.066,'LS_Skin',20)
ellipsoid('Head_skull',(0,1.645,-.006),(.106,.151,.105),'LS_Skin')
ellipsoid('Head_jaw',(0,1.565,.025),(.08,.073,.073),'LS_Skin')
ellipsoid('Head_hair',(0,1.728,-.029),(.108,.072,.105),'LS_Hair')
for side in [-1,1]:
    ellipsoid('Head_ear',(side*.104,1.634,-.003),(.018,.035,.025),'LS_Skin')
    ellipsoid('Head_eye',(side*.041,1.653,.087),(.021,.009,.010),'LS_WhiteMarking')
    ellipsoid('Head_pupil',(side*.040,1.654,.096),(.007,.007,.005),'LS_Hair')
    rod('Head_eyebrow',(side*.023,1.677,.091),(side*.061,1.674,.083),.005,'LS_Hair',10)
ellipsoid('Head_nose',(0,1.623,.099),(.018,.032,.021),'LS_Skin')
rod('Head_mouth',(-.025,1.587,.089),(.025,1.587,.089),.003,'LS_Hair',10)

anchors={'Origin':[0,0,0],'AxisX':[1,0,0],'AxisY':[0,1,0],'AxisZ':[0,0,1]}
manifest=dict(materials=materials,models=[],references=[
    {'url':'https://www.nasa.gov/commercial-crew-program-press-kit/','use':'Crew Dragon interior, touchscreen controls and window layout; visual reference only.'},
    {'url':'https://science.nasa.gov/moon/facts/','use':'Lunar day/night thermal extremes; game timing is deliberately compressed.'}],
    design='Original game capsule interior inspired by Crew Dragon; retains existing lunar lander dimensions and visible windows. Original station work uniform. No third-party meshes or textures.')

def export_group(identifier):
    source=groups[identifier]; merged={}
    for original in source:
        obj=original.copy();obj.data=original.data.copy();scene.collection.objects.link(obj)
        bone=original.name.split('_')[0] if identifier=='uniform' else 'Cabin'
        merged.setdefault((bone,original.data.materials[0].name),[]).append(obj)
    baked=[]
    for (bone,mat),objects in merged.items():
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects:obj.select_set(True)
        bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.convert(target='MESH')
        if len(objects)>1:bpy.ops.object.join()
        obj=bpy.context.object;obj.name=bone+'__'+mat;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);obj.data.calc_loop_triangles();baked.append(obj)
    empties=[]
    for name,p in anchors.items():
        obj=bpy.data.objects.new('Anchor_'+name,None);scene.collection.objects.link(obj);obj.location=u(p);empties.append(obj)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in baked+empties:obj.select_set(True)
    bpy.context.view_layer.objects.active=baked[0]
    filename='DragonInspiredCabin.fbx' if identifier=='cabin' else 'StationWorkUniform.fbx'
    bpy.ops.export_scene.fbx(filepath=str(OUT/filename),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=False,use_mesh_modifiers=True,mesh_smooth_type='OFF',add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
    tris=sum(len(obj.data.loop_triangles) for obj in baked)
    manifest['models'].append(dict(id=identifier,fbx=filename,meshCount=len(baked),triangleCount=tris))
    print('LIFE_ART_MODEL',identifier,len(baked),tris,flush=True)
    for obj in baked+empties:bpy.data.objects.remove(obj,do_unlink=True)

export_group('cabin')
if '--cabin-only' in sys.argv:
    previous=json.loads((OUT/'life-art-manifest.json').read_text(encoding='utf-8'))
    manifest['models'].extend(model for model in previous['models'] if model['id']=='uniform')
else:
    export_group('uniform')
(OUT/'life-art-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
# Uniform is stored off to the side in the editable source, outside the interior preview.
for obj in groups['uniform']:obj.location.x+=6
world=bpy.data.worlds.new('Interior studio');world.use_nodes=True;scene.world=world
world.node_tree.nodes.clear();background=world.node_tree.nodes.new('ShaderNodeBackground');world_output=world.node_tree.nodes.new('ShaderNodeOutputWorld');world.node_tree.links.new(background.outputs[0],world_output.inputs[0])
background.inputs[0].default_value=(.07,.09,.13,1);background.inputs[1].default_value=.3
def area(name,p,target,power,size):
    data=bpy.data.lights.new(name,'AREA');obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=u(p);obj.rotation_euler=(u(target)-obj.location).to_track_quat('-Z','Y').to_euler();data.energy=power;data.size=size
area('Interior overhead preview',(0,2.95,-.3),(0,1,0),220,3)
area('Viewport bounce preview',(0,1.6,2.7),(0,1,-1),140,2)
data=bpy.data.cameras.new('Interior preview');cam=bpy.data.objects.new('Interior preview',data);scene.collection.objects.link(cam)
cam.location=u((-.90,1.68,-.62));cam.rotation_euler=(u((-.16,1.60,1.35))-cam.location).to_track_quat('-Z','Y').to_euler();data.lens=21
scene.camera=cam;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=1600;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX'
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'LifeSupport_InteriorAndUniform.blend'))
scene.render.filepath=str(SOURCE/'Cabin_Structure.png');bpy.ops.render.render(write_still=True)
print('LIFE_ART_BUILD_COMPLETE',flush=True)
