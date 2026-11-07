import bpy,bmesh,numpy as np,json,sys
from pathlib import Path
SRC=Path(__file__).resolve().parents[1];OUT=SRC/'Polish3D';name=sys.argv[sys.argv.index('--')+1];D=np.load(OUT/'Geometry'/(name+'_Solid.npz'))
bpy.ops.wm.read_factory_settings(use_empty=True);m=bpy.data.meshes.new(name);m.from_pydata(D['vertices'].tolist(),[],D['faces'].tolist());m.update();o=bpy.data.objects.new(name,m);bpy.context.scene.collection.objects.link(o);o.select_set(True);bpy.context.view_layer.objects.active=o
modifier=o.modifiers.new('Smooth coherent outer sculpture','SMOOTH');modifier.factor=.55;modifier.iterations=10;bpy.ops.object.modifier_apply(modifier=modifier.name)
modifier=o.modifiers.new('Sculpture delivery budget','DECIMATE');modifier.ratio=min(1,90000/len(m.polygons));bpy.ops.object.modifier_apply(modifier=modifier.name);m=o.data
bm=bmesh.new();bm.from_mesh(m);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));defects=sum(not e.is_manifold for e in bm.edges);bm.to_mesh(m);bm.free();m.update()
for p in m.polygons:p.use_smooth=True
V=np.array([list(v.co) for v in m.vertices],np.float32);F=np.array([list(p.vertices) for p in m.polygons],np.int32);N=np.array([list(v.normal) for v in m.vertices],np.float32)
np.savez_compressed(OUT/'Geometry'/(name+'.npz'),vertices=V,faces=F,normals=N)
uv=OUT/'Geometry'/(name+'_UV.npz')
if uv.exists():uv.replace(OUT/'Geometry'/(name+'_RejectedNoisyUV.npz'))
report={'name':name,'triangles':len(F),'vertices':len(V),'nonmanifold_edges':defects,'dimensions':np.ptp(V,axis=0).tolist(),'geometry_source':'Coherent filled volumetric reconstruction from original full surface points; no winding-dependent surface-net artifacts'}
(OUT/'Geometry'/(name+'_report.json')).write_text(json.dumps(report,indent=2));bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Work'/(name+'_Sculpt.blend')),compress=True)
print('SOLID_SCULPT_FINALIZED',json.dumps(report),flush=True)
