"""Prepare a genuine volumetric mesh and visibility data for original-image projection."""
import bpy,numpy as np,json,sys,bmesh
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
SRC=Path(__file__).resolve().parents[1]
name=sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else '06_Quan_vua_Denas'
fit=json.loads((SRC/'Reconstructions'/(name+'_camera_fit.json')).read_text())
bpy.ops.wm.open_mainfile(filepath=str(SRC/'Reconstructions'/(name+'_Reconstructed.blend')))
o=next(o for o in bpy.context.scene.objects if o.type=='MESH')
bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
bm=bmesh.new();bm.from_mesh(o.data)
bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=.00005)
bmesh.ops.dissolve_degenerate(bm,edges=bm.edges,dist=.000001)
bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
bm.to_mesh(o.data);bm.free();o.data.update()
mod=o.modifiers.new('Preserve sculpt silhouette • game mesh','DECIMATE');mod.ratio=min(1,80000/len(o.data.polygons));bpy.ops.object.modifier_apply(modifier=mod.name)
if len(o.data.polygons)>88000:
    # Some surface-net outputs contain intersecting shells that resist edge
    # collapse. Voxel remeshing produces one coherent editable volume first.
    o.data.remesh_voxel_size=.008
    o.data.remesh_voxel_adaptivity=0
    bpy.ops.object.voxel_remesh()
    mod=o.modifiers.new('Triangulate coherent volume','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
    mod=o.modifiers.new('Game mesh budget','DECIMATE');mod.ratio=min(1,80000/len(o.data.polygons));bpy.ops.object.modifier_apply(modifier=mod.name)
if len(o.data.polygons)>88000:raise RuntimeError('Mesh triangle budget could not be reached')
bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(o.data);bm.free();o.data.update()
v=np.array([list(v.co) for v in o.data.vertices],np.float32);f=np.array([list(p.vertices) for p in o.data.polygons],np.int32)
eye=Vector(fit['eye_direction']);bvh=BVHTree.FromPolygons(v.tolist(),f.tolist(),all_triangles=True)
visible=[]
for p in o.data.polygons:
    if p.normal.dot(eye)<-.04:visible.append(False);continue
    pt=p.center;hit,n,idx,d=bvh.ray_cast(pt+eye*5,-eye,6)
    visible.append(hit is not None and (hit-pt).length<.006)
np.savez_compressed(SRC/'Reconstructions'/(name+'_surface.npz'),vertices=v,faces=f,visible=np.array(visible))
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Reconstructions'/(name+'_Surface.blend')),compress=True)
print('SURFACE_PREPARED',name,len(v),len(f),'visible_faces',sum(visible),flush=True)
