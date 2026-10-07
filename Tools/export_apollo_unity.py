"""Export the editable Blender exterior as a self-contained native Unity FBX package.

Run: C:/Tools/blender.exe -b -t 8 --python Tools/export_apollo_unity.py
The .blend stays untouched. Only visible model collections are exported.
"""
import json
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/_LunarEscape/Art/ApolloExterior'
OUT.mkdir(parents=True, exist_ok=True)
(OUT / 'Textures').mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'Models/ApolloLander/Apollo_LunarLander.blend'))

materials = [
    ('Original PBR', 'OriginalStructure'),
    ('Satin aluminium', 'SatinAluminium'),
    ('Warm aluminium', 'WarmAluminium'),
    ('Charcoal thermal panels', 'ThermalPanels'),
    ('Seals', 'Seals'),
    ('Blue smoked glass', 'WindowGlass'),
    ('Warm white', 'MarkingWhite'),
    ('Red', 'MarkingRed'),
    ('Navy', 'MarkingBlue'),
]
manifest = {'materials': [], 'source': 'Models/ApolloLander/Apollo_LunarLander.blend'}
for suffix, name in materials:
    material = next(m for m in bpy.data.materials if m.name.endswith(suffix))
    bsdf = material.node_tree.nodes.get('Principled BSDF')
    manifest['materials'].append({
        'name': name,
        'color': list(bsdf.inputs['Base Color'].default_value),
        'metallic': float(bsdf.inputs['Metallic'].default_value),
        'roughness': float(bsdf.inputs['Roughness'].default_value),
        'textured': name == 'OriginalStructure',
    })
    material.name = name

base = bpy.data.images.get('texture_pbr_20250901')
normal = bpy.data.images.get('texture_pbr_20250901_normal')
packed = bpy.data.images.get('texture_pbr_20250901_metallic-texture_pbr_20250901_roughness')
assert base and normal and packed, 'Expected the packed source PBR textures in the saved Blender model.'
for image, name in [(base, 'StructureBaseColor'), (normal, 'StructureNormal')]:
    image.filepath_raw = str(OUT / 'Textures' / (name + '.png'))
    image.file_format = 'PNG'
    image.save()
# glTF packs roughness in G and metallic in B; URP uses metallic R / smoothness A.
pixels = np.empty(len(packed.pixels), dtype=np.float32)
packed.pixels.foreach_get(pixels)
pixels = pixels.reshape(-1, 4)
converted = np.ones_like(pixels)
converted[:, 0] = pixels[:, 2]
converted[:, 3] = 1.0 - pixels[:, 1]
image = bpy.data.images.new('Unity Metallic Smoothness', width=packed.size[0], height=packed.size[1], alpha=True)
image.colorspace_settings.name = 'Non-Color'
image.pixels.foreach_set(converted.ravel())
image.filepath_raw = str(OUT / 'Textures/StructureMetallicSmoothness.png')
image.file_format = 'PNG'
image.save()

model = [o for o in bpy.context.scene.objects if o.type in {'MESH', 'FONT'}
         and any(c.name[:2] in {'01', '02', '03', '04'} for c in o.users_collection)]
groups = {}
anchors = {'Origin': (0, 0, 0), 'Up': (0, 0, 1), 'Front': (0, 1, 0),
           'Hatch': (0, 2.34, 3.64), 'LadderFoot': (0, 4.44, .34),
           'Porch': (0, 3.39, 2.86)}
for obj in model:
    collection = next(c.name[:2] for c in obj.users_collection if c.name[:2] in {'01', '02', '03', '04'})
    if collection == '01':
        group = obj.name.split(' • ')[-1].replace(' ', '')
        if 'landing gear' in obj.name:
            verts = [obj.matrix_world @ v.co for v in obj.data.vertices]
            # The spatial groups also contain parts of the central engine.
            # Select the outer pad, excluding the engine and inboard struts.
            axis = 1 if 'Front' in group or 'Rear' in group else 0
            extent = max(abs(v[axis]) for v in verts)
            verts = [v for v in verts if abs(v[axis]) > extent * .75]
            low = min(v.z for v in verts)
            foot = [v for v in verts if v.z < low + .22]
            center = tuple((min(v[i] for v in foot) + max(v[i] for v in foot)) / 2 for i in range(3))
            anchors['Foot' + group] = center
    else:
        group = {'02': 'CabinPanels', '03': 'EVAAccess', '04': 'Hardware'}[collection]
    groups.setdefault(group, []).append(obj)

# Apply the evaluated bevel/text geometry before joining. The editable originals
# remain in the .blend, while Unity gets a small number of renderers.
export_objects = []
for name, objects in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.convert(target='MESH')
    if len(objects) > 1:
        bpy.ops.object.join()
    joined = bpy.context.object
    joined.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    export_objects.append(joined)
for name, point in anchors.items():
    obj = bpy.data.objects.new('Anchor_' + name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = point
    export_objects.append(obj)

bpy.ops.object.select_all(action='DESELECT')
for obj in export_objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = export_objects[0]
bpy.ops.export_scene.fbx(filepath=str(OUT / 'ApolloExterior.fbx'), use_selection=True,
                         object_types={'MESH', 'EMPTY'}, axis_forward='-Z', axis_up='Y',
                         apply_unit_scale=True, bake_space_transform=False,
                         use_mesh_modifiers=True, mesh_smooth_type='OFF',
                         add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
for obj in export_objects:
    if obj.type == 'MESH':
        obj.data.calc_loop_triangles()
manifest['meshCount'] = len(groups)
manifest['triangleCount'] = sum(len(o.data.loop_triangles) for o in export_objects if o.type == 'MESH')
manifest['anchors'] = {k: list(v) for k, v in anchors.items()}
(OUT / 'materials.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
print('APOLLO_UNITY_EXPORT ' + json.dumps(manifest), flush=True)
