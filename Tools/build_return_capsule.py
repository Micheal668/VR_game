"""Blender 制作三人钟形返回舱；保留可编辑源文件并导出米制网格。"""
import bpy, math, json, struct
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/_LunarEscape/Art/EarthReturn/Capsule'
SRC=ROOT/'Models/ReturnCapsule'
for p in (OUT,SRC): p.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.context.scene.unit_settings.system='METRIC'
materials={}
def mat(name,color,metal=0,rough=.4):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    materials[name]=m;return m
mat('Ceramic',(0.76,.79,.8));mat('Cushion',(.025,.031,.039),0,.72);mat('Titanium',(.28,.32,.35),.75,.23)
mat('Seals',(.012,.017,.023),0,.8);mat('Light',(.75,.87,.95));mat('Harness',(.12,.14,.16),0,.85);mat('Warning',(.9,.24,.025))
mat('Pressure glass',(.12,.23,.28),.05,.08)
def u(p):return Vector((p[0],-p[2],p[1]))
def own(o,n,m):o.name=n;o.data.materials.append(materials[m]);return o
def box(n,p,s,m,tilt=0,bevel=.03):
    bpy.ops.mesh.primitive_cube_add(size=1,location=u(p));o=own(bpy.context.object,n,m);o.dimensions=(s[0],s[2],s[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.rotation_euler.x=math.radians(tilt)
    if bevel:
        b=o.modifiers.new('Soft molded edge','BEVEL');b.width=min(bevel,min(s)*.3);b.segments=3;o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    return o
def rod(n,a,b,r,m):
    a,b=u(a),u(b);d=b-a;bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=r,depth=d.length,location=(a+b)/2)
    o=own(bpy.context.object,n,m);o.rotation_euler=d.to_track_quat('Z','Y').to_euler();return o
def ring(n,p,normal,r,t,m):
    bpy.ops.mesh.primitive_torus_add(major_segments=80,minor_segments=10,major_radius=r,minor_radius=t,location=u(p));o=own(bpy.context.object,n,m)
    o.rotation_euler=u(normal).to_track_quat('Z','Y').to_euler()
    for f in o.data.polygons:f.use_smooth=True
    return o
def lathe(n,profile,m):
    N=128;verts=[(r*math.cos(i*math.tau/N),r*math.sin(i*math.tau/N),h) for r,h in profile for i in range(N)]
    faces=[(j*N+i,j*N+(i+1)%N,((j+1)%len(profile))*N+(i+1)%N,((j+1)%len(profile))*N+i) for j in range(len(profile)) for i in range(N)]
    mesh=bpy.data.meshes.new(n);mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new(n,mesh);bpy.context.collection.objects.link(o);own(o,n,m)
    for f in mesh.polygons:f.use_smooth=True
    return o
# 压力壳有真实厚度、顶端开口和底部封闭，不用背面剔除掩盖穿帮。
shell=lathe('Bell pressure shell',[(2.23,-.1),(2.30,.35),(2.26,1.1),(2.09,1.8),(1.78,2.45),(1.18,3.04),(.68,3.33),(.68,3.15),(1.10,2.89),(1.66,2.31),(1.96,1.70),(2.12,1.04),(2.16,.35),(2.09,.03)],'Ceramic')
for idx,angle in enumerate((-27,27,-102,102)):
    a=math.radians(angle);n=Vector((math.sin(a),.38,math.cos(a))).normalized();c=Vector((math.sin(a)*1.99,1.83,math.cos(a)*1.99));r=.43 if abs(angle)<90 else .32
    cut=rod('Window cutter',c-n*.6,c+n*.6,r,'Seals');bpy.context.view_layer.objects.active=shell
    mod=shell.modifiers.new('Real pressure window','BOOLEAN');mod.operation='DIFFERENCE';mod.object=cut;bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cut,do_unlink=True)
    for depth,rad,t,m in [(-.15,r+.065,.065,'Ceramic'),(-.09,r+.013,.024,'Seals'),(.06,r+.026,.028,'Titanium')]:ring('Window %d %s'%(idx,m),c+n*depth,n,rad,t,m)
    rod('Window %d pressure glass'%idx,c-n*.025,c-n*.017,r-.007,'Pressure glass')
    for k in range(8):
        tangent=Vector((math.cos(a),0,-math.sin(a)));vertical=n.cross(tangent)
        p=c-n*.17+(tangent*math.cos(k*math.tau/8)+vertical*math.sin(k*math.tau/8))*(r+.075)
        rod('Window captive bolt',p-n*.012,p+n*.012,.012,'Titanium')
lathe('Thermal shield',[(0,-.17),(2.14,-.17),(2.23,-.07),(2.1,.02),(0,.02)],'Seals')
lathe('Upper docking tunnel',[(.68,3.2),(.68,4),(.80,4),(.80,3.2)],'Titanium')
ring('Docking capture ring',(0,3.98,0),(0,1,0),.75,.07,'Ceramic')
rod('Docked pressure vestibule ceiling',(0,4.04,0),(0,4.08,0),.70,'Ceramic')
rod('Top transfer hatch',(0,3.13,0),(0,3.20,0),.675,'Ceramic')
for side in (-1,1):rod('Hatch handrail',(side*.48,2.83,-.3),(side*.48,2.83,.3),.028,'Titanium')
for i,x in enumerate((-.96,0,.96)):
    prefix='Seat %d '%(i+1)
    box(prefix+'bucket back',(x,.91,-.84),(.73,1.20,.16),'Cushion',-24,.08)
    box(prefix+'headrest',(x,1.53,-1.09),(.48,.32,.16),'Cushion',-24,.07)
    box(prefix+'seat pan',(x,.43,-.35),(.72,.16,.73),'Cushion',12,.06)
    box(prefix+'leg support',(x,.22,.25),(.62,.12,.58),'Cushion',28,.05)
    for s in (-1,1):
        rod(prefix+'frame',(x+s*.33,.1,-.7),(x+s*.33,1.36,-1.02),.024,'Titanium')
        box(prefix+'armrest',(x+s*.40,.65,-.30),(.10,.13,.65),'Cushion',0)
        rod(prefix+'leg',(x+s*.27,.08,-.38),(x+s*.27,.43,-.38),.025,'Titanium')
        if i==1:box(prefix+'harness',(x+s*.15,1.05,-.72),(.065,.76,.022),'Harness',-24,.005)
    box(prefix+'buckle',(x,.56,-.34),(.095,.07,.035),'Titanium')
    box(prefix+'foot plate',(x,.10,.58),(.59,.06,.22),'Seals')
# 吊臂式显示屏位于座椅斜上方；Unity 在黑色面板前添加可交互屏幕。
box('Central display housing',(0,2.20,.88),(1.76,.95,.075),'Seals',-25,.04)
side_screen=box('Exterior monitor housing',(1.41,1.45,.435),(.74,.55,.06),'Seals',0,.035)
side_screen.rotation_euler.z=math.radians(42)
rod('Exterior monitor arm',(1.86,1.5,.35),(1.48,1.45,.50),.025,'Titanium')
for side in (-1,1):
    rod('Display support upper arm',(side*1.46,2.47,.45),(side*.98,2.56,.52),.033,'Titanium')
    rod('Display support hinge arm',(side*.98,2.56,.52),(side*.84,2.48,.72),.033,'Titanium')
    box('Side stowage',(side*1.63,.62,-.43),(.38,.63,.70),'Ceramic')
    for z in (-.7,-.2,.3):box('Floor seam',(0,.035,z),(3.55,.005,.012),'Titanium',bevel=0)
for k in range(12):
    a=k*math.tau/12
    rod('Upper panel seam',(math.sin(a)*1.72,2.30,math.cos(a)*1.72),(math.sin(a)*.92,3.02,math.cos(a)*.92),.009,'Titanium')
    ring('Ceiling fastener',(math.sin(a)*.91,2.97,math.cos(a)*.91),(0,1,0),.021,.006,'Titanium')
ring('Indirect cabin light',(0,2.67,0),(0,1,0),1.36,.021,'Light')
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'ThreeSeatReturnCapsule.blend'))
# 展开三角面导出实际 Blender 修改器结果，坐标已转换到 Unity 米制。
deps=bpy.context.evaluated_depsgraph_get();manifest={'source':'Models/ReturnCapsule/ThreeSeatReturnCapsule.blend','materials':[],'meshes':[]}
for name,m in materials.items():
    p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');manifest['materials'].append({'name':name,'color':list(m.diffuse_color),'metallic':p.inputs['Metallic'].default_value,'roughness':p.inputs['Roughness'].default_value})
with (OUT/'capsule-meshes.bytes').open('wb') as f:
    objs=[o for o in bpy.context.scene.objects if o.type=='MESH'];f.write(struct.pack('<i',len(objs)))
    for idx,o in enumerate(objs):
        ev=o.evaluated_get(deps);me=ev.to_mesh();me.calc_loop_triangles();verts=[];normals=[]
        for tri in me.loop_triangles:
            for li in reversed(tri.loops):
                vi=me.loops[li].vertex_index
                p=o.matrix_world@me.vertices[vi].co;n=o.matrix_world.to_3x3()@me.corner_normals[li].vector
                verts.extend((p.x,p.z,-p.y));normals.extend((n.x,n.z,-n.y))
        f.write(struct.pack('<i',len(verts)//3));f.write(struct.pack('<%df'%len(verts),*verts));f.write(struct.pack('<%df'%len(normals),*normals))
        manifest['meshes'].append({'name':o.name,'material':o.data.materials[0].name});ev.to_mesh_clear()
(OUT/'capsule.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
print('RETURN_CAPSULE_EXPORTED',len(objs),flush=True)
