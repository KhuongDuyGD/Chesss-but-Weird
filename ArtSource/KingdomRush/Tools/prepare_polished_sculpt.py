"""Prepare coherent, fully volumetric sculpt geometry; no silhouette extrusion."""
import bpy,bmesh,numpy as np,json,sys
from pathlib import Path
SRC=Path(__file__).resolve().parents[1];OUT=SRC/'Polish3D';(OUT/'Geometry').mkdir(parents=True,exist_ok=True);(OUT/'Work').mkdir(exist_ok=True)
names=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else ['06_Quan_vua_Denas']
for name in names:
    bpy.ops.wm.open_mainfile(filepath=str(SRC/'Reconstructions'/(name+'_Surface.blend')))
    o=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    for other in list(bpy.context.scene.objects):
        if other!=o:bpy.data.objects.remove(other,do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.000001);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    defects=sum(not e.is_manifold for e in bm.edges);bm.to_mesh(o.data);bm.free()
    if defects:
        o.data.remesh_voxel_size=.007;o.data.remesh_voxel_adaptivity=0;bpy.ops.object.voxel_remesh()
        modifier=o.modifiers.new('Coherent sculpt triangulation','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=modifier.name)
    modifier=o.modifiers.new('Remove reconstruction surface chatter','SMOOTH');modifier.factor=.12;modifier.iterations=2;bpy.ops.object.modifier_apply(modifier=modifier.name)
    if len(o.data.polygons)>90000:
        modifier=o.modifiers.new('Preserve 3D sculpture • delivery budget','DECIMATE');modifier.ratio=90000/len(o.data.polygons);bpy.ops.object.modifier_apply(modifier=modifier.name)
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
    for p in o.data.polygons:p.use_smooth=True
    o.name=name
    v=np.array([list(v.co) for v in o.data.vertices],np.float32);f=np.array([list(p.vertices) for p in o.data.polygons],np.int32)
    n=np.array([list(v.normal) for v in o.data.vertices],np.float32)
    np.savez_compressed(OUT/'Geometry'/(name+'.npz'),vertices=v,faces=f,normals=n)
    bm=bmesh.new();bm.from_mesh(o.data);remaining=sum(not e.is_manifold for e in bm.edges);bm.free()
    report={'name':name,'triangles':len(f),'vertices':len(v),'nonmanifold_edges':remaining,'original_surface_defects':defects,'dimensions':np.ptp(v,axis=0).tolist(),'geometry_source':'Full reconstructed 3D sculpt; no depth-sheet or contour-extruded replacement'}
    (OUT/'Geometry'/(name+'_report.json')).write_text(json.dumps(report,indent=2))
    bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Work'/(name+'_Sculpt.blend')),compress=True)
    print('POLISHED_SCULPT_PREPARED',json.dumps(report),flush=True)
