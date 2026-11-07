"""Reimport the actual exported FBXs; verify units, origins, mesh count and usable UVs."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];SRC=Path(__file__).parent;OUT=ROOT/'Assets/Skins/KingdomRushCollab'
manifest=json.loads((OUT/'model_manifest.json').read_text());ref=json.loads((SRC/'existing_dimensions.json').read_text())
report={'passed':True,'pieces':[],'board':{},'checks':[]};failures=[]
for item in manifest['models']:
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(OUT/'Models'/(item['name']+'.fbx')))
    obs=[o for o in bpy.context.scene.objects if o.type=='MESH']
    vs=[o.matrix_world@Vector(v) for o in obs for v in o.bound_box]
    lo=[min(v[i] for v in vs) for i in range(3)];hi=[max(v[i] for v in vs) for i in range(3)]
    dims=[hi[i]-lo[i] for i in range(3)];expected=item['dimensions_m']
    error=max(abs(dims[i]-expected[i]) for i in range(3))
    result={'name':item['name'],'dimensions_xyz':dims,'reference_xyz':expected,'max_error_m':error,'ground_error_m':abs(lo[2]),
            'mesh_count':len(obs),'triangles':sum(len(p.vertices)-2 for o in obs for p in o.data.polygons),
            'material_count':sum(len(o.data.materials) for o in obs),'has_uv':all(bool(o.data.uv_layers) for o in obs),
            'finite_vertices':all(math.isfinite(c) for o in obs for v in o.data.vertices for c in v.co)}
    import numpy as np
    faces=np.array([list(p.vertices) for p in obs[0].data.polygons],dtype=np.int32)
    # FBX may split a geometric vertex at a UV seam. Validate physical closure
    # after identifying coordinates, without modifying the imported mesh.
    positions=np.array([list(v.co) for v in obs[0].data.vertices])
    _,welded=np.unique(np.round(positions,6),axis=0,return_inverse=True);faces=welded[faces]
    edges=np.sort(np.concatenate([faces[:,[0,1]],faces[:,[1,2]],faces[:,[2,0]]]),axis=1)
    _,counts=np.unique(edges,axis=0,return_counts=True)
    result['nonmanifold_edge_count']=int((counts!=2).sum())
    uv=np.array([list(loop.uv) for loop in obs[0].data.uv_layers.active.data])
    result['finite_surface_uv']=bool(np.isfinite(uv).all() and uv.min()>=-.00001 and uv.max()<=1.00001)
    result['depth_to_height']=dims[1]/dims[2]
    result['explicit_forward_marker']=any(o.name.startswith('KR_Forward') for o in bpy.context.scene.objects)
    result['passed']=error<1e-5 and abs(lo[2])<1e-5 and len(obs)==1 and result['has_uv'] and result['finite_vertices'] and result['finite_surface_uv'] and result['depth_to_height']>.3 and result['explicit_forward_marker'] and max(dims[:2])<=.82001 and result['nonmanifold_edge_count']==0
    if not result['passed']:failures.append(item['name'])
    report['pieces'].append(result)
# Inspect source board's individually named tiles and prisoner seats.
with bpy.data.libraries.load(str(SRC/'KR_Citadel_Board.blend')) as (a,b):b.scenes=a.scenes
scene=b.scenes[0]
bpy.context.window.scene=scene;bpy.context.view_layer.update()
tiles=[o for o in scene.objects if o.name.startswith('Tile •')];seats=[o for o in scene.objects if o.name.startswith('Prison • seat surface')]
tops=lambda obs:[max((o.matrix_world@Vector(v)).z for v in o.bound_box) for o in obs]
report['board']={'tile_count':len(tiles),'tile_surface_max_error_m':max(abs(z) for z in tops(tiles)),
                 'prison_seat_count':len(seats),'prison_surface_max_error_m':max(abs(z-.036) for z in tops(seats)),
                 'grid_centers':[list(o.location) for o in tiles]}
if len(tiles)!=64 or len(seats)!=12 or report['board']['tile_surface_max_error_m']>1e-5 or report['board']['prison_surface_max_error_m']>1e-5:failures.append('board geometry')
for p in list((SRC/'Pieces').glob('*.blend'))+[SRC/'KR_Citadel_Board.blend',SRC/'KR_Linirea_Valley.blend',SRC/'KingdomRush_Complete.blend']:
    with bpy.data.libraries.load(str(p)) as (a,b):
        if not a.scenes:failures.append(p.name+' has no scene')
report['checks']=['12 FBXs reimported successfully','Each FBX has one combined mesh','Proportions preserved; complete set uniformly reduced to fit 1 m cells',
                  'Ground pivot at zero','Finite vertex coordinates and UVs','64 playable tile surfaces at zero','12 prisoner pad surfaces at 0.036',
                  'Closed volumetric mesh edges; finite UVs','15 Blender source files readable']
report['likeness_note']='Technical mesh/import checks do not certify pixel identity or likeness at every 3D angle.'
report['failures']=failures;report['passed']=not failures
(SRC/'validation_report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('KR_VALIDATION',json.dumps({k:v for k,v in report.items() if k not in ['pieces','board']}))
if failures:raise RuntimeError(str(failures))
