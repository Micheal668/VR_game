"""Inspect the user-supplied suit with Blender; source file is never modified."""
from pathlib import Path
import bpy, math
from mathutils import Vector
root=Path(__file__).resolve().parents[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(root/'VR项目照片/Advanced Crew Escape Suit.glb'))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
for o in objects:
    for m in o.data.materials:
        if m and m.use_nodes:
            for n in m.node_tree.nodes:
                if n.type=='BSDF_PRINCIPLED':n.inputs['Transmission Weight'].default_value=0
points=[o.matrix_world @ Vector(c) for o in objects for c in o.bound_box]
lo=Vector(tuple(min(p[i] for p in points) for i in range(3)))
hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
print('SUIT_BOUNDS',lo,hi)
center=(lo+hi)/2; size=max(hi-lo)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=20
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('World');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.18,.18,1)
scene.view_settings.view_transform='Standard'
for pos in [(2,-3,4),(-3,1,2)]:
    bpy.ops.object.light_add(type='AREA',location=center+Vector(pos)*size)
    light=bpy.context.object;light.data.energy=150*size*size;light.data.shape='DISK';light.data.size=size*3
    light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
for i,offset in enumerate([(0,-1.8,.15),(0,1.8,.15),(1.8,0,.15)]):
    bpy.ops.object.camera_add(location=center+Vector(offset)*size)
    cam=bpy.context.object;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=size*1.15;scene.camera=cam
    scene.render.filepath=str(root/f'Logs/suit-source-{i}.png');bpy.ops.render.render(write_still=True)
