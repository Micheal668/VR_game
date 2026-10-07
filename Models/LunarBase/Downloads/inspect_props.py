import bpy, json
from pathlib import Path
from mathutils import Vector
root=Path('C:/Coding/Projects/VR_game/Models/LunarBase/Downloads')
result={}
for path in root.glob('*/*_2k.blend'):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    data=[]
    for obj in bpy.data.objects:
        if obj.type not in {'MESH','EMPTY','ARMATURE'}: continue
        bounds=[obj.matrix_world @ Vector(p) for p in obj.bound_box]
        data.append({'name':obj.name,'type':obj.type,'hide_render':obj.hide_render,
                     'location':list(obj.location),'rotation':list(obj.rotation_euler),'scale':list(obj.scale),
                     'bounds_min':[min(v[i] for v in bounds) for i in range(3)],
                     'bounds_max':[max(v[i] for v in bounds) for i in range(3)],
                     'polygons':len(obj.data.polygons) if obj.type=='MESH' else 0,
                     'materials':[s.name for s in obj.material_slots] if obj.type=='MESH' else [],
                     'parent':obj.parent.name if obj.parent else None})
    result[path.parent.name]=data
(root/'inspection.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result,indent=2))
