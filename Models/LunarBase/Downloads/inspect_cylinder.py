import bpy,json
from mathutils import Vector
from pathlib import Path
root=Path('C:/Coding/Projects/VR_game/Models/LunarBase/Downloads')
bpy.ops.wm.open_mainfile(filepath=str(root/'portable_welding_cart/portable_welding_cart_2k.blend'))
obj=bpy.data.objects['portable_welding_cart']
bpy.context.view_layer.objects.active=obj
obj.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.separate(type='LOOSE')
bpy.ops.object.mode_set(mode='OBJECT')
data=[]
for obj in bpy.context.scene.objects:
    if obj.type != 'MESH': continue
    points=[obj.matrix_world@v.co for v in obj.data.vertices]
    lo=[min(v[i] for v in points) for i in range(3)]
    hi=[max(v[i] for v in points) for i in range(3)]
    data.append({'name':obj.name,'verts':len(points),'min':lo,'max':hi,'dims':[hi[i]-lo[i] for i in range(3)]})
(root/'cylinder-components.json').write_text(json.dumps(data,indent=2))
print(json.dumps(sorted(data,key=lambda d:d['verts'],reverse=True),indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(root/'portable_welding_cart/loose-parts.blend'))
