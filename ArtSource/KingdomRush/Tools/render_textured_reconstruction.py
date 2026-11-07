"""Render and save the textured volumetric reconstruction; front and rotation evidence."""
import bpy,numpy as np,json,sys,math
from pathlib import Path
from mathutils import Vector
SRC=Path(__file__).resolve().parents[1];sys.path.insert(0,str(SRC));import build_kingdom_rush as K
name=sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else '06_Quan_vua_Denas'
fit=json.loads((SRC/'Reconstructions'/(name+'_artwork_camera.json')).read_text());D=np.load(SRC/'Reconstructions'/(name+'_textured_surface.npz'))
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
d=bpy.data.meshes.new(name+' • full volume');d.from_pydata(D['vertices'].tolist(),[],D['faces'].tolist());d.update();o=bpy.data.objects.new(name,d);bpy.context.scene.collection.objects.link(o)
uv=d.uv_layers.new(name='OriginalArtworkAtlas')
coords=D['uv'].reshape(-1,2)
for i,co in enumerate(coords):uv.data[i].uv=co
for p in d.polygons:p.use_smooth=True
if len(d.polygons)>160000:
    o.select_set(True);bpy.context.view_layer.objects.active=o
    modifier=o.modifiers.new('Preserve volumetric sculpt • delivery mesh','DECIMATE');modifier.ratio=160000/len(d.polygons)
    bpy.ops.object.modifier_apply(modifier=modifier.name);d=o.data
    np.savez_compressed(SRC/'Reconstructions'/(name+'_textured_surface.npz'),vertices=np.array([list(v.co) for v in d.vertices],np.float32),faces=np.array([list(p.vertices) for p in d.polygons],np.int32),uv=np.array([list(co.uv) for co in d.uv_layers.active.data],np.float32).reshape(-1,3,2))
m=bpy.data.materials.new(name+' • original artwork');m.use_nodes=True
shader=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');t=m.node_tree.nodes.new('ShaderNodeTexImage')
t.image=bpy.data.images.load(str(SRC/'Reconstructions/Textures'/(name+'_OriginalAtlas.png')));t.image.pack();t.interpolation='Linear'
m.node_tree.links.new(t.outputs['Color'],shader.inputs['Base Color']);m.node_tree.links.new(t.outputs['Color'],shader.inputs['Emission Color'])
shader.inputs['Emission Strength'].default_value=1;shader.inputs['Roughness'].default_value=.76;shader.inputs['Specular IOR Level'].default_value=.10
d.materials.append(m)
s=bpy.context.scene;K.render_settings(s,fit['image_size'][0],fit['image_size'][1],True);s.cycles.samples=32
s.view_settings.view_transform='Standard';s.view_settings.look='None';s.view_settings.exposure=0
background=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND');background.inputs['Strength'].default_value=0
right,up,eye=[Vector(fit[k]) for k in ['right','up','eye_direction']];sx,sy=fit['scale_xy'];ox,oy=fit['offset_xy'];W,H=fit['image_size']
target=right*((W/2-ox)/sx)+up*((oy-H/2)/sy)
cam=K.camera(s,target+eye*5,target,W/sx,'Camera • original image calibration');cam.data.sensor_fit='HORIZONTAL'
s.render.pixel_aspect_x=sy/sx;s.render.pixel_aspect_y=1
out=SRC/'Reconstructions/Checks';out.mkdir(exist_ok=True)
s.render.filepath=str(out/(name+'_textured_source_view.png'));bpy.ops.render.render(write_still=True)
# Additional angles show actual depth and the inferred back. Use a simple lit finish.
shader.inputs['Emission Strength'].default_value=.18;background.inputs['Strength'].default_value=.25
K.light(s,'Review • key',(-3,-4,5),110,(1,.92,.80),4)
K.light(s,'Review • fill',(3,-1,3),65,(.72,.84,1),4)
K.light(s,'Review • rim',(0,4,4),100,(1,.92,.79),3)
s.render.resolution_x=1000;s.render.resolution_y=1000;s.render.pixel_aspect_x=1
for view,p in [('three_quarter',(2.1,-4,1.5)),('back',(0,4,1.0))]:
    K.camera(s,p,(0,0,0),2.45,'Camera • '+view)
    s.render.filepath=str(out/(name+'_textured_'+view+'.png'));bpy.ops.render.render(write_still=True)
# Author at matching type height. Record the footprint explicitly; do not silently distort proportions.
kind=['Pawn','Rook','Knight','Bishop','Queen','King'][int(name[:2])-1];expected=K.MEAS[kind]['dimensions'];lo,hi=K.bounds([o]);uniform=expected[2]/(hi.z-lo.z)
for v in d.vertices:v.co=(v.co-Vector((0,0,lo.z)))*uniform
actual_lo,actual_hi=K.bounds([o]);o['status']='Strict-likeness revision under review; visible original pixels, inferred hidden surfaces.'
o['original_reference']=str(K.REF/(name+'.png'));o['height_matched_m']=expected[2];o['actual_dimensions_m']=list(actual_hi-actual_lo);o['original_game_dimensions_m']=expected
o['full_highres_sculpt']=str(sorted((SRC/'Reconstructions/Raw').glob(name+'*.glb'))[-1])
o['surface_type']='Closed volumetric reconstructed mesh, not a billboard or a flat image card.'
K.camera(s,(2.2,-4.2,2.3),(0,0,expected[2]*.49),expected[2]*1.35,'Camera • model review')
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA';area.spaces.active.shading.type='MATERIAL'
            area.spaces.active.overlay.show_extras=False
bpy.context.preferences.filepaths.save_version=0
review=SRC/'Reconstructions/Review';review.mkdir(exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(review/(name+'_OriginalArtwork.blend')),compress=True)
print('TEXTURED_VOLUME_SAVED',name,json.dumps({'dimensions':list(actual_hi-actual_lo),'game_dimensions':expected,'reference_height_matched':True}),flush=True)
