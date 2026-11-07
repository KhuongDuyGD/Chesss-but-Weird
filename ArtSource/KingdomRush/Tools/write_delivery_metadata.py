from pathlib import Path
import json,shutil
SRC=Path(__file__).resolve().parents[1];ROOT=SRC.parents[1];OUT=ROOT/'Assets/Skins/KingdomRushCollab'
manifest=json.loads((OUT/'model_manifest.json').read_text());old=SRC/'asset_manifest.json';archive=SRC/'Rejected_Procedural_Draft'
if old.exists() and not (archive/old.name).exists():shutil.copy2(old,archive/old.name)
checks=json.loads((SRC/'Reconstructions/Comparisons/calibrated_view_report.json').read_text())
for model in manifest['models']:
    result=next(r for r in checks if r['name']==model['name'])
    model['calibrated_view_silhouette_iou']=result['calibrated_view_silhouette_iou']
    model['pixel_identical']=result['pixel_identical'];model['exact_likeness_certified']=False
for path in [OUT/'model_manifest.json',SRC/'model_manifest.json']:path.write_text(json.dumps(manifest,indent=2),encoding='utf-8')
delivery={'authoring':'Closed depth-based volumes from supplied artwork and locally reconstructed 3D depth; hidden surfaces inferred',
 'exact_likeness_certified':False,'status':'Art review; manual geometry/UV refinement still required for strict likeness at oblique angles',
 'pieces':[dict(m,blend=str((SRC/'Pieces'/(m['name']+'.blend')).relative_to(ROOT)).replace('\\','/'),fbx=str((OUT/'Models'/(m['name']+'.fbx')).relative_to(ROOT)).replace('\\','/')) for m in manifest['models']],
 'board':{'playable_grid':8,'cell_size':1,'surface_z':0,'prison_surface':.036},
 'facing':{'Blender_Denas':'+Y','Blender_Veznan':'-Y','Unity':'Actual painted front is measured after FBX conversion and aligned toward the opposing army','verified_instances':32}}
old.write_text(json.dumps(delivery,indent=2),encoding='utf-8')
print('DELIVERY_METADATA_WRITTEN',len(delivery['pieces']))
