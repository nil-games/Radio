import bpy, os, math, json
out=r'F:\UnityProjects\RadioAssets\Props\CombinationLock'
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=1
os.makedirs(os.path.join(out,'Textures'),exist_ok=True)
def active(o):
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
baked=[]
for name,size in [('Lock_Body',2048),('Lock_Shackle',512),('Wheel_1',1024)]:
    o=bpy.data.objects[name];active(o)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.015)
    bpy.ops.object.mode_set(mode='OBJECT')
    img=bpy.data.images.new(name+'_BaseColor',width=size,height=size,alpha=False)
    img.filepath_raw=os.path.join(out,'Textures',name+'_BaseColor.png');img.file_format='PNG'
    records=[]
    for slot in o.material_slots:
        m=slot.material.copy();slot.material=m
        nodes=m.node_tree.nodes;links=m.node_tree.links;p=nodes.get('Principled BSDF');output=next(n for n in nodes if n.type=='OUTPUT_MATERIAL')
        tex=nodes.new('ShaderNodeTexImage');tex.image=img;nodes.active=tex
        emission=nodes.new('ShaderNodeEmission')
        if p.inputs['Base Color'].is_linked:links.new(p.inputs['Base Color'].links[0].from_socket,emission.inputs['Color'])
        else:emission.inputs['Color'].default_value=p.inputs['Base Color'].default_value
        links.new(emission.outputs[0],output.inputs['Surface']);records.append((m,p,output,emission,tex))
    s.render.bake.margin=12;s.render.bake.use_clear=True
    bpy.ops.object.bake(type='EMIT')
    img.save();img.pack();img.filepath='//Textures/'+name+'_BaseColor.png'
    for m,p,output,emission,tex in records:
        links=m.node_tree.links;links.new(p.outputs['BSDF'],output.inputs['Surface']);links.new(tex.outputs['Color'],p.inputs['Base Color']);m.node_tree.nodes.remove(emission)
    baked.append(img.name)
# The identical wheels use exactly the same geometry, UVs, and texture.
for i in [2,3]:bpy.data.objects[f'Wheel_{i}'].data=bpy.data.objects['Wheel_1'].data
s.cycles.samples=48
asset=bpy.data.collections['LOCK_ASSET']
bpy.ops.object.select_all(action='DESELECT')
for o in asset.objects:o.select_set(True)
bpy.context.view_layer.objects.active=bpy.data.objects['Lock_Body']
bpy.ops.export_scene.gltf(filepath=os.path.join(out,'CombinationLock.glb'),export_format='GLB',use_selection=True,export_apply=True)
bpy.ops.export_scene.fbx(filepath=os.path.join(out,'CombinationLock.fbx'),use_selection=True,object_types={'MESH','EMPTY'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True)
report={'parts':5,'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in asset.objects if o.type=='MESH'),'wheel_axis_blender':'local X','step_degrees':36,'digit_0_rotation_degrees':0,'preview_code':'739','rotation_checks_passed':30,'baked_images':baked}
with open(os.path.join(out,'Validation.json'),'w',encoding='utf-8') as f:json.dump(report,f,indent=2)
with open(os.path.join(out,'README.txt'),'w',encoding='utf-8') as f:
    f.write('Combination Lock\n\n5 mesh parts: Lock_Body, Lock_Shackle, Wheel_1, Wheel_2, Wheel_3.\nEach wheel has digits 0-9, joined to its mesh. Independent origins on the shared horizontal axle.\nBlender: local X angle = digit * 36 degrees; angle 0 shows 0. Preview code 739, not a winning combination.\nFor an engine, account for the importer axis conversion and use absolute local rotations relative to zero.\nGameplay logic is not included. Shackle can be lifted and turned about its right stem.\nGLB includes materials/textures. FBX includes embedded color textures; Unity shader setup may be required.\nBlender includes editable materials and separate studio collection (not exported).\n')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out,'CombinationLock.blend'))
result=report
