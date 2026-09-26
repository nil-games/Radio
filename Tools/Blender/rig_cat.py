import bpy, math, os, json
from mathutils import Vector, Quaternion

source = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
assert not any(o.type == 'ARMATURE' for o in bpy.context.scene.objects), 'Existing rig: inspect before replacing'
dest = os.path.join(os.path.dirname(bpy.data.filepath), 'Cat_Rigged.blend')
assert not os.path.exists(dest), 'Output exists; do not overwrite'
bpy.ops.wm.save_as_mainfile(filepath=dest)
source.name = 'Cat_Mesh'
for m in list(source.modifiers):
    if m.type == 'ARMATURE' and m.object is None: source.modifiers.remove(m)
source.vertex_groups.clear()
arm = bpy.data.armatures.new('Cat_Skeleton')
rig = bpy.data.objects.new('Cat_Rig', arm)
bpy.context.collection.objects.link(rig)
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
spec = {}
def bone(name, head, tail, parent=None, deform=True):
    b = arm.edit_bones.new(name); b.head=head; b.tail=tail; b.use_deform=deform
    if parent: b.parent=arm.edit_bones[parent]
    spec[name]=(Vector(head),Vector(tail))
    return b
bone('CTRL_Root',(0,0,0),(0,0,.15),deform=False)
bone('CTRL_Body',(0,.02,.40),(0,.02,.53),'CTRL_Root',False)
bone('DEF_Pelvis',(0,.14,.40),(0,.03,.43),'CTRL_Body')
bone('DEF_Spine',(0,.03,.43),(0,-.13,.45),'DEF_Pelvis')
bone('DEF_Chest',(0,-.13,.45),(0,-.27,.48),'DEF_Spine')
bone('DEF_Neck',(0,-.27,.48),(0,-.35,.61),'DEF_Chest')
bone('DEF_Head',(0,-.35,.61),(0,-.38,.73),'DEF_Neck')
for sign,side in [(1,'L'),(-1,'R')]:
    bone('DEF_Ear.'+side,(sign*.092,-.348,.724),(sign*.133,-.351,.808),'DEF_Head')
tail=[(0,.205,.445),(0,.275,.38),(0,.31,.29),(0,.353,.20),(0,.410,.128),(0,.466,.098),(0,.485,.127)]
for i in range(len(tail)-1):
    bone('DEF_Tail_%02d'%i,tail[i],tail[i+1],'DEF_Pelvis' if i==0 else 'DEF_Tail_%02d'%(i-1))
limbs=[]
for sign,side in [(1,'L'),(-1,'R')]:
    for part,x,pts,parent in [
        ('Front',.107,[(-.255,.445),(-.248,.265),(-.304,.073),(-.355,.027)],'DEF_Chest'),
        ('Hind',.174,[(.13,.405),(.075,.275),(.173,.13),(.100,.029)],'DEF_Pelvis')]:
        names=[]
        for i,label in enumerate(['Upper','Lower','Paw']):
            name=f'DEF_{part}_{label}.{side}'
            bone(name,(sign*x,*pts[i]),(sign*x,*pts[i+1]),parent if i==0 else names[-1])
            names.append(name)
        ctrl=f'CTRL_{part}_Paw.{side}'
        bone(ctrl,(sign*x,*pts[2]),(sign*x,*pts[3]),'CTRL_Root',False)
        pole=f'CTRL_{part}_Pole.{side}'
        py=pts[1][0]+(.30 if part=='Front' else -.30)
        bone(pole,(sign*x,py,pts[1][1]),(sign*x,py,pts[1][1]+.06),'CTRL_Root',False)
        limbs.append((part,side,names,ctrl,pole))
bpy.ops.object.mode_set(mode='OBJECT')
rig.show_in_front=True
arm.display_type='OCTAHEDRAL'
controls=arm.collections.new('Controls - IK and Body')
fk=arm.collections.new('FK - Spine Head Tail Ears')
legs=arm.collections.new('Leg bones - optional FK')
for b in arm.bones:
    (controls if b.name.startswith('CTRL') else legs if any(x in b.name for x in ['Front','Hind']) else fk).assign(b)
    b.color.palette='THEME04' if b.name.startswith('CTRL') else 'THEME03'

# Heat weighting sees only the anatomical deform bones, never controllers.
bpy.ops.object.select_all(action='DESELECT')
source.select_set(True); rig.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
modifier=next(m for m in source.modifiers if m.type=='ARMATURE')
modifier.use_deform_preserve_volume=True
deforms=[b for b in arm.bones if b.use_deform]
def segment_dist(v,b):
    a,c=spec[b.name]; d=c-a; t=max(0,min(1,(v-a).dot(d)/d.length_squared))
    return (v-a-t*d).length
unweighted=[]
for v in source.data.vertices:
    if not any(g.weight>1e-7 for g in v.groups):
        unweighted.append(v.index)
        near=sorted(deforms,key=lambda b:segment_dist(v.co,b))[:3]
        ws=[1/max(segment_dist(v.co,b),.006)**4 for b in near]
        for b,w in zip(near,ws): source.vertex_groups[b.name].add([v.index],w/sum(ws),'REPLACE')

# Keep the face rigid under head turns, blend at the neck and preserve ear weights.
for v in source.data.vertices:
    x,y,z=v.co
    if y<-.25 and z>.60:
        alpha=max(0,min(1,(z-.60)/.065))
        current={source.vertex_groups[g.group].name:g.weight for g in v.groups}
        ears={n:w for n,w in current.items() if n.startswith('DEF_Ear')}
        ear_sum=sum(ears.values())
        for n,w in current.items():
            if n not in ears: source.vertex_groups[n].add([v.index],w*(1-alpha),'REPLACE')
        head=current.get('DEF_Head',0)*(1-alpha)+alpha*(1-ear_sum)
        source.vertex_groups['DEF_Head'].add([v.index],head,'REPLACE')

# IK controls remain fixed in root space while the torso moves.
for part,side,names,ctrl,pole in limbs:
    lower=rig.pose.bones[names[1]]
    ik=lower.constraints.new('IK'); ik.name='Paw IK'; ik.target=rig; ik.subtarget=ctrl
    ik.chain_count=2; ik.use_stretch=False; ik.pole_target=rig; ik.pole_subtarget=pole
    for n in names[:2]: rig.pose.bones[n].ik_stretch=0
    target_positions={n:spec[n] for n in names[:2]}
    def error(angle):
        ik.pole_angle=angle; bpy.context.view_layer.update()
        return sum((rig.pose.bones[n].head-h).length_squared+(rig.pose.bones[n].tail-t).length_squared for n,(h,t) in target_positions.items())
    angle=min([i*math.pi/36 for i in range(-36,37)],key=error)
    for delta in [.01,.001,.0001]:
        angle=min([angle+i*delta for i in range(-10,11)],key=error)
    ik.pole_angle=angle
    paw=rig.pose.bones[names[2]]
    cr=paw.constraints.new('COPY_ROTATION'); cr.name='Paw orientation'; cr.target=rig; cr.subtarget=ctrl
    p=rig.pose.bones[ctrl]; p['IK']=1.0
    p.id_properties_ui('IK').update(min=0.0,max=1.0,description='1 = paw IK, 0 = manually rotate leg bones in FK')
    for con in [ik,cr]:
        drv=con.driver_add('influence').driver; drv.type='AVERAGE'
        var=drv.variables.new();var.name='ik';var.type='SINGLE_PROP'
        var.targets[0].id=rig;var.targets[0].data_path=f'pose.bones["{ctrl}"]["IK"]'

widgets=bpy.data.collections.new('Rig Widgets'); bpy.context.scene.collection.children.link(widgets)
def widget(name,kind):
    me=bpy.data.meshes.new(name)
    if kind=='circle': verts=[(math.cos(i*math.tau/32),0,math.sin(i*math.tau/32)) for i in range(32)]
    elif kind=='diamond': verts=[(1,0,0),(0,1,0),(-1,0,0),(0,-1,0),(0,0,1),(0,0,-1)]
    else: verts=[(-1,0,-.65),(1,0,-.65),(1,0,1.5),(.5,0,2),(-.5,0,2),(-1,0,1.5)]
    edges=[(i,(i+1)%len(verts)) for i in range(len(verts))]
    me.from_pydata(verts,edges,[]); me.update()
    o=bpy.data.objects.new(name,me);widgets.objects.link(o);o.hide_render=True;o.hide_set(True)
    return o
circle=widget('WGT_Circle','circle'); diamond=widget('WGT_Diamond','diamond'); foot=widget('WGT_Paw','foot')
for p in rig.pose.bones:
    p.rotation_mode='XYZ'
    if not p.name.startswith('CTRL'): continue
    p.custom_shape = diamond if 'Pole' in p.name else foot if 'Paw' in p.name else circle
    p.use_custom_shape_bone_size=False
    size=.035 if 'Pole' in p.name else .07 if 'Paw' in p.name else .30 if 'Root' in p.name else .20
    p.custom_shape_scale_xyz=(size,size,size)
    p.lock_scale=(True,True,True)
    if 'Pole' in p.name: p.lock_rotation=(True,True,True)
for p in rig.pose.bones:
    if p.name.startswith('DEF'):
        p.lock_location=(True,True,True);p.lock_scale=(True,True,True)
rig['README']='Pose Mode: Root moves whole cat; Body moves torso, feet remain planted. Rotate spine/head/ears/tail bones. Move paw controls for IK; move poles to set bend direction. Per-paw IK slider: 1 IK / 0 manual FK. No automatic IK/FK snapping.'
readme=bpy.data.texts.new('CAT_RIG_README')
readme.write(rig['README']+'\n\nSource mesh and packed texture preserved. Face has rigid head weighting, no facial animation or separate eyes.\nGenerated quadruped rig; test extreme poses before production.\n')
bpy.context.scene.frame_set(1)
bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=120
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='POSE')
for a in bpy.context.screen.areas:
    if a.type=='VIEW_3D':
        s=a.spaces.active;s.region_3d.view_rotation=Quaternion((.5,.5,.5,.5));s.region_3d.view_location=(0,0,.4);s.region_3d.view_distance=1.65;s.region_3d.view_perspective='ORTHO'
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=dest)
result={'saved':dest,'bones':len(arm.bones),'deform_bones':len(deforms),'unweighted_fixed':len(unweighted),'weights_missing':sum(not any(g.weight>1e-6 for g in v.groups) for v in source.data.vertices),'ik_rest_error':{n: (rig.pose.bones[n].tail-spec[n][1]).length for _,_,ns,_,_ in limbs for n in ns[:2]}}
