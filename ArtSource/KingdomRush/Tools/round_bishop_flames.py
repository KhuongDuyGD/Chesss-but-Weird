"""Give the green staff flames physical thickness while preserving front contours."""
import bpy,bmesh,numpy as np,json
from pathlib import Path
SRC=Path(__file__).resolve().parents[1];G=SRC/'Polish3D/Geometry';name='04_Quan_tuong_Veznan';D=np.load(G/(name+'_UV.npz'));V=D['vertices'].copy();F=D['faces'];UV=D['uv']
mask=V[:,0]>.25;t=np.clip((V[:,2]-.68)/.14,0,1);t=t*t*(3-2*t);V[mask,1]=.25+(V[mask,1]-.25)*(1+t[mask]*1.0)
bpy.ops.wm.read_factory_settings(use_empty=True);m=bpy.data.meshes.new(name);m.from_pydata(V.tolist(),[],F.tolist());m.update();o=bpy.data.objects.new(name,m);bpy.context.scene.collection.objects.link(o)
bm=bmesh.new();bm.from_mesh(m);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));defects=sum(not e.is_manifold for e in bm.edges);bm.to_mesh(m);bm.free();m.update();N=np.array([list(v.normal) for v in m.vertices],np.float32)
for p in m.polygons:p.use_smooth=True
np.savez_compressed(G/(name+'.npz'),vertices=V,faces=F,normals=N);np.savez_compressed(G/(name+'_UV.npz'),vertices=V,faces=F,normals=N,uv=UV)
report=json.loads((G/(name+'_report.json')).read_text());report['flames']='Staff flame depth doubled through a smooth transition; front contours and UV islands preserved';report['dimensions']=np.ptp(V,axis=0).tolist();report['nonmanifold_edges']=defects;(G/(name+'_report.json')).write_text(json.dumps(report,indent=2))
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Polish3D/Work'/(name+'_Sculpt.blend')),compress=True)
print('BISHOP_FLAMES_ROUNDED',defects,flush=True)
