import bpy, os, json, struct
out=r'F:\UnityProjects\RadioAssets\Props\CombinationLock'
for i in [2,3]:
    w=bpy.data.objects[f'Wheel_{i}'];w.data=bpy.data.objects['Wheel_1'].data.copy()
    for idx,slot in enumerate(w.material_slots):
        slot.link='DATA';slot.material=bpy.data.objects['Wheel_1'].material_slots[idx].material
for scene in bpy.data.scenes:
    for o in scene.objects:o.select_set(False)
for o in bpy.data.collections['LOCK_ASSET'].objects:o.select_set(True)
bpy.context.view_layer.objects.active=bpy.data.objects['Lock_Body']
bpy.context.view_layer.update()
bpy.ops.export_scene.gltf(filepath=os.path.join(out,'CombinationLock.glb'),export_format='GLB',use_selection=True,export_apply=True,use_active_scene=True)
bpy.ops.export_scene.fbx(filepath=os.path.join(out,'CombinationLock.fbx'),use_selection=True,object_types={'MESH','EMPTY'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True)
with open(os.path.join(out,'CombinationLock.glb'),'rb') as f:
    header=f.read(12);length,typ=struct.unpack('<II',f.read(8));data=json.loads(f.read(length))
nodes=[n.get('name') for n in data['nodes']]
assert all(n in nodes for n in ['Wheel_1','Wheel_2','Wheel_3','Lock_Body','Lock_Shackle'])
assert 'Cube' not in nodes and not data.get('cameras'),nodes
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out,'CombinationLock.blend'))
result={'exported_nodes':nodes,'images':len(data.get('images',[]))}
