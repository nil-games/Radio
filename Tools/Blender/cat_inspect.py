import bpy
from mathutils import Vector, Quaternion
obj = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
coords = [obj.matrix_world @ v.co for v in obj.data.vertices]
result = {'file': bpy.data.filepath, 'verts': len(coords), 'faces': len(obj.data.polygons), 'bounds': [[min(v[i] for v in coords), max(v[i] for v in coords)] for i in range(3)], 'modifiers': [(m.name,m.type) for m in obj.modifiers], 'materials': [m.name for m in obj.data.materials], 'addons': list(bpy.context.preferences.addons.keys())}
for area in bpy.context.screen.areas:
    if area.type == 'VIEW_3D':
        s=area.spaces.active
        s.region_3d.view_rotation = Quaternion((0.5,0.5,0.5,0.5))
        s.region_3d.view_perspective = 'ORTHO'
        s.region_3d.view_location = sum(coords,Vector()) / len(coords)
        s.region_3d.view_distance = max(obj.dimensions)*1.8
