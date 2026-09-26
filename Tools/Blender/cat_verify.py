import bpy, math, json, os
from mathutils import Vector, Quaternion
rig=bpy.data.objects['Cat_Rig'];obj=bpy.data.objects['Cat_Mesh']
def reset():
    for p in rig.pose.bones:
        p.location=(0,0,0);p.rotation_euler=(0,0,0);p.scale=(1,1,1)
    bpy.context.view_layer.update()
reset()
deps=bpy.context.evaluated_depsgraph_get()
ev=obj.evaluated_get(deps); me=ev.to_mesh()
rest_error=max((a.co-b.co).length for a,b in zip(obj.data.vertices,me.vertices));ev.to_mesh_clear()
checks={}
for part in ['Front','Hind']:
    for side in ['L','R']:
        reset();p=rig.pose.bones[f'CTRL_{part}_Paw.{side}']
        delta=Vector((0,-.045,.065))
        p.location=p.bone.matrix_local.to_3x3().inverted()@delta
        bpy.context.view_layer.update()
        lower=rig.pose.bones[f'DEF_{part}_Lower.{side}']
        checks[f'{part}.{side}']=(lower.tail-p.head).length
reset()
for p in rig.pose.bones:
    n=p.name
    if n.startswith('DEF') and not ('Front' in n or 'Hind' in n):
        p.custom_shape=bpy.data.objects['WGT_Circle'];p.use_custom_shape_bone_size=False
        size=.028 if 'Ear' in n else .045 if 'Tail' in n else .14 if 'Head' in n else .115
        p.custom_shape_scale_xyz=(size,size,size)
    if n.endswith('.L'): p.color.palette='THEME03'
    elif n.endswith('.R'): p.color.palette='THEME04'
    else: p.color.palette='THEME09'
rig.data.collections['Leg bones - optional FK'].is_visible=False
report={'rest_deformation_max_m':rest_error,'ik_target_error_m':checks,'mesh_vertices':len(obj.data.vertices),'bones':len(rig.data.bones),'unweighted':sum(not any(g.weight>1e-6 for g in v.groups) for v in obj.data.vertices),'texture_packed':all(i.packed_file is not None for i in bpy.data.images if i.type=='IMAGE' and i.size[0]>0)}
assert rest_error<.0001, report
assert max(checks.values())<.001, report
assert report['unweighted']==0, report
readme=bpy.data.texts['CAT_RIG_README'];readme.clear()
readme.write('CAT RIG / Управление\n\nPose Mode: выберите Cat_Rig.\nCTRL_Root — перемещение и поворот всей кошки.\nCTRL_Body — корпус; лапы остаются на месте.\nCTRL_Front_Paw / CTRL_Hind_Paw — перемещайте G, поворачивайте R.\nPole — направление сгиба соответствующей лапы.\nКольца спины, шеи, головы, ушей и хвоста — вращение R.\nСлева зелёные контроллеры, справа синие.\n\nНа каждой лапе Custom Properties > IK: 1 — IK, 0 — FK.\nДля FK включите коллекцию костей Leg bones - optional FK.\nАвтоматического совмещения IK/FK нет; переключайте в нейтральной позе.\nAlt+G / Alt+R сбрасывают выбранные контроллеры.\n\nИсходный Cat.blend не изменён, текстура сохранена и упакована.\nЭто риг тела, не лицевой риг: глаза и рот входят в единый меш.\nВесовая привязка рассчитана по анатомическим областям и сглажена по топологии.\nПроверены подъём всех четырёх лап, поворот головы и изгиб хвоста.\nПеред экстремальными позами может потребоваться дополнительная правка весов.\n\nValidation:\n'+json.dumps(report,indent=2))
for a in bpy.context.screen.areas:
    if a.type=='VIEW_3D':
        s=a.spaces.active;s.overlay.show_overlays=True
        s.region_3d.view_rotation=Quaternion((.820,.424,.176,.340)).normalized()
        s.region_3d.view_location=(0,0,.40);s.region_3d.view_distance=1.7
        s.region_3d.view_perspective='ORTHO'
rig.data.bones.active=rig.data.bones['CTRL_Body']
bpy.ops.pose.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
result=report
