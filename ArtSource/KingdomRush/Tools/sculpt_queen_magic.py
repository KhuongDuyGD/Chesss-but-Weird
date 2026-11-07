"""Replace the reconstruction's flat flame slabs with curved volumetric wisps."""
import bpy,bmesh,numpy as np,json,math
from pathlib import Path
SRC=Path(__file__).resolve().parents[1];OUT=SRC/'Polish3D';name='05_Quan_hau_Veznan'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'Work'/(name+'_Sculpt.blend')));o=next(o for o in bpy.context.scene.objects if o.type=='MESH')
bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
bm=bmesh.new();bm.from_mesh(o.data)
remove=[v for v in bm.verts if (v.co.x>.40 and v.co.z>.62) or (v.co.x<-.41 and v.co.y<.12 and v.co.z>.24)]
bmesh.ops.delete(bm,geom=remove,context='VERTS');border=[e for e in bm.edges if e.is_boundary]
if border:bmesh.ops.holes_fill(bm,edges=border,sides=0)
bmesh.ops.triangulate(bm,faces=list(bm.faces));bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
parts=[o]
for base,top in [((.59,.35,.60),.985),((-.59,-.11,.27),.70)]:
    x,y,z=base
    for branch in range(3):
        height=(top-z)*(1 if branch==0 else .76);offset=(branch-1)*.09
        for i in range(22):
            t=i/21;radius=(.105 if branch==0 else .07)*(1-t)**.7+.004
            xx=x+offset+(.05 if branch==0 else .07)*math.sin(t*5.2+branch*.6)
            yy=y+.04*math.sin(t*5+branch*2)
            bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=8,radius=1,location=(xx,yy,z+t*height));p=bpy.context.object;p.scale=(radius,radius*.9,radius*1.3);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);parts.append(p)
bpy.ops.object.select_all(action='DESELECT')
for p in parts:p.select_set(True)
bpy.context.view_layer.objects.active=o;bpy.ops.object.join();o.data.remesh_voxel_size=.0055;o.data.remesh_voxel_adaptivity=0;bpy.ops.object.voxel_remesh()
modifier=o.modifiers.new('Smooth flowing magic sculpture','SMOOTH');modifier.factor=.30;modifier.iterations=5;bpy.ops.object.modifier_apply(modifier=modifier.name)
modifier=o.modifiers.new('Triangulate flowing sculpture','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=modifier.name)
modifier=o.modifiers.new('Sculpture budget','DECIMATE');modifier.ratio=min(1,90000/len(o.data.polygons));bpy.ops.object.modifier_apply(modifier=modifier.name)
bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));defects=sum(not e.is_manifold for e in bm.edges);bm.to_mesh(o.data);bm.free();o.data.update()
for p in o.data.polygons:p.use_smooth=True
V=np.array([list(v.co) for v in o.data.vertices],np.float32);F=np.array([list(p.vertices) for p in o.data.polygons],np.int32);N=np.array([list(v.normal) for v in o.data.vertices],np.float32)
np.savez_compressed(OUT/'Geometry'/(name+'.npz'),vertices=V,faces=F,normals=N)
uv=OUT/'Geometry'/(name+'_UV.npz')
if uv.exists():uv.replace(OUT/'Geometry'/(name+'_BeforeMagicUV.npz'))
report={'name':name,'triangles':len(F),'vertices':len(V),'nonmanifold_edges':defects,'dimensions':np.ptp(V,axis=0).tolist(),'geometry_source':'Full sculpture with genuinely volumetric curved magic, replacing flat reconstructed slabs'}
(OUT/'Geometry'/(name+'_report.json')).write_text(json.dumps(report,indent=2));bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Work'/(name+'_Sculpt.blend')),compress=True)
print('QUEEN_VOLUMETRIC_MAGIC_SCULPTED',json.dumps(report),flush=True)
