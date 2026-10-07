"""Build an editable reference-guided Apollo LM study from the user's supplied GLB.
Run: C:/Tools/blender.exe -b -t 8 --python Tools/build_apollo_blender.py
The supplied source is read only. Front is +Y, Z is up; units are approximate metres.
"""
import bpy, math, json, hashlib
import numpy as np
from pathlib import Path
from mathutils import Vector, Matrix

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Models/ApolloLander'
SOURCE=Path('C:/Users/MichealC/OneDrive/Desktop/VR项目/VR项目')
GLB=SOURCE/'e5ab71481dd02a14c5a071bedbc0191d.glb'
OUT.mkdir(parents=True,exist_ok=True)
(OUT/'Previews').mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1

def collection(name):
    c=bpy.data.collections.new(name);scene.collection.children.link(c);return c
basecol=collection('01 • 原模型整理 | Supplied structure')
frontcol=collection('02 • 舱体与舷窗 | Cabin panels')
detailcol=collection('03 • 舱门平台舷梯 | EVA access')
hardwarecol=collection('04 • 紧固件与标识 | Hardware')
refcol=collection('90 • 照片参考（默认隐藏） | References')
studio=collection('99 • 灯光相机 | Studio')
current=frontcol
def move(o,col=None):
    for c in list(o.users_collection):c.objects.unlink(o)
    (col or current).objects.link(o);return o
def mat(name,color,metal=0,rough=.45):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough;return m
silver=mat('铝合金 • Satin aluminium',(.47,.51,.53),.75,.34)
ivory=mat('浅色隔热板 • Warm aluminium',(.65,.64,.57),.6,.38)
black=mat('黑色隔热板 • Charcoal thermal panels',(.022,.029,.033),.32,.5)
rubber=mat('密封圈 • Seals',(.008,.012,.016),.05,.67)
glass=mat('舷窗 • Blue smoked glass',(.018,.067,.092),.72,.17)
gold=mat('金色隔热箔 • Gold MLI',(.72,.34,.048),.88,.28)
white=mat('标识 • Warm white',(.87,.87,.79),.1,.52)
red=mat('标识 • Red',(.52,.015,.025),.1,.53)
blue=mat('标识 • Navy',(.016,.035,.12),.1,.5)
# Fine foil creases supplement the retained original material on newly built pieces.
n=gold.node_tree.nodes.new('ShaderNodeTexNoise');n.inputs['Scale'].default_value=105;n.inputs['Detail'].default_value=3
bump=gold.node_tree.nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.28;bump.inputs['Distance'].default_value=.025
gold.node_tree.links.new(n.outputs['Fac'],bump.inputs['Height']);gold.node_tree.links.new(bump.outputs['Normal'],gold.node_tree.nodes['Principled BSDF'].inputs['Normal'])

def mesh(name,verts,faces,material,col=None):
    m=bpy.data.meshes.new(name);m.from_pydata(verts,[],faces);m.update()
    o=bpy.data.objects.new(name,m);(col or current).objects.link(o)
    if material:m.materials.append(material)
    return o
def bevel(o,amount=.012,segments=2):
    b=o.modifiers.new('边缘倒角 | Edge bevel','BEVEL');b.width=amount;b.segments=segments
    return o
def panel(name,verts,material,thick=.024):
    o=mesh(name,verts,[tuple(range(len(verts)))],material)
    so=o.modifiers.new('板厚 | Thickness','SOLIDIFY');so.thickness=thick
    bevel(o,.009);return o
def box(name,loc,size,material):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=move(bpy.context.object);o.name=name;o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(material);bevel(o);return o
def rod(name,a,b,r,material,vertices=12):
    a,b=Vector(a),Vector(b);d=b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=d.length,location=(a+b)/2)
    o=move(bpy.context.object);o.name=name;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();o.data.materials.append(material)
    for p in o.data.polygons:p.use_smooth=True
    return o
def ring(name,loc,r,t,material,axis=(0,0,1)):
    bpy.ops.mesh.primitive_torus_add(major_radius=r,minor_radius=t,major_segments=48,minor_segments=8,location=loc)
    o=move(bpy.context.object);o.name=name;o.rotation_euler=Vector(axis).to_track_quat('Z','Y').to_euler();o.data.materials.append(material);return o
def seam(name,points,material=silver,r=.009,closed=True):
    for i in range(len(points) if closed else len(points)-1):rod(name,points[i],points[(i+1)%len(points)],r,material,8)
def bolts(points,spacing=.18):
    # Joined later for a tidy Outliner; small hexagonal fasteners remain mesh geometry.
    for a,b in zip(points,points[1:]+points[:1]):
        a,b=Vector(a),Vector(b);count=max(1,int((b-a).length/spacing))
        for j in range(count):
            p=a.lerp(b,(j+.5)/count);rod('铆钉 | Rivet',p,p+Vector((0,.012,0)),.016,silver,6)

print('APOLLO: importing supplied structure',flush=True)
bpy.ops.import_scene.gltf(filepath=str(GLB))
source=next(o for o in scene.objects if o.type=='MESH')
source.data.transform(Matrix.Scale(9.6,4)@source.matrix_world);source.matrix_world=Matrix.Identity(4)
bpy.context.view_layer.objects.active=source;source.select_set(True)
dec=source.modifiers.new('保留轮廓的减面 | Structure reduction','DECIMATE');dec.ratio=.28
bpy.ops.object.modifier_apply(modifier=dec.name)
tri=source.modifiers.new('Triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
original=source.active_material;original.name='原模型 • 金箔及结构贴图 | Original PBR'
p=original.node_tree.nodes.get('Principled BSDF')
if 'Specular Tint' in p.inputs:p.inputs['Specular Tint'].default_value=(1,1,1,1)
if 'Specular IOR Level' in p.inputs:p.inputs['Specular IOR Level'].default_value=.45
v=np.empty(len(source.data.vertices)*3,dtype=np.float32);source.data.vertices.foreach_get('co',v);v=v.reshape(-1,3)
idx=np.empty(len(source.data.loops),dtype=np.int32);source.data.loops.foreach_get('vertex_index',idx);faces=idx.reshape(-1,3)
uv=np.empty(len(source.data.loops)*2,dtype=np.float32);source.data.uv_layers.active.data.foreach_get('uv',uv);uv=uv.reshape(-1,3,2)
centers=v[faces].mean(axis=1);cat=np.zeros(len(faces),dtype=np.int8)
cat[centers[:,2]>=2.94]=1
leg=(centers[:,2]<2.94)&((np.maximum(abs(centers[:,0]),abs(centers[:,1]))>2.62)|(centers[:,2]<.75))
for i,(axis,sign) in enumerate([(1,1),(0,1),(1,-1),(0,-1)]):
    other=1-axis;cat[leg&(centers[:,axis]*sign>abs(centers[:,other]))]=2+i
names=['下降级 • Gold descent stage','上升级结构 • Ascent structure','前支腿 • Front landing gear','右支腿 • Right landing gear','后支腿 • Rear landing gear','左支腿 • Left landing gear']
for i,name in enumerate(names):
    mask=cat==i;f=faces[mask];used,inverse=np.unique(f,return_inverse=True)
    ob=mesh(name,v[used].tolist(),inverse.reshape(-1,3).tolist(),original,basecol)
    layer=ob.data.uv_layers.new(name='Original UV');layer.data.foreach_set('uv',uv[mask].ravel())
    ob['source']='User supplied GLB, reduced and spatially grouped; original UV retained'
bpy.data.objects.remove(source,do_unlink=True)

# Faceted pressure cabin closes the missing front/rear walls in the supplied mesh.
current=frontcol
# Clean, independently editable side blankets replace ragged face-level colouring.
ascent=bpy.data.objects['上升级结构 • Ascent structure']
for sign in [-1,1]:
    def sidepoint(y,z):
        hit,p,normal,index=ascent.ray_cast(Vector((sign*5,y,z)),Vector((-sign,0,0)))
        return (p.x+sign*.045,y,z) if hit else (sign*1.72,y,z)
    patches=[([(-.8,3.30),(.65,3.30),(.70,3.94),(-.8,3.94)],black),
             ([(-.8,3.98),(.13,3.98),(.13,4.78),(-.8,4.78)],ivory),
             ([(.17,3.98),(.72,3.98),(.74,4.78),(.17,4.78)],black),
             ([(-.8,4.82),(.74,4.82),(.74,5.47),(-.8,5.47)],silver)]
    for k,(yz,material) in enumerate(patches):
        # Bilinear grid conforms to the pre-existing faceted shell.
        vs=[];fs=[];steps=12
        for j in range(steps+1):
            t=j/steps
            left=Vector(yz[0]).lerp(Vector(yz[3]),t);right=Vector(yz[1]).lerp(Vector(yz[2]),t)
            for i in range(steps+1):
                q=left.lerp(right,i/steps);vs.append(sidepoint(q.x,q.y))
        for j in range(steps):
            for i in range(steps):
                a=j*(steps+1)+i;fs.append((a,a+1,a+steps+2,a+steps+1))
        mesh('侧面隔热板 %s %s | Side blanket'%(sign,k),vs,fs,material)
        border=[]
        for a,b in zip(yz,yz[1:]+yz[:1]):
            for j in range(8):
                q=Vector(a).lerp(Vector(b),j/8);border.append(sidepoint(q.x,q.y))
        seam('侧板铝边 | Side trim',border,r=.008)
outline=[(-.56,5.72),(.56,5.72),(1.78,4.92),(1.84,3.83),(1.17,3.00),(-1.17,3.00),(-1.84,3.83),(-1.78,4.92)]
def depth(z):return 2.13+(z-3.0)*.08
front=[(x,depth(z),z) for x,z in outline]
back=[(x,-1.97,z) for x,z in outline]
ob=mesh('承压舱 • Closed pressure cabin',front+back,[tuple(range(8)),tuple(range(15,7,-1))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)],black)
bevel(ob,.025)
# Each front plate has its own object, seam and material.
def frontpanel(name,xz,material):
    pts=[(x,depth(z)+.038,z) for x,z in xz];ob=panel(name,pts,material);seam('面板接缝 | Panel seam',pts,r=.011);return pts
frontpanel('左肩面板 | Left brow',[(-1.78,4.92),(-.56,5.72),(-.42,5.10),(-1.13,4.63)],ivory)
frontpanel('右肩面板 | Right brow',[(.42,5.10),(.56,5.72),(1.78,4.92),(1.13,4.63)],silver)
frontpanel('左下舱壁 | Lower left',[(-1.84,3.83),(-1.15,3.0),(-.53,3.0),(-.62,4.30),(-1.65,4.22)],silver)
frontpanel('右下舱壁 | Lower right',[(.62,4.30),(.53,3.0),(1.15,3.0),(1.84,3.83),(1.65,4.22)],ivory)
frontpanel('左窗周边 | Window surround L',[(-1.78,4.90),(-.48,5.10),(-.62,4.28),(-1.78,4.15)],black)
frontpanel('右窗周边 | Window surround R',[(.48,5.10),(1.78,4.90),(1.78,4.15),(.62,4.28)],black)
for sign in [-1,1]:
    pts=[(sign*.63,depth(4.97)+.09,4.97),(sign*1.45,depth(4.77)+.09,4.77),(sign*.74,depth(4.39)+.09,4.39)]
    panel('三角舷窗 • '+('Left' if sign<0 else 'Right'),pts,glass)
    seam('舷窗黑色密封 | Window gasket',pts,r=.044,material=rubber)
    seam('舷窗银色压框 | Window frame',[(x,y+.01,z) for x,y,z in pts],r=.017)
    # A narrow highlight gives opaque game-safe windows a readable glass surface.
    a=Vector(pts[0]).lerp(Vector(pts[1]),.16);b=Vector(pts[0]).lerp(Vector(pts[1]),.77)
    rod('玻璃反光边 | Window glint',a+Vector((0,.046,-.065)),b+Vector((0,.046,-.065)),.008,silver,8)
centerpts=frontpanel('中央仪器面板 | Center spine',[(-.56,5.72),(.56,5.72),(.60,4.23),(.51,3.04),(-.51,3.04),(-.60,4.23)],ivory)
bolts(centerpts)
for z,r in [(4.49,.13),(5.20,.058),(3.10,.055)]:
    y=depth(z)+.075;rod('圆形仪器孔 | Optical port',(0,y,z),(0,y+.016,z),r,rubber,32)
    ring('仪器孔金属边 | Port bezel',(0,y+.027,z),r,.025,silver,(0,1,0))
# Broad rear equipment face, matching the supplied rear-angle views.
rearpts=[(x,-2.015,z) for x,z in outline]
panel('后舱壁 | Rear equipment face',rearpts,silver)
for sign in [-1,1]:
    panel('后隔热分区 | Rear thermal panel',[(sign*.58,-2.05,5.55),(sign*1.72,-2.05,4.87),(sign*1.72,-2.05,3.92),(sign*.82,-2.05,3.2)],black)
panel('后中央隔热带 | Rear center strip',[(-.5,-2.056,5.72),(.5,-2.056,5.72),(.6,-2.056,3.08),(-.6,-2.056,3.08)],ivory)
seam('后舱壁接缝 | Rear seams',rearpts)

current=detailcol
# Closed EVA hatch with readable rounded frame, hinges and latch.
hatch=box('出舱舱门 | EVA hatch',(0,depth(3.64)+.09,3.64),(.88,.09,.88),silver)
hatch.modifiers['边缘倒角 | Edge bevel'].width=.075;hatch.modifiers['边缘倒角 | Edge bevel'].segments=5
frame=[(-.43,2.315,3.23),(.43,2.315,3.23),(.43,2.385,4.04),(-.43,2.385,4.04)]
seam('舱门密封 | Hatch seal',frame,material=rubber,r=.023)
for z in [3.35,3.89]:box('舱门铰链 | Hatch hinge',(-.47,2.40,z),(.13,.10,.13),silver)
rod('舱门把手 | Hatch handle',(.24,2.43,3.56),(.24,2.43,3.74),.022,black)
rod('舱门闩 | Hatch latch',(.18,2.43,3.66),(.34,2.43,3.66),.017,silver)
platform=box('出舱平台 | EVA porch',(0,2.85,2.96),(1.07,1.10,.075),black)
for j in range(15):rod('平台防滑横条 | Porch tread',(-.51,2.34+j*.073,3.01),(.51,2.34+j*.073,3.01),.014,silver,8)
for sign in [-1,1]:
    rod('平台扶手立柱 | Porch handrail',(sign*.56,2.39,3.00),(sign*.56,2.39,3.71),.019,silver)
    rod('平台扶手斜撑 | Porch handrail',(sign*.56,2.39,3.71),(sign*.56,3.34,3.14),.019,silver)
    rod('平台边框 | Porch edge',(sign*.56,2.33,3.02),(sign*.56,3.37,3.02),.022,silver)
# Ladder runs along front landing strut; individual rungs remain selectable.
top=Vector((0,3.39,2.86));bottom=Vector((0,4.44,.34))
for sign in [-1,1]:rod('舷梯侧梁 | Ladder rail',top+Vector((sign*.28,0,0)),bottom+Vector((sign*.28,0,0)),.029,silver)
for i in range(10):
    p=bottom.lerp(top,i/9);rod('舷梯踏棍 %02d | Rung'%(i+1),p+Vector((-.28,0,0)),p+Vector((.28,0,0)),.024,silver)
for t in [.12,.65,.95]:
    p=bottom.lerp(top,t)
    for sign in [-1,1]:rod('舷梯支架 | Ladder bracket',p+Vector((sign*.24,0,0)),p+Vector((sign*.24,-.18,0)),.019,black)

current=hardwarecol
# Flag and lettering are new mesh details, not photographs pasted over the spacecraft.
def flatrect(name,x,z,w,h,y,material):
    return panel(name,[(x-w/2,y,z-h/2),(x+w/2,y,z-h/2),(x+w/2,y,z+h/2),(x-w/2,y,z+h/2)],material,.004)
fy=2.145;fx=-1.30;fz=1.87;fw=.65;fh=.36
flagstart=set(hardwarecol.objects)
flatrect('国旗安装板 | Flag mounting plate',fx,fz,.95,.67,fy-.014,black)
flatrect('国旗底板 | Flag backing',fx,fz,fw+.05,fh+.05,fy,white)
for j in range(13):flatrect('国旗条纹 | Flag stripe',fx,fz+fh/2-(j+.5)*fh/13,fw,fh/13,fy+.009,red if j%2==0 else white)
flatrect('国旗蓝区 | Flag canton',fx+fw*.3,fz+fh*.23,fw*.4,fh*.54,fy+.02,blue)
for row in range(9):
    for col in range(6 if row%2==0 else 5):
        x=fx+fw*.48-col*fw*.063-(fw*.03 if row%2 else 0);z=fz+fh*.46-row*fh*.055
        rod('旗星 | Flag star',(x,fy+.023,z),(x,fy+.026,z),.007,white,5)
flagobjects=set(hardwarecol.objects)-flagstart
textstart=set(hardwarecol.objects)
flatrect('文字铭牌 | Identification plate',1.26,1.8,1.00,.69,2.145,black)
def label(body,loc,size,material,name):
    curve=bpy.data.curves.new(name,'FONT');curve.body=body;curve.align_x='CENTER';curve.size=size;curve.extrude=.001
    ob=bpy.data.objects.new(name,curve);current.objects.link(ob);ob.location=loc;ob.rotation_euler=(math.pi/2,0,math.pi);curve.materials.append(material);return ob
label('UNITED\nSTATES',(1.26,2.166,1.91),.18,white,'美国标识 | UNITED STATES')
textobjects=set(hardwarecol.objects)-textstart
def fit_insignia(objects,origin,target,angle):
    bpy.context.view_layer.update()
    matrix=Matrix.Translation(Vector(target))@Matrix.Rotation(angle,4,'Z')@Matrix.Translation(-Vector(origin))
    for ob in objects:ob.matrix_world=matrix@ob.matrix_world
fit_insignia(flagobjects,(-1.30,fy,1.87),(-1.42,1.69,1.87),math.radians(43))
fit_insignia(textobjects,(1.26,fy,1.80),(1.48,1.77,1.80),math.radians(-43))
label('LM • APOLLO',(0,2.449,3.89),.055,black,'舱门铭牌 | LM plaque')

# Pack reference images in hidden collection for self-contained future editing.
for i,name in enumerate(['Apollo_16_LM.jpg','阿波罗背部.png','阿波罗侧边1.png','阿波罗侧边2.png','阿波罗顶部.png']):
    image=bpy.data.images.load(str(SOURCE/name));image.pack()
    ob=bpy.data.objects.new('参考 • '+name,None);refcol.objects.link(ob);ob.empty_display_type='IMAGE';ob.data=image;ob.empty_display_size=7;ob.location=(12+i*8,0,4);ob.rotation_euler=(math.pi/2,0,0);ob.hide_render=True
refcol.hide_viewport=True;refcol.hide_render=True

# Merge repetitive tiny hardware only; principal panels, hatch and ladder remain editable.
for prefix in ['铆钉','旗星','面板接缝','后舱壁接缝']:
    items=[o for o in scene.objects if o.type=='MESH' and o.name.startswith(prefix)]
    if len(items)>1:
        bpy.ops.object.select_all(action='DESELECT')
        for o in items:o.select_set(True)
        bpy.context.view_layer.objects.active=items[0];bpy.ops.object.join();items[0].name=prefix+' • Combined hardware'

current=studio
floor=mat('摄影棚背景 | Studio slate',(.055,.069,.085),.15,.53)
box('摄影棚地面 | Studio floor',(0,0,-.17),(200,200,.20),floor)
scene.world=bpy.data.worlds.new('Studio world');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.19,.22,.27,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.45
for name,loc,power,size in [('Key',(5,7,12),2100,7),('Fill',(-7,4,7),1550,6),('Rim',(1,-6,11),2400,5)]:
    bpy.ops.object.light_add(type='AREA',location=loc);o=move(bpy.context.object);o.name='灯光 • '+name;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,3))-o.location).to_track_quat('-Z','Y').to_euler()
def camera(name,loc,scale):
    bpy.ops.object.camera_add(location=loc);c=move(bpy.context.object);c.name=name;c.rotation_euler=(Vector((0,0,3.1))-c.location).to_track_quat('-Z','Y').to_euler();c.data.type='ORTHO';c.data.ortho_scale=scale;c.data.lens=50;return c
cameras=[camera('01 • 前侧整体 | Hero',(10,14,10),12.2),camera('02 • 正面 | Front',(0,20,6.5),11.2),camera('03 • 背侧 | Rear',(-12,-15,10),12.0),camera('04 • 顶部 | Top',(.01,.01,23),11.5)]
scene.camera=cameras[0];scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=1400;scene.render.resolution_y=1400;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
scene['Model notes']='Reference-guided completion of supplied GLB. Approximate scale; exterior study, not engineering reconstruction. Front +Y, up +Z.'
scene['Source file']=GLB.name
scene['Completed additions']='Closed fore/aft cabin; triangular windows; thermal panels; hatch, porch, ladder; insignia. Source base structure and PBR preserved.'
readme=bpy.data.texts.new('说明 | READ ME')
readme.write('阿波罗登月舱 — 照片参考制作\n\n以用户提供的 GLB 为基础完成，不是从零建立的原创全部网格。\n原模型经减面并按上升级、下降级、四条支腿分组；保留原 UV 及 PBR。\n新增前后舱壁、三角舷窗、出舱舱门、平台、舷梯和标识，可单独选择编辑。\n90 参考集合默认隐藏，包含打包的多角度照片。\n99 摄影棚集合用于预览，导出时排除。\n单位为近似米，正面 +Y、上方 +Z。外观参考模型，不是工程复原，未制作内部驾驶舱、动画或碰撞体。\n原始文件保持不变。\n')
bpy.ops.file.pack_all()
bpy.ops.object.select_all(action='DESELECT')
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_perspective='CAMERA';area.spaces.active.overlay.show_overlays=False
        area.spaces.active.shading.type='MATERIAL';area.spaces.active.shading.use_scene_world=False
        area.spaces.active.clip_end=500
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Apollo_LunarLander.blend'))
for c,name in zip(cameras,['01_Hero','02_Front','03_Rear','04_Top']):
    scene.camera=c;scene.render.filepath=str(OUT/'Previews'/f'{name}.png');bpy.ops.render.render(write_still=True)
scene.camera=cameras[0]
print('APOLLO: exporting asset only',flush=True)
bpy.ops.object.select_all(action='DESELECT')
for col in [basecol,frontcol,detailcol,hardwarecol]:
    for o in col.objects:o.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(OUT/'Apollo_LunarLander.glb'),export_format='GLB',use_selection=True,export_apply=True,export_extras=True)
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Apollo_LunarLander.blend'))
report={'source':str(GLB),'source_sha256':hashlib.sha256(GLB.read_bytes()).hexdigest(),'basis':'User-supplied GLB with reference-guided additions; not built entirely from scratch','mesh_objects':len([o for c in [basecol,frontcol,detailcol,hardwarecol] for o in c.objects if o.type=='MESH']),'base_faces':sum(len(o.data.polygons) for o in basecol.objects),'units':'Approximate metres','front_axis':'+Y','up_axis':'+Z','limitations':['Exterior only','No rig, animation or collision meshes','Not an engineering-accurate reconstruction','Unity runtime assets are exported separately with Tools/export_apollo_unity.py']}
(OUT/'build-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print('APOLLO_BUILD_COMPLETE',flush=True)
