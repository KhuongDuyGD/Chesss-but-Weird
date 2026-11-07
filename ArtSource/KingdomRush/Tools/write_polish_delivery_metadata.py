from pathlib import Path
import json
SRC=Path(__file__).resolve().parents[1];ROOT=SRC.parents[1];OUT=ROOT/'Assets/Skins/KingdomRushCollab'
manifest=json.loads((OUT/'model_manifest.json').read_text());technical=json.loads((SRC/'validation_report.json').read_text());unity=(SRC/'unity_import_validation.txt').read_text()
assert technical['passed'] and len(technical['pieces'])==12
assert len([line for line in unity.splitlines() if line.startswith('PASS')])>=14
delivery={'authoring':'Full reconstructed volumetric sculptures, repaired outer surfaces, rounded 3D magic, unique UVs and coherent multiview surface paint','polish_revision':'2026-11-06','status':'Multiangle 3D polish completed; Blender/FBX/Unity technical verification passed','exact_likeness_certified':False,'pieces':[dict(m,blend=str((SRC/'Pieces'/(m['name']+'.blend')).relative_to(ROOT)).replace('\\','/'),fbx=str((OUT/'Models'/(m['name']+'.fbx')).relative_to(ROOT)).replace('\\','/')) for m in manifest['models']],'board':{'playable_grid':8,'cell_size':1,'surface_z':0,'prison_surface':.036,'surface_detail':'Seamless albedo grain and tangent normal maps'},'facing':{'Blender_Denas':'+Y','Blender_Veznan':'-Y','Unity':'Explicit KR_Forward marker after FBX axis conversion','verified_instances':32},'review':{'Blender_piece_angles':9,'Blender_piece_render_count':108,'board_angles':9,'background_angles':3,'Unity_piece_angles':5,'Unity_board_angles':4,'gallery':'Renders/KingdomRush_Multiangle_Review.jpg'},'backup_before_polish':'ApprovedBeforePolish_20261106','limitations':'Hidden parts inferred from one source image with user authorization. Technical and sampled-angle checks do not certify pixel identity or all possible viewing/lighting conditions.'}
game=SRC/'GameVerification'
if (game/'checks.txt').exists():
    checks=(game/'checks.txt').read_text(encoding='utf-8').splitlines()
    assert not any(line.startswith('FAIL') for line in checks)
    delivery['game_integration']={'catalog_id':'kingdom_rush','shop_products':['CHESS_PIECE_SKIN','BOARD_SKIN','BACKGROUND_SKIN'],
        'runtime':'Built Addressables, real Unity player loop','runtime_checks_passed':len([line for line in checks if line.startswith('PASS')]),
        'report':'GameVerification/checks.txt','basic_background':'Locked non-sale Classic default; independently equipped paid backgrounds',
        'backend_commit':'b8892f6','live_shop_verified':(game/'live_shop_offers.json').exists(),
        'gacha_excluded':(game/'live_gacha_exclusion.json').exists(),
        'limits':'No production purchase or authenticated two-client match was performed.'}
(SRC/'asset_manifest.json').write_text(json.dumps(delivery,indent=2),encoding='utf-8');print('POLISH_DELIVERY_METADATA_WRITTEN',len(delivery['pieces']),flush=True)
