"""Run in Blender: prepare CC0 downloaded props for Unity without gameplay edits."""
import bpy, bmesh, numpy as np, json, shutil, math, hashlib
from pathlib import Path
from mathutils import Vector, Matrix

ROOT=Path('C:/Coding/Projects/VR_game')
SRC=ROOT/'Models/LunarBase/Downloads'
OUT=ROOT/'Assets/_LunarEscape/Art/StationProps'
OUT.mkdir(parents=True,exist_ok=True)
PREV=SRC/'Previews'
PREV.mkdir(exist_ok=True)
ids=['combination_wrench','metal_toolbox','medical_box','circuit_board','power_box_01','oxygen_cylinder']
manifest={'texture_notes':'Base color is sRGB. Normal, metallic_smoothness and AO are linear. Metallic R; smoothness A. Normal is OpenGL tangent space. All pivots are mesh-bounds centers.', 'assets':[]}
previous_manifest=OUT/'material-manifest.json'
if previous_manifest.exists():
    manifest['assets']=[a for a in json.loads(previous_manifest.read_text(encoding='utf-8')).get('assets',[]) if a['id'] not in ids]

def rel(p): return str(p.relative_to(ROOT)).replace('\\','/')
def load_pixels(path,linear):
    image=bpy.data.images.load(str(path),check_existing=False)
    if linear: image.colorspace_settings.name='Non-Color'
    pixels=np.empty(len(image.pixels),dtype=np.float32)
    image.pixels.foreach_get(pixels)
    return image, pixels.reshape((-1,4))
def save_pixels(path,pixels,size,linear=True):
    img=bpy.data.images.new(path.stem,width=size[0],height=size[1],alpha=True)
    if linear: img.colorspace_settings.name='Non-Color'
    img.pixels.foreach_set(pixels.ravel())
    img.filepath_raw=str(path); img.file_format='PNG'; img.save()
    return img

def textures(asset_id,source_id):
    folder=OUT/asset_id/'textures';folder.mkdir(parents=True,exist_ok=True)
    source=SRC/source_id/'textures'
    base=folder/(asset_id+'_diff_2k.jpg')
    normal=folder/(asset_id+'_nor_gl_2k.png')
    shutil.copyfile(source/(source_id+'_diff_2k.jpg'),base)
    shutil.copyfile(source/(source_id+'_nor_gl_2k.png'),normal)
    image,pixels=load_pixels(source/(source_id+'_arm_2k.png'),True)
    ms=np.zeros_like(pixels);ms[:,0]=pixels[:,2];ms[:,3]=1-pixels[:,1]
    ms_path=folder/(asset_id+'_metallic_smoothness_2k.png')
    save_pixels(ms_path,ms,image.size)
    ao=np.ones_like(pixels);ao[:,:3]=pixels[:,0:1]
    ao_path=folder/(asset_id+'_ao_2k.png')
    save_pixels(ao_path,ao,image.size)
    values={'base_color':rel(base),'normal':rel(normal),'metallic_smoothness':rel(ms_path),'occlusion':rel(ao_path)}
    if asset_id=='circuit_board':
        diff,diffpix=load_pixels(base,False)
        alpha,alphapix=load_pixels(source/(source_id+'_alpha_2k.png'),True)
        diffpix[:,3]=alphapix[:,0]
        alpha_base=folder/(asset_id+'_diff_alpha_2k.png')
        save_pixels(alpha_base,diffpix,diff.size,False)
        values['alpha_base_color']=rel(alpha_base)
    return values

def setup_material(name,tex,alpha=False):
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    nodes=mat.node_tree.nodes;links=mat.node_tree.links
    nodes.clear()
    bsdf=nodes.new('ShaderNodeBsdfPrincipled')
    output=nodes.new('ShaderNodeOutputMaterial');links.new(bsdf.outputs[0],output.inputs['Surface'])
    base=nodes.new('ShaderNodeTexImage');base.image=bpy.data.images.load(str(ROOT/tex['alpha_base_color' if alpha else 'base_color']))
    links.new(base.outputs['Color'],bsdf.inputs['Base Color'])
    if alpha:
        links.new(base.outputs['Alpha'],bsdf.inputs['Alpha'])
    normal=nodes.new('ShaderNodeTexImage');normal.image=bpy.data.images.load(str(ROOT/tex['normal']))
    normal.image.colorspace_settings.name='Non-Color'
    nm=nodes.new('ShaderNodeNormalMap');links.new(normal.outputs['Color'],nm.inputs['Color']);links.new(nm.outputs['Normal'],bsdf.inputs['Normal'])
    ms=nodes.new('ShaderNodeTexImage');ms.image=bpy.data.images.load(str(ROOT/tex['metallic_smoothness']))
    ms.image.colorspace_settings.name='Non-Color'
    sep=nodes.new('ShaderNodeSeparateColor');links.new(ms.outputs['Color'],sep.inputs[0]);links.new(sep.outputs['Red'],bsdf.inputs['Metallic'])
    inv=nodes.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1
    links.new(ms.outputs['Alpha'],inv.inputs[1]);links.new(inv.outputs[0],bsdf.inputs['Roughness'])
    return mat

def raw_meshes():
    return [o for o in bpy.context.scene.objects if o.type=='MESH' and len(o.data.polygons)>0]

def cylinder_only():
    obj=bpy.data.objects['portable_welding_cart']
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.separate(type='LOOSE');bpy.ops.object.mode_set(mode='OBJECT')
    retained=[]
    # Source cart contains one green oxygen cylinder and a shorter red acetylene
    # cylinder. Keep green body plus its discrete regulator, gauge and valve parts.
    for part in raw_meshes():
        points=[part.matrix_world@v.co for v in part.data.vertices]
        lo=[min(p[i] for p in points) for i in range(3)]
        hi=[max(p[i] for p in points) for i in range(3)]
        is_body=(lo[0]<-0.24 and hi[0]<0 and hi[2]>1.4 and lo[2]<0.05)
        is_valve=(lo[2]>1.38 and hi[0]<0 and hi[1]<-0.1)
        if not (is_body or is_valve): bpy.data.objects.remove(part,do_unlink=True);continue
        # Remove transparent gauge cover faces; textured gauge faces/needles stay.
        bm=bmesh.new();bm.from_mesh(part.data)
        glass=[f for f in bm.faces if f.material_index==1]
        bmesh.ops.delete(bm,geom=glass,context='FACES');bm.to_mesh(part.data);bm.free()
        # Shorten only the straight vessel barrel, preserving the base, shoulder,
        # neck and valves. This is a documented portable gameplay adaptation.
        for v in part.data.vertices:
            if is_body:
                z=v.co.z
                if z>0.15 and z<1.20: v.co.z=0.15+(z-0.15)*0.0857142857143
                elif z>=1.20: v.co.z=z-0.96
            else: v.co.z-=0.96
        retained.append(part)
    return retained

def export_one(asset_id):
    source_id='portable_welding_cart' if asset_id=='oxygen_cylinder' else asset_id
    bpy.ops.wm.open_mainfile(filepath=str(SRC/source_id/(source_id+'_2k.blend')))
    if asset_id=='metal_toolbox':
        bpy.data.objects['metal_toolbox_lid'].rotation_euler.x=0
        bpy.context.view_layer.update()
    original=cylinder_only() if asset_id=='oxygen_cylinder' else raw_meshes()
    # Evaluate source rig/modifiers, bake the displayed pose, preserve UVs/normals.
    dg=bpy.context.evaluated_depsgraph_get(); baked=[]
    for obj in original:
        eo=obj.evaluated_get(dg)
        mesh=bpy.data.meshes.new_from_object(eo,preserve_all_data_layers=True,depsgraph=dg)
        mesh.transform(obj.matrix_world)
        clone=bpy.data.objects.new('prepared_'+obj.name,mesh);bpy.context.collection.objects.link(clone);baked.append(clone)
    for obj in list(bpy.context.scene.objects):
        if obj not in baked: bpy.data.objects.remove(obj,do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in baked: obj.select_set(True)
    bpy.context.view_layer.objects.active=baked[0];bpy.ops.object.join()
    obj=bpy.context.object;obj.name=asset_id
    # Preserve original material face assignment until reducing duplicate slots.
    tex=textures(asset_id,source_id)
    for old_mat in list(bpy.data.materials): old_mat.name='source_'+old_mat.name
    base_mat=setup_material(asset_id,tex)
    alpha_mat=setup_material(asset_id+'_alpha',tex,True) if asset_id=='circuit_board' else None
    alpha_polygons=[p.index for p in obj.data.polygons if alpha_mat and 'alpha' in obj.data.materials[p.material_index].name]
    obj.data.materials.clear();obj.data.materials.append(base_mat)
    if alpha_mat: obj.data.materials.append(alpha_mat)
    alpha_set=set(alpha_polygons)
    for poly in obj.data.polygons: poly.material_index=int(poly.index in alpha_set)
    coords=[v.co for v in obj.data.vertices]
    lo=Vector([min(v[i] for v in coords) for i in range(3)]);hi=Vector([max(v[i] for v in coords) for i in range(3)])
    mid=(lo+hi)*0.5
    scale=0.4/(hi.y-lo.y) if asset_id=='combination_wrench' else 1.0
    if asset_id=='oxygen_cylinder': scale=0.56/(hi.z-lo.z)
    for v in obj.data.vertices: v.co=(v.co-mid)*scale
    dims=(hi-lo)*scale
    obj.data.calc_loop_triangles();tris=len(obj.data.loop_triangles)
    folder=OUT/asset_id;fbx=folder/(asset_id+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},
                            global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
                            axis_forward='-Z',axis_up='Y',bake_space_transform=True,
                            use_mesh_modifiers=True,mesh_smooth_type='OFF',use_tspace=True,
                            path_mode='RELATIVE',embed_textures=False,add_leaf_bones=False,bake_anim=False)
    materials=[{'name':asset_id,**{k:v for k,v in tex.items() if k!='alpha_base_color'},'alpha_clip':False}]
    if alpha_mat:
        materials.append({**materials[0],'name':asset_id+'_alpha','base_color':tex['alpha_base_color'],'alpha_clip':True,'alpha_cutoff':0.5})
    source=json.loads((SRC/source_id/'source-manifest.json').read_text())
    notes={'metal_toolbox':'Source lid closed; meshes combined, original UV/PBR preserved.',
           'medical_box':'Closed original metal first-aid case; original flat case proportions preserved.',
           'oxygen_cylinder':'Green oxygen vessel/regulator extracted from source welding cart; straight barrel shortened to make a 0.56 m portable game prop; transparent gauge covers removed, gauge faces preserved.',
           'circuit_board':'Original electronics board; second material uses source alpha mask packed into base-color alpha.',
           'power_box_01':'Displayed source pose baked; open door and breakers preserved. Suitable for wall-mounted equipment, not a handheld battery.',
           'combination_wrench':'Original wrench uniformly scaled to 0.40 m length; body centered.'}[asset_id]
    entry={'id':asset_id,'fbx':rel(fbx),'source_id':source_id,'source_url':source['source_page'],
           'author':', '.join(source['author']),'license':'CC0-1.0', 'bounds_m':[dims.x,dims.z,dims.y],
           'triangles':tris,'materials':materials,'notes':notes,'sha256':hashlib.sha256(fbx.read_bytes()).hexdigest()}
    manifest['assets'].append(entry)
    (OUT/'material-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    (folder/'provenance.json').write_text(json.dumps(entry,indent=2),encoding='utf-8')
    # Save a Blender check image of the actual prepared mesh/textures.
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24
    scene.render.resolution_x=800;scene.render.resolution_y=800;scene.render.resolution_percentage=100
    scene.world=bpy.data.worlds.new('studio');scene.world.use_nodes=True
    background=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND')
    background.inputs[0].default_value=(0.22,0.25,0.3,1)
    background.inputs[1].default_value=.75
    scene.view_settings.view_transform='AgX'
    extent=max(dims)
    for name,location,energy,size in [('Key',(1.5,-2,3),300,2),('Fill',(-2,-.5,1.4),180,2),('Rim',(.8,1.5,2),250,1.5)]:
        data=bpy.data.lights.new(name,'AREA');data.energy=energy*extent*extent;data.shape='DISK';data.size=size*extent
        light=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(light)
        light.location=Vector(location)*extent;light.rotation_euler=(-light.location).to_track_quat('-Z','Y').to_euler()
    camera=bpy.data.objects.new('Camera',bpy.data.cameras.new('Camera'));bpy.context.collection.objects.link(camera)
    direction=Vector((1.4,-2.5,1.6)) if asset_id!='combination_wrench' else Vector((1,-2.2,2.9))
    camera.location=direction*extent;camera.rotation_euler=(-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.type='ORTHO';camera.data.ortho_scale=extent*1.4;scene.camera=camera
    scene.render.film_transparent=False;scene.render.image_settings.file_format='PNG'
    scene.render.filepath=str(PREV/(asset_id+'.png'))
    bpy.ops.render.render(write_still=True)
    print('PREPARED',asset_id,'bounds Unity',entry['bounds_m'],'triangles',tris,flush=True)

for asset_id in ids: export_one(asset_id)
print('STATION_PROPS_COMPLETE',str(OUT/'material-manifest.json'),flush=True)
