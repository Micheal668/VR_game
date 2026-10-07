"""Prepare public-domain NASA Apollo 11 lunar sample 10021,79 for a game prop."""
import bpy,json,hashlib
from pathlib import Path
from mathutils import Vector
ROOT=Path('C:/Coding/Projects/VR_game')
SRC=ROOT/'Models/LunarBase/Downloads/apollo_10021_79'
OUT=ROOT/'Assets/_LunarEscape/Art/StationProps/apollo_10021_79'
OUT.mkdir(parents=True,exist_ok=True)
TEX=OUT/'textures';TEX.mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
obj_path=next((SRC/'extracted').glob('*.obj'))
bpy.ops.wm.obj_import(filepath=str(obj_path),forward_axis='NEGATIVE_Z',up_axis='Y')
obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
bpy.context.view_layer.objects.active=obj;obj.select_set(True)
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
obj.name='apollo_10021_79'
obj.data.calc_loop_triangles();original_tris=len(obj.data.loop_triangles)
points=[v.co for v in obj.data.vertices]
lo=Vector([min(v[i] for v in points) for i in range(3)]);hi=Vector([max(v[i] for v in points) for i in range(3)])
source_dims=hi-lo;scale=.26/max(source_dims);mid=(hi+lo)*.5
for v in obj.data.vertices:v.co=(v.co-mid)*scale
dec=obj.modifiers.new('Game mesh simplification','DECIMATE');dec.ratio=min(1,16000/original_tris)
bpy.ops.object.modifier_apply(modifier=dec.name)
for poly in obj.data.polygons:poly.use_smooth=True
obj.data.calc_loop_triangles();tris=len(obj.data.loop_triangles)
image=bpy.data.images.load(str(next((SRC/'extracted').glob('*.jpg'))))
source_tex_size=list(image.size)
image.scale(2048,2048);image.filepath_raw=str(TEX/'apollo_10021_79_diff_2k.png');image.file_format='PNG';image.save()
mat=bpy.data.materials.new('apollo_10021_79');mat.use_nodes=True
nodes=mat.node_tree.nodes;nodes.clear()
shader=nodes.new('ShaderNodeBsdfPrincipled');shader.inputs['Roughness'].default_value=.88
img=nodes.new('ShaderNodeTexImage');img.image=image
output=nodes.new('ShaderNodeOutputMaterial')
mat.node_tree.links.new(img.outputs['Color'],shader.inputs['Base Color']);mat.node_tree.links.new(shader.outputs[0],output.inputs['Surface'])
obj.data.materials.clear();obj.data.materials.append(mat)
for poly in obj.data.polygons:poly.material_index=0
fbx=OUT/'apollo_10021_79.fbx'
bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,
 apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=True,
 use_mesh_modifiers=True,mesh_smooth_type='OFF',use_tspace=True,path_mode='RELATIVE',add_leaf_bones=False,bake_anim=False)
def rel(p):return str(p.relative_to(ROOT)).replace('\\','/')
dims=source_dims*scale
entry={'id':'apollo_10021_79','fbx':rel(fbx),'source_id':'apollo_10021_79',
'source_url':'https://ares.jsc.nasa.gov/astromaterials3d/explorer/?sample=10021-79',
'author':'NASA / Astromaterials 3D / Lunar Sample Laboratory Facility','license':'Public domain (NASA Astromaterials 3D FAQ)',
'bounds_m':[dims.x,dims.z,dims.y],'source_bounds_m':[source_dims.x,source_dims.z,source_dims.y],
'source_triangles':original_tris,'triangles':tris,'source_texture_dimensions':source_tex_size,
'materials':[{'name':'apollo_10021_79','base_color':rel(TEX/'apollo_10021_79_diff_2k.png'),
'normal':'','metallic_smoothness':'','occlusion':'','alpha_clip':False,'metallic':0,'smoothness':.12}],
'notes':'Actual Apollo 11 lunar sample 10021,79 photogrammetric model. Decimated for VR and source color texture resized to 2K; original color/UV retained. Uniformly enlarged to 0.26 m for gameplay readability; this in-game copy is not the real sample size. No synthetic normal or PBR data presented as measured NASA data.',
'sha256':hashlib.sha256(fbx.read_bytes()).hexdigest()}
(OUT/'provenance.json').write_text(json.dumps(entry,indent=2),encoding='utf-8')
manifest_path=OUT.parent/'material-manifest.json'
manifest=json.loads(manifest_path.read_text())
manifest['assets']=[a for a in manifest['assets'] if a['id']!=entry['id']]+[entry]
manifest_path.write_text(json.dumps(manifest,indent=2),encoding='utf-8')
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24
scene.render.resolution_x=800;scene.render.resolution_y=800;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('studio');scene.world.use_nodes=True
background=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND');background.inputs[0].default_value=(.22,.25,.3,1)
background.inputs[1].default_value=.75;scene.view_settings.view_transform='AgX'
for name,loc,energy,size in [('Key',(1.5,-2,3),300,2),('Fill',(-2,-.5,1.4),180,2),('Rim',(.8,1.5,2),250,1.5)]:
 data=bpy.data.lights.new(name,'AREA');data.energy=energy*.26*.26;data.shape='DISK';data.size=size*.26
 light=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(light);light.location=Vector(loc)*.26
 light.rotation_euler=(-light.location).to_track_quat('-Z','Y').to_euler()
camera=bpy.data.objects.new('Camera',bpy.data.cameras.new('Camera'));bpy.context.collection.objects.link(camera)
camera.location=Vector((1.4,-2.5,1.6))*.26;camera.rotation_euler=(-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO';camera.data.ortho_scale=.37;scene.camera=camera
scene.render.filepath=str(SRC.parent/'Previews/apollo_10021_79.png')
bpy.ops.render.render(write_still=True)
print('NASA_LUNAR_SAMPLE_PREPARED',json.dumps(entry),flush=True)
