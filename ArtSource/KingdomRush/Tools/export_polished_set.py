"""Publish closed full sculptures with nonoverlapping UVs and lit surface maps."""
import bpy,numpy as np,json,sys,math,shutil
from pathlib import Path
from mathutils import Vector
SRC=Path(__file__).resolve().parents[1];sys.path.insert(0,str(SRC));import build_kingdom_rush as K
P=SRC/'Polish3D';OUT=K.OUT;prepared={};largest=0
for path in sorted((K.REF).glob('0*.png')):
    name=path.stem;d=np.load(P/'Geometry'/(name+'_UV.npz'));v=d['vertices'].copy();lo=v.min(0);hi=v.max(0)
    kind=['Pawn','Rook','Knight','Bishop','Queen','King'][int(name[:2])-1];height=K.MEAS[kind]['dimensions'][2]
    v-=[(lo[0]+hi[0])/2,(lo[1]+hi[1])/2,lo[2]];v*=height/(hi[2]-lo[2]);largest=max(largest,float(np.ptp(v,axis=0)[:2].max()));prepared[name]=(d,v,height)
common=.82/largest
manifest={'cell_size_m':1,'maximum_piece_footprint_m':.82,'common_scale_from_old_heights':common,'user_size_choice':'Preserve chibi proportions; uniformly scale the complete set to fit cells','unseen_parts':'Inferred with user authorization','exact_likeness_certified':False,'polish_revision':'Full volumetric sculptures + unique 4096 UVs + multiview PBR surface color','models':[]}
for name,(d,v,height) in prepared.items():
    bpy.ops.wm.read_factory_settings(use_empty=True);s=bpy.context.scene;s.name=name+' • polished 3D';s.unit_settings.system='METRIC';s.unit_settings.scale_length=1
    v*=common;mesh=bpy.data.meshes.new(name);mesh.from_pydata(v.tolist(),[],d['faces'].tolist());mesh.update();uv=mesh.uv_layers.new(name='UniqueSurfaceUV');uv.data.foreach_set('uv',d['uv'].reshape(-1).tolist())
    for poly in mesh.polygons:poly.use_smooth=True
    o=bpy.data.objects.new(name,mesh);s.collection.objects.link(o)
    forward=bpy.data.objects.new('KR_Forward',None);s.collection.objects.link(forward);forward.parent=o;forward.location=(0,-.2,0);forward.empty_display_size=.05;forward.hide_render=True
    m=bpy.data.materials.new('KR_Art_'+name);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');m.diffuse_color=(.4,.4,.4,1)
    for suffix,socket in [('Albedo','Base Color'),('Metallic','Metallic'),('Roughness','Roughness')]:
        texture=OUT/'Textures'/(name+'_'+suffix+'.png');shutil.copy2(P/'Textures'/texture.name,texture)
        image=bpy.data.images.load(str(texture));image.pack()
        if suffix!='Albedo':image.colorspace_settings.name='Non-Color'
        t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=image;t.extension='EXTEND';t.interpolation='Linear';m.node_tree.links.new(t.outputs['Color'],p.inputs[socket])
    packed=OUT/'Textures'/(name+'_MetallicSmoothness.png');shutil.copy2(P/'Textures'/packed.name,packed);mesh.materials.append(m)
    o['geometry_source']='Full coherent sculpture, not contour-extruded image';o['front']='-Y in Blender; explicit KR_Forward marker for Unity';o['original_reference']=str(K.REF/(name+'.png'));o['unseen_parts']='Inferred with user authorization';o['common_set_scale']=common
    K.render_settings(s,900,900,True);s.cycles.samples=32;s.view_settings.view_transform='Standard';s.view_settings.look='None';s.view_settings.exposure=0
    bg=s.world.node_tree.nodes.get('Background');bg.inputs[0].default_value=(.28,.28,.28,1);bg.inputs[1].default_value=.65
    h=height*common;K.light(s,'Soft neutral key',(-2*h,-3*h,4*h),240*h*h,(1,1,1),3*h,(0,0,h*.5));K.light(s,'Soft fill',(3*h,-h,2*h),120*h*h,(1,1,1),3*h,(0,0,h*.5));K.light(s,'Rim',(0,3*h,3*h),200*h*h,(1,1,1),2*h,(0,0,h*.5))
    K.camera(s,(2.1*h,-3.4*h,1.9*h),(0,0,.5*h),1.36*h,'Camera • reference three-quarter')
    s.render.filepath=str(OUT/'Previews'/(name+'.png'));bpy.ops.render.render(write_still=True)
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);forward.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.export_scene.fbx(filepath=str(OUT/'Models'/(name+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE',embed_textures=False,use_custom_props=True)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA';area.spaces.active.shading.type='MATERIAL';area.spaces.active.overlay.show_extras=False
    bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Pieces'/(name+'.blend')),compress=True)
    bake=json.loads((P/'Geometry'/(name+'_bake_report.json')).read_text())
    manifest['models'].append({'name':name,'dimensions_m':np.ptp(v,axis=0).tolist(),'triangles':len(d['faces']),'material':m.name,'texture':str((OUT/'Textures'/(name+'_Albedo.png')).relative_to(K.ROOT)).replace('\\','/'),'metallic_smoothness_texture':str(packed.relative_to(K.ROOT)).replace('\\','/'),'original_type_height_m':height,'unique_uv':True,'atlas_size':[4096,4096],'shader':'URP Lit / Blender Principled','direct_multiview_surface_coverage':bake['direct_multiview_surface_coverage']})
    print('POLISHED_MODEL_EXPORTED',name,flush=True)
for path in [OUT/'model_manifest.json',SRC/'model_manifest.json']:path.write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print('POLISHED_SET_EXPORTED',len(manifest['models']),'uniform_scale',common,flush=True)
