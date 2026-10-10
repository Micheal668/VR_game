"""Original stylized lunar interiors. Metres in Unity XYZ; export frame is explicit.
Run C:/Tools/blender.exe -b -t 8 --python Tools/build_station_expansion.py
"""
import bpy, math, json
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/_LunarEscape/Art/StationExpansion'
SRC=ROOT/'Models/StationExpansion'
OUT.mkdir(parents=True,exist_ok=True); SRC.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene; scene.unit_settings.system='METRIC'
mats={}; objects=[]; colliders=[]; group='Lobby'
manifest={'materials':[], 'colliders':[], 'rooms':[]}
def u(p): return Vector((p[0],-p[2],p[1]))
def material(name,color,metal=.1,rough=.55,emit=0):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    m.node_tree.nodes.clear();bs=m.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
    output=m.node_tree.nodes.new('ShaderNodeOutputMaterial');m.node_tree.links.new(bs.outputs['BSDF'],output.inputs['Surface'])
    bs.inputs['Base Color'].default_value=(*color,1)
    bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
    bs.inputs['Emission Color'].default_value=(*color,1);bs.inputs['Emission Strength'].default_value=emit
    mats[name]=m;manifest['materials'].append({'name':name,'color':[*color,1],'metallic':metal,'roughness':rough,'emission':emit})
for args in [('EX_Shell',(.72,.78,.80)),('EX_White',(.87,.90,.90)),('EX_Blue',(.06,.22,.34)),
             ('EX_Trim',(.035,.065,.09),.45,.48),('EX_Floor',(.17,.23,.26),.1,.72),
             ('EX_Copper',(.9,.40,.06),.25,.4),('EX_Screen',(.008,.026,.04),.15,.3),
             ('EX_Light',(.65,.9,1),0,.35,2),('EX_Red',(.65,.018,.008),0,.45,.25),
             ('EX_Fabric',(.12,.26,.32),0,.85),('EX_Pillow',(.65,.72,.73),0,.9),
             ('EX_Solar',(.008,.045,.14),.35,.25),('EX_Green',(.03,.5,.25),0,.4,.2)]:material(*args)
def own(o,name,mat):
    o.name=group+'__'+name;o.data.materials.append(mats[mat]);objects.append((group,o));return o
def box(name,p,s,mat='EX_Shell',bevel=.04,solid=False,floor=False):
    bpy.ops.mesh.primitive_cube_add(size=1,location=u(p));o=own(bpy.context.object,name,mat)
    o.dimensions=(s[0],s[2],s[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Soft manufactured edges','BEVEL');mod.width=min(bevel,min(s)*.25);mod.segments=3
        mod=o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');mod.keep_sharp=True
    if solid:colliders.append({'name':name,'position':list(p),'size':list(s),'floor':floor})
    return o
def rod(name,a,b,r,mat='EX_Trim',vertices=24):
    a,b=u(a),u(b);d=b-a;bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=d.length,location=(a+b)/2)
    o=own(bpy.context.object,name,mat);o.rotation_euler=d.to_track_quat('Z','Y').to_euler()
    m=o.modifiers.new('Round edge','BEVEL');m.width=min(.014,r*.2);m.segments=2
    m=o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');return o
def ring(name,p,r,t,mat='EX_Copper',normal=(0,1,0)):
    bpy.ops.mesh.primitive_torus_add(major_segments=48,minor_segments=8,major_radius=r,minor_radius=t,location=u(p))
    o=own(bpy.context.object,name,mat);o.rotation_euler=u(normal).to_track_quat('Z','Y').to_euler();return o
def wall_x(name,x,z0,z1):
    box(name,(x,1.65,(z0+z1)/2),(.2,3.3,z1-z0),solid=True)
    box(name+' blue skirting',(x, .39,(z0+z1)/2),(.225,.65,z1-z0-.04),'EX_Blue')
    box(name+' crown',(x,3.14,(z0+z1)/2),(.24,.18,z1-z0),'EX_Trim')
    for z in [z0+.16,z1-.16]:box(name+' jamb',(x,1.65,z),(.27,3.05,.075),'EX_White')
def wall_z(name,z,x0,x1):
    box(name,((x0+x1)/2,1.65,z),(x1-x0,3.3,.2),solid=True)
    box(name+' blue skirting',((x0+x1)/2,.39,z),(x1-x0-.04,.65,.225),'EX_Blue')
    box(name+' crown',((x0+x1)/2,3.14,z),(x1-x0,.18,.24),'EX_Trim')
    for x in [x0+.16,x1-.16]:box(name+' jamb',(x,1.65,z),(.075,3.05,.27),'EX_White')
def deck(name,x0,x1,z0,z1):
    cx=(x0+x1)/2;cz=(z0+z1)/2
    box(name+' pressure deck',(cx,-.13,cz),(x1-x0,.26,z1-z0),'EX_Floor',.025,True,True)
    box(name+' roof',(cx,3.44,cz),(x1-x0,.22,z1-z0),'EX_Shell',.05,True)
    # Thin dark seams are flush under the collision plane: loose tools rest above them.
    for i in range(1,int(x1-x0)):
        box(name+' floor seam',(x0+i,.002,cz),(.012,.002,z1-z0-.25),'EX_Trim',0)
    for i in range(1,int(z1-z0)):
        box(name+' floor seam',(cx,.002,z0+i),(x1-x0-.25,.002,.012),'EX_Trim',0)
    for z in [z0+.45,z1-.45]:
        box(name+' ceiling light channel',(cx,3.285,z),(x1-x0-.9,.09,.17),'EX_Trim')
        box(name+' ceiling diffuser',(cx,3.23,z),(x1-x0-1,.03,.08),'EX_Light')
    manifest['rooms'].append({'name':name,'position':[cx,1.65,cz],'size':[x1-x0,3.3,z1-z0]})
def lintel_x(x,z,width):box('Portal lintel',(x,2.99,z),(.24,.64,width),'EX_White',.04,True)
def lintel_z(z,x,width):box('Portal lintel',(x,2.99,z),(width,.64,.24),'EX_White',.04,True)

group='Lobby';deck('Main lobby',-4,4,-4,4)
wall_z('Lobby south',-4,-4,4);wall_z('Lobby north',4,-4,4)
wall_x('Lobby west south',-4,-4,.7);wall_x('Lobby west north',-4,2.7,4);lintel_x(-4,1.7,2)
wall_x('Lobby east south',4,-4,.8);wall_x('Lobby east north',4,2.6,4);lintel_x(4,1.7,1.8)
# Architectural panels stay behind the established UI and airlock controls.
for z in [-3,-1,1,3]:
    box('Upper cross beam',(0,3.26,z),(7.75,.15,.14),'EX_White')
for x in [-3,-1,1,3]:
    box('North inset panel',(x,1.9,3.87),(1.85,2.02,.045),'EX_White')
    box('South inset panel',(x,1.9,-3.87),(1.85,2.02,.045),'EX_White')
box('Wall maintenance desk',(-1.1,.92,-3.32),(1.65,.12,.68),'EX_White',.04,True)
for x in [-1.78,-.42]:box('Maintenance desk leg',(x,.44,-3.32),(.10,.88,.55),'EX_Blue',.025,True)
box('Wrench placement mat',(-1.1,.988,-3.30),(.86,.014,.43),'EX_Blue',.025)
# An actual circular top (not a square collider hidden under a round visual).
group='SupplyTable'
rod('Pedestal',(0,.08,0),(0,.88,0),.53,'EX_Blue',48)
rod('Foot',(0,.02,0),(0,.10,0),.86,'EX_Trim',64)
rod('Round tabletop',(0,.87,0),(0,1.00,0),1.50,'EX_White',96)
ring('Rounded safety lip',(0,1.01,0),1.46,.028,'EX_Copper')
rod('Inset work surface',(0,1.0,0),(0,1.009,0),1.35,'EX_Blue',96)
ring('Center inlay',(0,1.012,0),.55,.006,'EX_White')
for i in range(12):
    a=i*math.tau/12
    box('Supply position marker',(math.cos(a)*1.21,1.014,math.sin(a)*1.21),(.10,.006,.035),'EX_Copper',0)

group='ExitAirlock';deck('Exit airlock',4.0,8.1,.6,2.8)
wall_z('Exit airlock south',.6,4.0,8.1);wall_z('Exit airlock north',2.8,4.0,8.1)
for x in [4.3,6.0,7.8]:
    for z in [.75,2.65]:box('Exit portal rib',(x,1.6,z),(.12,3.2,.13),'EX_Blue')

group='Corridor';deck('Rear corridor',-11.1,-4,.5,2.9)
wall_z('Corridor lab left',.5,-11.1,-8.35);wall_z('Corridor lab right',.5,-6.65,-4);lintel_z(.5,-7.5,1.7)
wall_z('Corridor bedroom left',2.9,-11.1,-8.35);wall_z('Corridor bedroom right',2.9,-6.65,-4);lintel_z(2.9,-7.5,1.7)
for x in [-5,-9.7]:
    for z in [.65,2.75]:box('Portal rib',(x,1.58,z),(.14,3.14,.14),'EX_Blue')
    box('Portal overhead',(x,3.14,1.7),(.14,.14,2.12),'EX_Blue')
for z in [.8,2.6]:box('Corridor floor path',(-7.55,.006,z),(6.65,.008,.045),'EX_Copper',0)

group='Bedroom';deck('Crew quarters',-10.8,-4.8,2.9,7.6)
wall_x('Bedroom west',-10.8,2.9,7.6);wall_x('Bedroom east',-4.8,2.9,7.6);wall_z('Bedroom headwall',7.6,-10.8,-4.8)
for index,x in enumerate([-10.03,-5.57]):
    box('Bed base '+str(index),(x,.29,6.08),(1.30,.52,2.4),'EX_Blue',.10,True)
    box('Mattress '+str(index),(x,.62,6.08),(1.24,.22,2.28),'EX_Pillow',.1)
    box('Blanket '+str(index),(x,.76,5.68),(1.24,.10,1.42),'EX_Fabric',.05)
    for dx in [-.5,.5]:box('Blanket seam',(x+dx,.815,5.68),(.015,.005,1.32),'EX_White',0)
    box('Pillow '+str(index),(x,.81,6.90),(.91,.18,.38),'EX_Pillow',.09)
    box('Headboard '+str(index),(x,.84,7.41),(1.37,1.30,.10),'EX_White',.04,True)
    nx=x+1.10 if index==0 else x-1.10
    box('Bedside cabinet '+str(index),(nx,.43,6.88),(.52,.86,.70),'EX_Shell',.055,True)
    box('Drawer front '+str(index),(nx,.48,6.515),(.46,.28,.025),'EX_Blue')
    box('Drawer pull '+str(index),(nx,.51,6.49),(.18,.025,.02),'EX_Copper')
    # Photo content is intentionally blank; a Unity surface is supplied for later replacement.
    box('Photo frame backing '+str(index),(nx,1.09,6.87),(.32,.36,.04),'EX_Trim')
    box('Photo placeholder '+str(index),(nx,1.09,6.842),(.27,.30,.01),'EX_Shell',0)
for x in [-10.03,-5.57]:
    box('Wall picture frame',(x,2.2,7.45),(1.10,.76,.055),'EX_Trim')
    box('Wall picture placeholder',(x,2.2,7.413),(1.01,.67,.01),'EX_Blue',0)
box('Quarters low storage',(-10.52,.48,3.75),(.46,.96,1.35),'EX_Shell',.06,True)

group='Laboratory';deck('Research laboratory',-10.8,-4.8,-4.5,.5)
wall_x('Lab west',-10.8,-4.5,.5);wall_x('Lab east',-4.8,-4.5,.5);wall_z('Lab south',-4.5,-10.8,-4.8)
box('Science bench',(-8,.89,-3.92),(5.15,.12,.95),'EX_White',.035,True)
for x in [-10,-8.1,-6.15]:box('Bench drawer cabinet',(x,.42,-3.95),(1.55,.84,.78),'EX_Blue',.045,True)
for x in [-10,-8.1,-6.15]:
    for y in [.28,.61]:
        box('Lab drawer',(x,y,-3.535),(1.43,.25,.025),'EX_Shell')
        box('Lab drawer pull',(x,y+.035,-3.51),(.24,.022,.024),'EX_Trim')
# Microscope: base, angled upright, stage, objective turret and eyepiece.
box('Microscope base',(-10,.99,-3.9),(.48,.06,.36),'EX_Trim')
rod('Microscope arm',(-10.12,1.04,-3.92),(-10.12,1.54,-4.02),.045,'EX_White')
box('Microscope stage',(-10.0,1.26,-3.86),(.28,.035,.25),'EX_Trim')
rod('Microscope head',(-10.02,1.48,-3.82),(-10.02,1.69,-3.98),.055,'EX_White')
rod('Microscope eyepiece',(-10.02,1.68,-3.98),(-10.02,1.79,-4.06),.036,'EX_Trim')
for dx in [-.045,.045]:rod('Microscope objective',(-10.02+dx,1.5,-3.82),(-10.02+dx,1.33,-3.82),.018,'EX_Copper')
# Instrument chamber with ribbed side blocks and front display.
box('Sample analyzer',(-8.55,1.27,-3.95),(.70,.60,.60),'EX_Shell',.07)
box('Analyzer window',(-8.55,1.31,-3.64),(.5,.35,.025),'EX_Screen')
for x in [-8.86,-8.24]:
    for i in range(5):box('Analyzer vent',(x,1.1+i*.07,-3.63),(.08,.019,.035),'EX_Trim',.004)
# Console faces north, reachable from the open lab floor.
box('Lab terminal plinth',(-5.4,.60,-1.35),(.70,1.2,.7),'EX_Blue',.07,True)
box('Lab terminal worktop',(-5.7,1.16,-1.5),(1.55,.12,1.15),'EX_White',.05,True)
box('Lab display bezel',(-5.65,1.78,-1.98),(1.43,.97,.12),'EX_Trim')
box('Lab display glass',(-5.65,1.78,-1.906),(1.26,.80,.02),'EX_Screen')
box('Sample storage shelf',(-10.4,1.1,-1.1),(.6,.13,2.2),'EX_White',.03,True)
box('Sample sorting table',(-7.9,.93,-1.55),(2.2,.12,1.4),'EX_White',.04,True)
box('Sample sorting pedestal',(-7.9,.42,-1.55),(.9,.84,.65),'EX_Blue',.05,True)

group='Utilities';deck('Life support engineering',-16,-11.1,-1.25,4.65)
wall_z('Engineering south',-1.25,-16,-11.1);wall_z('Engineering north',4.65,-16,-11.1)
wall_x('Engineering east south',-11.1,-1.25,.7);wall_x('Engineering east north',-11.1,2.7,4.65);lintel_x(-11.1,1.7,2)
wall_x('Engineering window south pier',-16,-1.25,-.05);wall_x('Engineering window north pier',-16,3.5,4.65)
box('Engineering window sill',(-16,.51,1.725),(.2,1.02,3.55),'EX_Blue',.04,True)
box('Engineering window lintel',(-16,3.10,1.725),(.2,.4,3.55),'EX_Shell',.04,True)
for z in [-.06,3.51]:box('Window metal upright',(-15.88,1.97,z),(.13,1.96,.10),'EX_Trim')
for y in [1.04,2.91]:box('Window metal horizontal',(-15.88,y,1.725),(.13,.11,3.70),'EX_Trim')
# Transparent pane is added by Unity so URP transparency is controlled explicitly.
box('Oxygen generator cabinet',(-13.1,1.13,-.70),(2.35,2.26,.83),'EX_White',.10,True)
box('Generator upper fascia',(-13.1,2.06,-.259),(2.20,.28,.05),'EX_Blue')
box('Generator lower fascia',(-13.1,.30,-.259),(2.20,.40,.05),'EX_Blue')
for x in [-13.83,-13.1,-12.37]:
    rod('Oxygen sieve tank',(x,.55,-.15),(x,1.75,-.15),.20,'EX_Blue',32)
    ring('Tank lower clamp',(x,.69,-.15),.205,.022,'EX_Copper')
    ring('Tank upper clamp',(x,1.61,-.15),.205,.022,'EX_White')
    rod('Oxygen feed pipe',(x,1.8,-.15),(x,2.28,-.15),.045,'EX_Trim')
rod('Generator header pipe',(-14.07,2.28,-.15),(-12.15,2.28,-.15),.045,'EX_Trim')
box('Oxygen service port backing',(-13.1,1.28,.095),(.36,.36,.10),'EX_Trim')
box('Power distribution backing',(-13.40,1.51,4.38),(2.70,1.76,.20),'EX_Trim',.06,True)
box('Power service lower console',(-13.40,.78,4.13),(2.70,.15,.65),'EX_White',.04,True)
for x in [-14.55,-12.25]:
    for y in [1.02,2.07]:box('Power cabinet captive bolt',(x,y,4.258),(.045,.045,.015),'EX_Copper',.008)
# Exterior arrays visible through the engineering window.
group='SolarArray'
for z in [-1.5,4.5]:
    for x in [-20,-23.8]:
        box('Solar array backing',(x,1.1,z),(3.5,.14,4.5),'EX_Trim')
        for ix in range(7):
            for iz in range(9):
                box('Photovoltaic cell',(x-1.5+ix*.5,1.182,z-2+iz*.5),(.47,.015,.47),'EX_Solar',.004)
        for xx in [x-1.2,x+1.2]:
            for zz in [z-1.6,z+1.6]:rod('Solar support',(xx,-.3,zz),(xx,1.05,zz),.05,'EX_White')
        for zz in [z-2.28,z+2.28]:box('Array frame',(x,1.20,zz),(3.64,.06,.06),'EX_White',.009)

manifest['colliders']=colliders
# Save authoring objects, then make separately merged export copies by room/material.
scene.world.color=(.1,.1,.1)
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'StationExpansion.blend'))
export=[];parts={}
for grp,obj in objects:parts.setdefault((grp,obj.data.materials[0].name),[]).append(obj)
for (grp,mat),originals in parts.items():
    bpy.ops.object.select_all(action='DESELECT');copies=[]
    for obj in originals:
        cp=obj.copy();cp.data=obj.data.copy();scene.collection.objects.link(cp);cp.select_set(True);copies.append(cp)
    bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.convert(target='MESH')
    if len(copies)>1:bpy.ops.object.join()
    mesh=bpy.context.object;mesh.name=grp+'__'+mat;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);export.append(mesh)
for name,p in [('Origin',(0,0,0)),('AxisX',(1,0,0)),('AxisY',(0,1,0)),('AxisZ',(0,0,1))]:
    obj=bpy.data.objects.new('Anchor_'+name,None);scene.collection.objects.link(obj);obj.location=u(p);export.append(obj)
bpy.ops.object.select_all(action='DESELECT')
for obj in export:obj.select_set(True)
bpy.context.view_layer.objects.active=export[0]
bpy.ops.export_scene.fbx(filepath=str(OUT/'StationExpansion.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=False,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
manifest['meshCount']=len(export)-4
manifest['editableObjects']=len(objects)
(OUT/'expansion-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
print('STATION_EXPANSION_EXPORTED '+str(manifest['meshCount'])+' meshes / '+str(len(objects))+' authoring objects',flush=True)
