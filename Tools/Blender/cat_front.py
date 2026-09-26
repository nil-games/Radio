import bpy
from mathutils import Quaternion
obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
for a in bpy.context.screen.areas:
    if a.type=='VIEW_3D':
        a.spaces.active.region_3d.view_rotation=Quaternion((0.70710678,0.70710678,0,0))
result={'groups':[(g.name,len([v for v in obj.data.vertices if any(w.group==g.index for w in v.groups)])) for g in obj.vertex_groups], 'armature_modifier': [(m.name,m.object.name if m.object else None) for m in obj.modifiers if m.type=='ARMATURE'], 'transform': [list(row) for row in obj.matrix_world], 'images':[(i.name,i.filepath,bool(i.packed_file)) for i in bpy.data.images]}
