import bpy,numpy as np,json,sys,math,bmesh
from pathlib import Path
from mathutils import Vector
SRC=Path(__file__).resolve().parents[1];sys.path.insert(0,str(SRC))
import build_kingdom_rush as K
name=sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else '06_Quan_vua_Denas'
raw=sorted((SRC/'Reconstructions/Raw').glob(name+'*.glb'))[-1]
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(raw));obs=[o for o in bpy.context.scene.objects if o.type=='MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in obs:o.select_set(True)
bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.join();o=bpy.context.object;o.name=name+' • Reconstructed volume'
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
vv=np.array([list(v.co) for v in o.data.vertices],np.float32);zz=vv[:,2];hh=np.ptp(zz)
bottom=vv[zz<zz.min()+hh*.012];above=vv[(zz>zz.min()+hh*.03)&(zz<zz.min()+hh*.05)]
floor_detected=len(above)>0 and np.max(np.ptp(bottom[:,:2],axis=0)/np.maximum(np.ptp(above[:,:2],axis=0),.001))>1.13
if floor_detected:
    # An inferred thin ground sheet is not part of the chess miniature.
    cut=float(zz.min()+hh*.012)
    bm=bmesh.new();bm.from_mesh(o.data)
    result=bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,plane_co=(0,0,cut),plane_no=(0,0,1),clear_inner=True,clear_outer=False)
    border=[e for e in bm.edges if e.is_boundary and max(abs(v.co.z-cut) for v in e.verts)<.00001]
    if border:bmesh.ops.holes_fill(bm,edges=border,sides=0)
    bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
    print('INFERRED_GROUND_SHEET_REMOVED',name,flush=True)
lo,hi=K.bounds([o]);center=(lo+hi)/2
for v in o.data.vertices:v.co-=center
o.data.materials.clear();o.data.materials.append(K.mat('Reconstruction • clay',(.65,.66,.70),0,.68))
for p in o.data.polygons:p.use_smooth=True
full_tri=len(o.data.polygons)
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.quads_convert_to_tris();bpy.ops.object.mode_set(mode='OBJECT')
verts=np.array([list(v.co) for v in o.data.vertices],np.float32);faces=np.array([list(p.vertices) for p in o.data.polygons],np.int32)
np.savez_compressed(SRC/'Reconstructions'/(name+'_geometry.npz'),vertices=verts,faces=faces)
copy=o.copy();copy.data=o.data.copy();bpy.context.scene.collection.objects.link(copy);bpy.context.view_layer.objects.active=copy
bm=bmesh.new();bm.from_mesh(copy.data);bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=.00005);bm.to_mesh(copy.data);bm.free();copy.data.update()
mod=copy.modifiers.new('Camera-fitting proxy','DECIMATE');mod.ratio=min(1,10000/len(copy.data.polygons));bpy.ops.object.modifier_apply(modifier=mod.name)
np.savez_compressed(SRC/'Reconstructions'/(name+'_proxy.npz'),vertices=np.array([list(v.co) for v in copy.data.vertices],np.float32),faces=np.array([list(p.vertices) for p in copy.data.polygons],np.int32))
bpy.data.objects.remove(copy,do_unlink=True)
s=bpy.context.scene;K.render_settings(s,900,900,True)
h=max(hi-lo)
K.light(s,'Key',(-3,-4,6),420,(1,.90,.78),4)
K.light(s,'Fill',(4,-1,3),260,(.65,.80,1),4)
K.light(s,'Rim',(0,4,5),360,(1,.89,.75),3)
out=SRC/'Reconstructions/Checks';out.mkdir(exist_ok=True)
for view,eye in [('front',(0,-4,0.5)),('back',(0,4,.5)),('left',(-4,0,.5)),('three_quarter',(2.4,-4,1.2))]:
    K.camera(s,eye,(0,0,0),h*1.2,'Camera • '+view)
    s.render.filepath=str(out/(name+'_clay_'+view+'.png'));bpy.ops.render.render(write_still=True)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Reconstructions'/(name+'_Reconstructed.blend')),compress=True)
print('RECONSTRUCTION_INSPECTED',name,'dims',list(hi-lo),'triangles',len(faces),flush=True)
