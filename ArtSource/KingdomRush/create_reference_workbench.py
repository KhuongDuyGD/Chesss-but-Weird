"""Full-resolution original-image workbench for strict likeness revision.
These are reference image empties and measurement guides, NOT finished 3D pieces.
"""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];SRC=Path(__file__).parent
REF=ROOT/'Assets/Audio/MusicPackage/KingdomRushCollab'
measure=json.loads((SRC/'existing_dimensions.json').read_text())
kinds=['Pawn','Rook','Knight','Bishop','Queen','King']
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
initial=bpy.context.scene
scenes=[]
for i,p in enumerate(sorted(REF.glob('0*.png'))):
    kind=kinds[int(p.name[:2])-1];height=measure[kind]['dimensions'][2]
    s=bpy.data.scenes.new(p.stem+' • ORIGINAL IMAGE');bpy.context.window.scene=s
    s.unit_settings.system='METRIC';s.unit_settings.scale_length=1
    img=bpy.data.images.load(str(p),check_existing=True);img.pack()
    o=bpy.data.objects.new('ORIGINAL IMAGE • '+p.stem,None);s.collection.objects.link(o)
    o.empty_display_type='IMAGE';o.data=img;o.empty_display_size=height;o.location=(0,0,height/2);o.rotation_euler=(math.pi/2,0,0)
    o['purpose']='Original full-resolution reference. Not a modeled chess piece.'
    o['required_height_m']=height;o['required_width_m']=measure[kind]['dimensions'][0];o['required_depth_m']=measure[kind]['dimensions'][1]
    # Bounding guide measures the actual required imported mesh, not the whitespace in the image.
    bpy.ops.mesh.primitive_cube_add(size=1,location=(0,.03,height/2));g=bpy.context.object;g.name='MEASUREMENT • target model bounds'
    g.dimensions=measure[kind]['dimensions'];g.display_type='WIRE';g.hide_render=True
    d=bpy.data.cameras.new('Reference • straight-on camera');c=bpy.data.objects.new('Reference • straight-on camera',d);s.collection.objects.link(c)
    c.location=(0,-5,height/2);c.rotation_euler=(math.pi/2,0,0);d.type='ORTHO';d.ortho_scale=height*1.18;s.camera=c
    s['status']='Reference setup for strict likeness revision. Previous procedural models are drafts and do not meet exact-likeness requirement.'
    s.render.resolution_x=1200;s.render.resolution_y=1200
    scenes.append(s)
bpy.data.scenes.remove(initial)
bpy.context.window.scene=scenes[0]
for screen in bpy.data.screens:
    for a in screen.areas:
        if a.type=='VIEW_3D':
            a.spaces.active.region_3d.view_perspective='CAMERA';a.spaces.active.overlay.show_floor=False
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'KingdomRush_Original_Reference_Workbench.blend'),compress=True)
print('REFERENCE_WORKBENCH_SAVED',len(scenes))
