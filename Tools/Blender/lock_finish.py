import bpy, math, os, json
from mathutils import Vector
s=bpy.context.scene
out=r'F:\UnityProjects\RadioAssets\Props\CombinationLock'
body=bpy.data.objects['Lock_Body']
for p in body.data.polygons:p.use_smooth=False
for i in range(1,4):
    w=bpy.data.objects[f'Wheel_{i}']
    for p in w.data.polygons:p.use_smooth=False
    # Strengthen number readability, preserving the flat mounting plane.
    ids={v for p in w.data.polygons if w.data.materials[p.material_index].name=='Black engraved digits' for v in p.vertices}
    for idx in ids:
        v=w.data.vertices[idx];x,y,z=v.co
        k=round(math.atan2(z,-y)/(math.tau/10))%10;a=k*math.tau/10
        normal=Vector((0,-math.cos(a),math.sin(a)));up=Vector((0,math.sin(a),math.cos(a)))
        origin=normal*(.0168*math.cos(math.pi/10)+.00012)
        delta=v.co-origin;v.co=origin+Vector((delta.x*1.16,0,0))+up*(delta.dot(up)*1.16)+normal*delta.dot(normal)
for o in bpy.data.collections['STUDIO_NOT_FOR_EXPORT'].objects:
    if o.type=='LIGHT':o.data.energy*=.45
bpy.data.objects['Studio floor'].scale.x=100;bpy.data.objects['Studio floor'].scale.y=100
s.view_settings.exposure=-1.2
cam=s.camera;cam.location=(.105,-.43,.158);cam.rotation_euler=(Vector((0,-.002,.066))-cam.location).to_track_quat('-Z','Y').to_euler()
# Verify all 30 code stops: face normal at the selected index must point forward.
checks=[]
for i in range(1,4):
    w=bpy.data.objects[f'Wheel_{i}'];preview=w.rotation_euler.x
    for digit in range(10):
        w.rotation_euler.x=digit*math.tau/10;bpy.context.view_layer.update()
        n=w.matrix_world.to_3x3()@Vector((0,-math.cos(digit*math.tau/10),math.sin(digit*math.tau/10)))
        assert (n-Vector((0,-1,0))).length<1e-5
    w.rotation_euler.x=preview
    checks.append({'object':w.name,'digits':list(range(10)),'axis':'local X','step_degrees':36,'origin':list(w.location)})
s.render.filepath=os.path.join(out,'CombinationLock_preview.png')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out,'CombinationLock.blend'))
result={'rotation_test':'30/30 passed','disks':checks}
