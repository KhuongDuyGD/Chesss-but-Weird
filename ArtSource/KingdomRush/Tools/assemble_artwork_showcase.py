"""Assemble the delivered art, not the superseded procedural piece drafts."""
import bpy,json,sys,math
from pathlib import Path
from mathutils import Vector
SRC=Path(__file__).resolve().parents[1];sys.path.insert(0,str(SRC));import build_kingdom_rush as K
bpy.ops.wm.read_factory_settings(use_empty=True);s=bpy.context.scene;s.name='Kingdom Rush • Polished Volumetric Set'
def source_scene(path):
    with bpy.data.libraries.load(str(path),link=False) as (a,b):b.scenes=a.scenes
    source=next(scene for scene in b.scenes if any(o.type=='MESH' for o in scene.objects))
    current=bpy.context.window.scene;bpy.context.window.scene=source;bpy.context.view_layer.update();bpy.context.window.scene=current
    return source
for filename in ['KR_Citadel_Board.blend','KR_Linirea_Valley.blend']:
    source=source_scene(SRC/filename)
    for ob in source.objects:
        if ob.type!='MESH':continue
        copy=ob.copy();s.collection.objects.link(copy);copy.parent=None;copy.matrix_world=ob.matrix_world.copy()
models={}
manifest=json.loads((K.OUT/'model_manifest.json').read_text())
for item in manifest['models']:
    name=item['name'];source=source_scene(SRC/'Pieces'/(name+'.blend'));models[name]=next(o for o in source.objects if o.type=='MESH')
prefix=['01_Quan_tot','02_Quan_xe','03_Quan_ngua','04_Quan_tuong','05_Quan_hau','06_Quan_vua'];back=[1,2,3,4,5,3,2,1]
for dark in [False,True]:
    faction='Veznan' if dark else 'Denas'
    for rank in [0,1]:
        for file in range(8):
            kind=0 if rank else back[file];name=prefix[kind]+'_'+faction;copy=models[name].copy();s.collection.objects.link(copy)
            copy.name=faction+' '+name+' '+chr(97+file)+str(8-rank if dark else 1+rank)
            copy.parent=None;copy.location=(file-3.5,3.5-rank if dark else -3.5+rank,0)
            copy.rotation_euler=(0,0,0 if dark else math.pi)
            forward=copy.rotation_euler.to_matrix()@Vector((0,-1,0))
            assert forward.dot(Vector((0,-1 if dark else 1,0)))>.999,'Piece must face the opposing army'
            copy['chess_piece_instance']=True
K.render_settings(s,2400,1700);s.cycles.samples=48;s.view_settings.exposure=.25
world=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND');world.inputs['Color'].default_value=(.26,.40,.57,1);world.inputs['Strength'].default_value=.5
K.light(s,'Hero • warm key',(-7,-11,16),2500,(1,.84,.66),10)
K.light(s,'Hero • sky fill',(10,-2,14),2000,(.62,.78,1),11)
K.light(s,'Hero • sunset rim',(-3,12,16),3000,(1,.74,.47),9)
d=bpy.data.lights.new('Hero • sun','SUN');d.energy=1.4;d.angle=.15;o=bpy.data.objects.new('Hero • sun',d);s.collection.objects.link(o);o.rotation_euler=(.5,-.4,-.7)
hero=K.camera(s,(13,-20,20),(0,.3,.12),19.8,'Camera • board and armies')
landscape=K.camera(s,(13,-23,13),(0,4,.3),20,'Camera • landscape context');landscape.data.type='PERSP';landscape.data.lens=24
s.camera=hero
for scene in list(bpy.data.scenes):
    if scene!=s:bpy.data.scenes.remove(scene)
s['art_status']='Full volumetric sculptures with unique surface UVs and lit PBR materials. Six-view coherent paint; hidden surfaces inferred.'
s['user_scale_choice']='Preserve chibi proportions, uniformly reduce complete set to fit cells.'
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA';area.spaces.active.shading.type='MATERIAL';area.spaces.active.overlay.show_extras=False
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'KingdomRush_Complete.blend'),compress=True)
s.render.filepath=str(SRC/'Renders/KingdomRush_Hero.png');bpy.ops.render.render(write_still=True)
K.camera(s,(0,-.01,26),(0,0,0),14.2,'Camera • grid verification');s.render.resolution_x=1800;s.render.resolution_y=1800;s.cycles.samples=32
s.render.filepath=str(SRC/'Renders/KingdomRush_Top.png');bpy.ops.render.render(write_still=True)
s.camera=landscape;s.render.resolution_x=2200;s.render.resolution_y=1400
s.render.filepath=str(SRC/'Renders/KingdomRush_Valley_Context.png');bpy.ops.render.render(write_still=True)
print('SHOWCASE_ORIGINAL_ARTWORK_DONE','pieces',sum(bool(o.get('chess_piece_instance')) for o in s.objects),flush=True)
