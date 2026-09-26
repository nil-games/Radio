import bpy, math, os, json
from mathutils import Vector

OUT=r'F:\UnityProjects\RadioAssets\Props\CombinationLock'
os.makedirs(OUT,exist_ok=True)
assert not os.path.exists(os.path.join(OUT,'CombinationLock.blend')), 'Do not overwrite an existing asset'
scene=bpy.data.scenes.new('Combination Lock - Studio')
bpy.context.window.scene=scene
asset=bpy.data.collections.new('LOCK_ASSET');scene.collection.children.link(asset)
studio=bpy.data.collections.new('STUDIO_NOT_FOR_EXPORT');scene.collection.children.link(studio)
def move(o,c=asset):
    for old in list(o.users_collection):old.objects.unlink(o)
    c.objects.link(o)
def mat(name,base,metal,rough,wear=False):
    m=bpy.data.materials.new(name);m.diffuse_color=(*base,1);m.use_nodes=True
    n=m.node_tree.nodes;l=m.node_tree.links;p=n.get('Principled BSDF')
    p.inputs['Base Color'].default_value=(*base,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    if wear:
        tc=n.new('ShaderNodeTexCoord');mapping=n.new('ShaderNodeVectorMath');mapping.operation='MULTIPLY';mapping.inputs[1].default_value=(7,7,180)
        l.new(tc.outputs['Generated'],mapping.inputs[0]);noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=5;noise.inputs['Detail'].default_value=2
        l.new(mapping.outputs[0],noise.inputs['Vector'])
        ramp=n.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.2;ramp.color_ramp.elements[0].color=(*(v*.52 for v in base),1)
        ramp.color_ramp.elements[1].position=.8;ramp.color_ramp.elements[1].color=(*(min(v*1.5,1) for v in base),1)
        l.new(noise.outputs['Fac'],ramp.inputs[0]);l.new(ramp.outputs[0],p.inputs['Base Color'])
        bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.12;bump.inputs['Distance'].default_value=.00008
        l.new(noise.outputs['Fac'],bump.inputs['Height']);l.new(bump.outputs[0],p.inputs['Normal'])
    return m
brass=mat('Aged brushed brass',(.36,.285,.15),.78,.36,True)
edge=mat('Edge brass',(.48,.385,.22),.8,.30,True)
steel=mat('Brushed steel shackle',(.42,.45,.47),.9,.23,True)
dark=mat('Dark recessed metal',(.022,.024,.021),.65,.48)
ink=mat('Black engraved digits',(.009,.011,.009),.1,.55)
def active(o):
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
def bevel(o,w,seg=2):
    active(o);b=o.modifiers.new('Manufactured edge bevel','BEVEL');b.width=w;b.segments=seg
    bpy.ops.object.modifier_apply(modifier=b.name)
    for p in o.data.polygons:p.use_smooth=True
    n=o.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL');n.keep_sharp=True;n.weight=40
    bpy.ops.object.modifier_apply(modifier=n.name)
def box(name,loc,dim,m,b=.0):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name;move(o);o.dimensions=dim
    active(o);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(m)
    if b:bevel(o,b,3)
    return o
def cyl(name,loc,r,depth,m,axis='Z',verts=32):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts,radius=r,depth=depth,location=loc)
    o=bpy.context.object;o.name=name;move(o)
    if axis=='X':o.rotation_euler.y=math.pi/2
    active(o);bpy.ops.object.transform_apply(location=False,rotation=True,scale=True);o.data.materials.append(m);bevel(o,.00025,1)
    return o
def join(obs,name,origin):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.join();o=obs[0];o.name=name
    scene.cursor.location=origin;bpy.ops.object.origin_set(type='ORIGIN_CURSOR');return o
body=box('Body',(0,0,.039),(.09,.036,.078),brass,.005)
# Cut genuine pockets so the wheels have room to turn, not black stickers.
for x in [-.023,0,.023]:
    cut=box('Pocket cutter',(x,-.016,.037),(.0168,.018,.037),dark,.002)
    active(body);mod=body.modifiers.new('Recess for number wheel','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cut
    bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cut,do_unlink=True)
parts=[body]
for x in [-.023,0,.023]:
    parts.append(box('Dark pocket backing',(x,-.0075,.037),(.0164,.002,.036),dark,.0015))
    # Small registration ticks, aligned with the selected digit.
    for sx in [-1,1]:parts.append(box('Code index',(x+sx*.0099,-.01825,.037),(.002,.00035,.00065),ink,.0001))
for x in [-.027,.027]:
    parts.append(cyl('Shackle socket',(x,0,.078),.0085,.0018,dark))
    parts.append(cyl('Socket brass lip',(x,0,.0787),.0076,.0015,edge))
# Back panel seam gives thickness and a manufactured rear face.
parts.append(box('Back cover',(0,.0178,.039),(.081,.0018,.068),brass,.003))
for x in [-.032,.032]:
    for z in [.014,.064]:
        o=cyl('Rear fastener',(x,.019,z),.0016,.0005,dark,verts=12);o.rotation_euler.x=math.pi/2;parts.append(o)
body=join(parts,'Lock_Body',(0,0,0))
# Continuous U-shaped tube, 18 arc segments and 12 vertices around the tube.
path=[Vector((-.027,0,.072)),Vector((-.027,0,.101))]
for k in range(1,19):
    a=math.pi-k*math.pi/18;path.append(Vector((.027*math.cos(a),0,.101+.027*math.sin(a))))
path.append(Vector((.027,0,.072)))
vs=[];fs=[];tube=.0061
for i,p in enumerate(path):
    tangent=(path[min(i+1,len(path)-1)]-path[max(i-1,0)]).normalized();u=Vector((0,1,0));v=tangent.cross(u).normalized()
    for j in range(12):vs.append(tuple(p+tube*(math.cos(j*math.tau/12)*u+math.sin(j*math.tau/12)*v)))
for i in range(len(path)-1):
    for j in range(12):a=i*12+j;b=i*12+(j+1)%12;fs.append((a,b,b+12,a+12))
fs.extend([tuple(reversed(range(12))),tuple((len(path)-1)*12+j for j in range(12))])
me=bpy.data.meshes.new('U shackle mesh');me.from_pydata(vs,[],fs);me.update()
shackle=bpy.data.objects.new('Lock_Shackle',me);asset.objects.link(shackle);me.materials.append(steel)
for p in me.polygons:p.use_smooth=True
active(shackle);scene.cursor.location=(.027,0,.073);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
shackle['opening']='Lift local Z by 0.022m, then rotate local Z around right-hand stem.'
# One complete decagonal number drum; each flat segment carries its own number.
r=.0168;width=.0126;center=Vector((-.023,-.0105,.037));vs=[];fs=[]
for x in [-width/2,width/2]:
    for k in range(10):
        a=(k-.5)*math.tau/10;vs.append((x,-r*math.cos(a),r*math.sin(a)))
for k in range(10):fs.append((k,(k+1)%10,(k+1)%10+10,k+10))
fs.extend([tuple(reversed(range(10))),tuple(range(10,20))])
me=bpy.data.meshes.new('Ten numbered facets');me.from_pydata(vs,[],fs);me.update()
wheel=bpy.data.objects.new('Wheel_1',me);asset.objects.link(wheel);wheel.location=center;me.materials.append(edge);bevel(wheel,.00022,1)
wparts=[wheel]
for sign in [-1,1]:wparts.append(cyl('Wheel dark rim',center+Vector((sign*(width/2+.0002),0,0)),r+.00015,.0007,dark,'X',30))
fontpath='C:/Windows/Fonts/arialbd.ttf'
font=bpy.data.fonts.load(fontpath) if os.path.exists(fontpath) else None
for k in range(10):
    a=k*math.tau/10;normal=Vector((0,-math.cos(a),math.sin(a)));up=Vector((0,math.sin(a),math.cos(a)))
    cu=bpy.data.curves.new('Digit %d'%k,'FONT');cu.body=str(k);cu.align_x='CENTER';cu.align_y='CENTER';cu.size=.0089;cu.resolution_u=2;cu.extrude=0
    if font:cu.font=font
    o=bpy.data.objects.new('Digit %d'%k,cu);asset.objects.link(o);o.location=center+normal*(r*math.cos(math.pi/10)+.00012);o.rotation_euler.x=math.pi/2-a;cu.materials.append(ink)
    active(o);bpy.ops.object.convert(target='MESH');wparts.append(bpy.context.object)
    # Slim border strokes and grip strips flank each digit on its facet.
    for s in [-1,1]:
        for offset,thick in [(.0044,.0003)]:
            o=box('Facet grip',center+normal*(r*math.cos(math.pi/10)+.00006)+up*(s*offset),(.009,.00015,thick),dark)
            o.rotation_euler.x=-a;wparts.append(o)
wheel=join(wparts,'Wheel_1',center)
wheel.rotation_mode='XYZ'
wheels=[wheel]
for i,x in [(2,0),(3,.023)]:
    o=wheel.copy();o.data=wheel.data.copy();asset.objects.link(o);o.name=f'Wheel_{i}';o.location.x=x;wheels.append(o)
root=bpy.data.objects.new('CombinationLock',None);asset.objects.link(root)
for o in [body,shackle]+wheels:o.parent=root
for o,k in zip(wheels,[7,3,9]):
    o.rotation_euler.x=k*math.tau/10
    o['digit_count']=10;o['step_degrees']=36;o['rotation_axis']='local X';o['zero_rotation_digit']=0;o['preview_digit']=k
    o['usage']='Set local X rotation to digit * 36 degrees. Digits are part of this mesh.'
root['size_m']='0.09 wide x 0.036 deep x 0.134 high'
# Studio lighting, kept outside the export collection.
ground=box('Studio floor',(0,0,-.004),(.8,.8,.006),mat('Backdrop',(.11,.125,.15),0,.8));move(ground,studio)
world=bpy.data.worlds.new('Studio World');scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.22,.24,.28,1);world.node_tree.nodes['Background'].inputs[1].default_value=.45
def aim(o,p):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
for name,loc,power,size in [('Key',(-.15,-.18,.25),14,.19),('Rim',(.13,.06,.20),20,.13),('Fill',(.16,-.16,.10),7,.16)]:
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);studio.objects.link(o);o.location=loc;aim(o,(0,0,.064))
camdata=bpy.data.cameras.new('Product camera');cam=bpy.data.objects.new('Product camera',camdata);studio.objects.link(cam);cam.location=(.15,-.40,.19);aim(cam,(0,-.002,.066));camdata.type='ORTHO';camdata.ortho_scale=.176;scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=1000;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG';scene.render.filepath=os.path.join(OUT,'CombinationLock_preview.png')
for a in bpy.context.screen.areas:
    if a.type=='VIEW_3D':a.spaces.active.region_3d.view_perspective='CAMERA'
active(body)
readme=bpy.data.texts.new('LOCK_README');readme.write('Combination lock / Кодовый замок\n\nWheel_1, Wheel_2, Wheel_3 are independent meshes. Numbers belong to each mesh.\nBlender local X: digit * 36 degrees, modulo 360. Rest angle 0 shows digit 0.\nPreview is 739; this is NOT a hard-coded winning combination.\nShackle is separate; lift then rotate about its right stem.\nExport only LOCK_ASSET, not the studio.\n')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'CombinationLock.blend'))
result={'file':bpy.data.filepath,'parts':[o.name for o in [body,shackle]+wheels],'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in [body,shackle]+wheels)}
