"""Pack the final surface images into the four source-only editable Blender files."""
import bpy,json
from pathlib import Path
root=Path(__file__).parent/'Factions';results=[]
for faction in ['Denas','Veznan']:
    for kind in ['Board','Environment']:
        path=root/('KR_'+faction+'_'+kind+'.blend')
        bpy.ops.wm.read_factory_settings(use_empty=True)
        with bpy.data.libraries.load(str(path),link=False) as (source,target):target.scenes=source.scenes
        scene=max(target.scenes,key=lambda s:sum(o.type=='MESH' for o in s.objects));bpy.context.window.scene=scene
        images={node.image for o in scene.objects if o.type=='MESH' for mat in o.data.materials if mat and mat.use_nodes for node in mat.node_tree.nodes if node.type=='TEX_IMAGE' and node.image}
        for image in images:
            if not image.packed_file:image.pack()
        bpy.data.libraries.write(str(path),{scene},fake_user=True,compress=True)
        results.append({'source':path.name,'scene':scene.name,'packedImages':len(images),'meshParts':sum(o.type=='MESH' for o in scene.objects)})
(root/'HighQuality/source_pack_report.json').write_text(json.dumps(results,indent=2))
print('HQ_SOURCES_PACKED',json.dumps(results),flush=True)
