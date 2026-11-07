import bpy, json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[2]
results={}
for kind in ['Pawn','Rook','Knight','Bishop','Queen','King','Arena']:
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    folder='Chess_Arena' if kind=='Arena' else 'Chess_Set'
    path=root/'Assets/Skins'/"Tazji'sLowPoly"/folder/('Chess_'+kind+'.fbx')
    bpy.ops.import_scene.fbx(filepath=str(path))
    obs=[o for o in bpy.context.scene.objects if o.type=='MESH']
    vs=[o.matrix_world@Vector(v) for o in obs for v in o.bound_box]
    lo=[min(v[i] for v in vs) for i in range(3)];hi=[max(v[i] for v in vs) for i in range(3)]
    results[kind]={'min':lo,'max':hi,'dimensions':[hi[i]-lo[i] for i in range(3)],'objects':[{'name':o.name,'dimensions':list(o.dimensions),'location':list(o.location)} for o in obs]}
(Path(__file__).parent/'existing_dimensions.json').write_text(json.dumps(results,indent=2))
print(json.dumps(results))
