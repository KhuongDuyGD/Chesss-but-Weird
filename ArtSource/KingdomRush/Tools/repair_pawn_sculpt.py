"""Repair the un-decimated source before any face reorientation or simplification."""
import bpy,bmesh,numpy as np,json
from pathlib import Path
SRC=Path(__file__).resolve().parents[1];OUT=SRC/'Polish3D';name='01_Quan_tot_Denas'
bpy.ops.wm.open_mainfile(filepath=str(SRC/'Reconstructions'/(name+'_Reconstructed.blend')))
o=next(o for o in bpy.context.scene.objects if o.type=='MESH')
for other in list(bpy.context.scene.objects):
    if other!=o:bpy.data.objects.remove(other,do_unlink=True)
bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
o.data.remesh_voxel_size=.006;o.data.remesh_voxel_adaptivity=0;bpy.ops.object.voxel_remesh()
modifier=o.modifiers.new('Clean sculpt micro chatter','SMOOTH');modifier.factor=.14;modifier.iterations=3;bpy.ops.object.modifier_apply(modifier=modifier.name)
modifier=o.modifiers.new('Triangulate full coherent volume','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=modifier.name)
modifier=o.modifiers.new('Game-ready sculpture','DECIMATE');modifier.ratio=min(1,90000/len(o.data.polygons));bpy.ops.object.modifier_apply(modifier=modifier.name)
bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));defects=sum(not e.is_manifold for e in bm.edges);bm.to_mesh(o.data);bm.free();o.data.update()
for p in o.data.polygons:p.use_smooth=True
V=np.array([list(v.co) for v in o.data.vertices],np.float32);F=np.array([list(p.vertices) for p in o.data.polygons],np.int32);N=np.array([list(v.normal) for v in o.data.vertices],np.float32)
np.savez_compressed(OUT/'Geometry'/(name+'.npz'),vertices=V,faces=F,normals=N)
uv=OUT/'Geometry'/(name+'_UV.npz')
if uv.exists():uv.replace(OUT/'Geometry'/(name+'_RejectedNoisyUV.npz'))
report={'name':name,'triangles':len(F),'vertices':len(V),'nonmanifold_edges':defects,'dimensions':np.ptp(V,axis=0).tolist(),'geometry_source':'Full high resolution raw sculpture remeshed before simplification; rejected corrupted intermediate'}
(OUT/'Geometry'/(name+'_report.json')).write_text(json.dumps(report,indent=2));bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Work'/(name+'_Sculpt.blend')),compress=True)
print('PAWN_FULL_SOURCE_REPAIRED',json.dumps(report),flush=True)
