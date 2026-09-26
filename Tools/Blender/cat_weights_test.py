import bpy, math, numpy as np
from mathutils import Quaternion
rig=bpy.data.objects['Cat_Rig'];obj=bpy.data.objects['Cat_Mesh']
bones=[b for b in rig.data.bones if b.use_deform];names=[b.name for b in bones]
coords=np.array([v.co[:] for v in obj.data.vertices],dtype=float)
N=len(coords); weights=np.zeros((N,len(bones)))
for j,b in enumerate(bones):
    h=np.array(b.head_local);d=np.array(b.tail_local)-h
    t=np.clip(((coords-h)*d).sum(axis=1)/(d*d).sum(),0,1)
    dist=np.linalg.norm(coords-h-t[:,None]*d,axis=1)
    weights[:,j]=1/np.maximum(dist,.012)**4
for i,(x,y,z) in enumerate(coords):
    side='L' if x>0 else 'R'
    allowed=None
    if y>.24 and abs(x)<.095: allowed=[n for n in names if 'Tail' in n or (y<.285 and n=='DEF_Pelvis')]
    elif z<.24 and y<-.14: allowed=[n for n in names if 'Front' in n and n.endswith('.'+side)]
    elif z<.25 and y>-.12: allowed=[n for n in names if 'Hind' in n and n.endswith('.'+side)]
    elif z>.64 and y<-.23: allowed=[n for n in names if n=='DEF_Head' or n.startswith('DEF_Ear')]
    if allowed:
        weights[i,[j for j,n in enumerate(names) if n not in allowed]]=0
    # Avoid skin crossing between opposite limbs.
    if abs(x)>.04:
        for j,n in enumerate(names):
            if ('Front' in n or 'Hind' in n) and not n.endswith('.'+side): weights[i,j]=0
weights/=weights.sum(axis=1)[:,None]
edges=np.array([e.vertices[:] for e in obj.data.edges],dtype=int)
degree=np.bincount(edges.flatten(),minlength=N)
base=weights.copy()
for step in range(16):
    avg=np.zeros_like(weights)
    np.add.at(avg,edges[:,0],weights[edges[:,1]])
    np.add.at(avg,edges[:,1],weights[edges[:,0]])
    avg/=np.maximum(degree,1)[:,None]
    weights=.35*base+.65*avg
weights[weights<.012]=0
weights/=weights.sum(axis=1)[:,None]
for vg in obj.vertex_groups: vg.remove(list(range(N)))
for j,n in enumerate(names):
    vg=obj.vertex_groups.get(n) or obj.vertex_groups.new(name=n)
    for i in np.where(weights[:,j]>0)[0]: vg.add([int(i)],float(weights[i,j]),'REPLACE')
# A non-destructive test pose. No keyframes are written to the user's animation.
rig.pose.bones['CTRL_Body'].location.y=-.035
rig.pose.bones['DEF_Head'].rotation_euler.y=math.radians(15)
rig.pose.bones['DEF_Tail_02'].rotation_euler.x=math.radians(14)
rig.pose.bones['DEF_Tail_03'].rotation_euler.x=math.radians(12)
p=rig.pose.bones['CTRL_Front_Paw.L']
p.location=p.bone.matrix_local.to_3x3().inverted() @ __import__('mathutils').Vector((0,-.055,.07))
bpy.context.view_layer.update()
for a in bpy.context.screen.areas:
    if a.type=='VIEW_3D':
        a.spaces.active.region_3d.view_rotation=Quaternion((.5,.5,.5,.5))
        a.spaces.active.overlay.show_overlays=False
result={'weighted_vertices':N,'finite':bool(np.isfinite(weights).all()),'max_sum_error':float(np.abs(weights.sum(axis=1)-1).max()),'test_pose':'body lowered, head turned, left front paw raised, tail bent'}
