"""Finalize source-only scenes, board notation and visible near-field landmarks."""
import bpy,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent))
import build_factions as F
K=F.K
for faction,evil in [('Denas',False),('Veznan',True)]:
    board=F.board(faction,evil)
    with bpy.data.libraries.load(str(F.DEST/('KR_'+faction+'_Environment.blend')),link=False) as (a,b):b.scenes=a.scenes
    s=max((scene for scene in b.scenes if scene.name.startswith('KR_'+faction+'_Environment')),key=lambda scene:sum(o.type=='MESH' for o in scene.objects))
    bpy.context.window.scene=s;K.PARTS=[]
    for o in list(s.objects):
        if o.type=='MESH' and any((o.matrix_world.translation.x-x)**2+(o.matrix_world.translation.y-y)**2<1.7**2 for x,y in [(-9,-7),(9,7),(-9,7),(9,-7)]):
            bpy.data.objects.remove(o,do_unlink=True)
    for x,y in [(-9,-7),(9,7),(-9,7),(9,-7)]:
        z=F.height(x,y)
        F.pillar(x,y,z,1.15,.34,'Obsidian' if evil else 'Royal limestone',evil)
        for i in range(5):
            dx=(i-2)*.45
            K.ico('Near-field broken rock' if evil else 'Garden border stone',(x+dx,y-.8,z+.1),(.22,.17,.19),'Ash rock' if evil else 'rock',1)
    extra=list(K.PARTS);K.apply_meshes(extra);K.uv(extra)
    meshes=[o for o in s.objects if o.type=='MESH']
    K.export_fbx('KR_'+faction+'_Environment',meshes)
    K.save_blend(F.DEST/('KR_'+faction+'_Environment.blend'))
    s.render.filepath=str(F.OUT/'Previews'/('KR_'+faction+'_Environment.png'));bpy.ops.render.render(write_still=True)
    # Copy only the correct faction board into the review scene, never the factory cube.
    for o in board.objects:
        if o.type=='MESH':
            c=o.copy();c.data=o.data;c.parent=None;c.matrix_world=o.matrix_world;s.collection.objects.link(c)
    K.camera(s,(13,-19,23),(0,0,.1),25,'Final context camera')
    s.render.filepath=str(F.DEST/(faction+'_Context.png'));bpy.ops.render.render(write_still=True)
F.write_palette()
print('FACTION_POLISH_DONE',flush=True)
