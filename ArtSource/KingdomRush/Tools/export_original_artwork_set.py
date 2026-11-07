"""Publish the reviewed volumetric artwork set at a common scale fitting 1 m cells.
Original photo proportions are preserved; unseen surfaces are explicitly inferred.
"""
import bpy,numpy as np,json,math,sys,shutil
from pathlib import Path
from mathutils import Vector,Matrix
SRC=Path(__file__).resolve().parents[1];sys.path.insert(0,str(SRC));import build_kingdom_rush as K
OUT=K.OUT;names=sorted(p.stem for p in K.REF.glob('0*.png'));archive=SRC/'Rejected_Procedural_Draft';archive.mkdir(exist_ok=True)
for name in names:
    for folder,suffix in [('Pieces','.blend'),('Models','.fbx'),('Previews','.png')]:
        old=(SRC if folder=='Pieces' else OUT)/folder/(name+suffix)
        dest=archive/folder/old.name
        if old.exists() and not dest.exists():dest.parent.mkdir(exist_ok=True);shutil.copy2(old,dest)
prepared={};largest=0
for name in names:
    source=np.load(SRC/'Reconstructions'/(name+'_textured_surface.npz'));fit=json.loads((SRC/'Reconstructions'/(name+'_artwork_camera.json')).read_text())
    v=source['vertices'].copy();rotation=np.array(Matrix.Rotation(-math.radians(fit['yaw_degrees']),3,'Z'))
    v=v@rotation.T
    lo=v.min(0);hi=v.max(0);center=np.array([(lo[0]+hi[0])/2,(lo[1]+hi[1])/2,lo[2]])
    kind=['Pawn','Rook','Knight','Bishop','Queen','King'][int(name[:2])-1];oldheight=K.MEAS[kind]['dimensions'][2]
    heightscale=oldheight/(hi[2]-lo[2]);v=(v-center)*heightscale
    largest=max(largest,float(np.ptp(v,axis=0)[:2].max()))
    prepared[name]=(source,fit,v,rotation,center,heightscale,oldheight)
common=.82/largest
manifest={'cell_size_m':1.0,'maximum_piece_footprint_m':.82,'common_scale_from_old_heights':common,
          'user_size_choice':'Preserve artwork proportions; uniformly reduce the complete set to fit cells',
          'unseen_parts':'Inferred with user authorization','exact_likeness_certified':False,'models':[]}
for name,(source,fit,v,rotation,center,heightscale,oldheight) in prepared.items():
    bpy.ops.wm.read_factory_settings(use_empty=True);s=bpy.context.scene
    v*=common;faces=source['faces'];mesh=bpy.data.meshes.new(name);mesh.from_pydata(v.tolist(),[],faces.tolist());mesh.update()
    o=bpy.data.objects.new(name,mesh);s.collection.objects.link(o)
    uv=mesh.uv_layers.new(name='OriginalArtworkAtlas');uv.data.foreach_set('uv',source['uv'].reshape(-1).tolist())
    for p in mesh.polygons:p.use_smooth=True
    atlas=OUT/'Textures'/(name+'_Albedo.png');shutil.copy2(SRC/'Reconstructions/Textures'/(name+'_OriginalAtlas.png'),atlas)
    image=bpy.data.images.load(str(atlas));image.pack()
    material=bpy.data.materials.new('KR_Art_'+name);material.use_nodes=True
    nodes=material.node_tree.nodes;nodes.clear();t=nodes.new('ShaderNodeTexImage');t.image=image;t.interpolation='Linear';t.extension='EXTEND'
    emission=nodes.new('ShaderNodeEmission');emission.inputs['Strength'].default_value=1
    output=nodes.new('ShaderNodeOutputMaterial');material.node_tree.links.new(t.outputs['Color'],emission.inputs['Color']);material.node_tree.links.new(emission.outputs[0],output.inputs['Surface']);mesh.materials.append(material)
    dimensions=np.ptp(v,axis=0).tolist();o['original_reference']=str(K.REF/(name+'.png'));o['unseen_parts']='Inferred; original artwork is used for visible surfaces.'
    o['size_choice']='Uniform complete-set reduction; no horizontal squeezing';o['common_set_scale']=common;o['exact_likeness_certified']=False
    o['high_resolution_reconstruction']=str(SRC/'Reconstructions'/(name+'_Reconstructed.blend'))
    s.unit_settings.system='METRIC';s.unit_settings.scale_length=1
    K.render_settings(s,fit['image_size'][0],fit['image_size'][1],True);s.view_settings.view_transform='Standard';s.view_settings.look='None';s.cycles.samples=32
    sx,sy=fit['scale_xy'];ox,oy=fit['offset_xy'];W,H=fit['image_size'];right,up,eye=[np.array(fit[k]) for k in ['right','up','eye_direction']]
    target=right*((W/2-ox)/sx)+up*((oy-H/2)/sy);total=heightscale*common
    target=(rotation@target-center)*total;eye=rotation@eye
    K.camera(s,Vector(target+eye*5*total),Vector(target),W/sx*total,'Camera • original artwork view')
    s.render.filepath=str(OUT/'Previews'/(name+'.png'));bpy.ops.render.render(write_still=True)
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.export_scene.fbx(filepath=str(OUT/'Models'/(name+'.fbx')),use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE',embed_textures=False,use_custom_props=True)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.region_3d.view_perspective='CAMERA';area.spaces.active.shading.type='MATERIAL';area.spaces.active.overlay.show_extras=False
    bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Pieces'/(name+'.blend')),compress=True)
    report=json.loads((SRC/'Reconstructions'/(name+'_projection_report.json')).read_text())
    manifest['models'].append({'name':name,'dimensions_m':dimensions,'triangles':len(faces),'material':'KR_Art_'+name,'texture':str(atlas.relative_to(K.ROOT)).replace('\\','/'),'original_type_height_m':oldheight,'source_silhouette_iou':report['silhouette_iou_after']})
    print('ORIGINAL_ARTWORK_EXPORTED',name,dimensions,flush=True)
(OUT/'model_manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
(SRC/'model_manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print('ORIGINAL_ARTWORK_SET_EXPORTED',len(names),'common_scale',common,flush=True)
