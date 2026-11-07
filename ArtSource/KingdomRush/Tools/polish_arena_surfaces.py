"""Keep the approved arena geometry; add scale-correct seamless material detail."""
import bpy,sys,json
from pathlib import Path
from mathutils import Vector
SRC=Path(__file__).resolve().parents[1];sys.path.insert(0,str(SRC));import build_kingdom_rush as K
OUT=K.OUT;reports=[]
for filename in ['KR_Citadel_Board.blend','KR_Linirea_Valley.blend']:
    bpy.ops.wm.open_mainfile(filepath=str(SRC/filename))
    s=max(bpy.data.scenes,key=lambda scene:sum(o.type=='MESH' for o in scene.objects));bpy.context.window.scene=s;bpy.context.view_layer.update()
    meshes=[o for o in s.objects if o.type=='MESH'];count=0
    for material in {m for o in meshes for m in o.data.materials if m}:
        if not material.use_nodes:continue
        p=next((n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
        if not p:continue
        name=material.name.lower();meadow='baked meadow' in name
        textured=meadow or any(key in name for key in ['stone','basalt','limestone','rock','grass','timber','terracotta'])
        if not textured:continue
        nodes=material.node_tree.nodes;links=material.node_tree.links
        # Texture domain stays in meters: old palette UVs use 0.5 UV per meter.
        coord=nodes.new('ShaderNodeUVMap');coord.uv_map='UVMap'
        mapping=nodes.new('ShaderNodeVectorMath');mapping.operation='MULTIPLY';mapping.inputs[1].default_value=(48,63,1) if meadow else (1,1,1);links.new(coord.outputs['UV'],mapping.inputs[0])
        family='Meadow' if meadow or 'grass' in name else 'Stone'
        images={}
        for suffix in ['Detail','Normal']:
            im=bpy.data.images.load(str(OUT/'Textures'/f'KR_{family}_{suffix}.png'),check_existing=True);im.colorspace_settings.name='Non-Color';im.pack()
            t=nodes.new('ShaderNodeTexImage');t.image=im;t.extension='REPEAT';links.new(mapping.outputs['Vector'],t.inputs['Vector']);images[suffix]=t
        multiply=nodes.new('ShaderNodeMixRGB');multiply.blend_type='MULTIPLY';multiply.inputs[0].default_value=1
        if p.inputs['Base Color'].is_linked:
            previous=p.inputs['Base Color'].links[0].from_socket;links.new(previous,multiply.inputs[1])
        else:multiply.inputs[1].default_value=p.inputs['Base Color'].default_value
        double=nodes.new('ShaderNodeVectorMath');double.operation='SCALE';double.inputs['Scale'].default_value=2;links.new(images['Detail'].outputs['Color'],double.inputs[0]);links.new(double.outputs['Vector'],multiply.inputs[2]);links.new(multiply.outputs[0],p.inputs['Base Color'])
        normal=nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.35 if meadow else .22;links.new(images['Normal'].outputs['Color'],normal.inputs['Color']);links.new(normal.outputs['Normal'],p.inputs['Normal'])
        material['surface_detail']='Seamless grain + tangent-space normal; UV density in meters';count+=1
    s['polish_revision']='Volumetric arena, seamless material detail, no billboard surfaces';s.unit_settings.system='METRIC';s.unit_settings.scale_length=1
    K.export_fbx(Path(filename).stem,meshes)
    bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(SRC/filename),compress=True)
    reports.append({'asset':filename,'mesh_objects_preserved':len(meshes),'materials_with_detail':count})
(SRC/'Polish3D/arena_report.json').write_text(json.dumps(reports,indent=2));print('ARENA_SURFACES_POLISHED',json.dumps(reports),flush=True)
