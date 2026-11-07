"""Preserve the temporarily approved set and fetch local surface-paint weights."""
from pathlib import Path
import shutil,os,json
SRC=Path(__file__).resolve().parents[1];ROOT=SRC.parents[1]
backup=SRC/'ApprovedBeforePolish_20261106';backup.mkdir(exist_ok=True)
for folder in ['Pieces']:
    if not (backup/folder).exists():shutil.copytree(SRC/folder,backup/folder)
for file in ['KingdomRush_Complete.blend','KR_Citadel_Board.blend','KR_Linirea_Valley.blend','model_manifest.json','asset_manifest.json','REVISION_STATUS.md']:
    if (SRC/file).exists() and not (backup/file).exists():shutil.copy2(SRC/file,backup/file)
if not (backup/'UnityAssets').exists():shutil.copytree(ROOT/'Assets/Skins/KingdomRushCollab',backup/'UnityAssets')
print('APPROVED_SET_BACKED_UP',str(backup),flush=True)
os.environ['HF_HUB_DISABLE_XET']='1'
from huggingface_hub import snapshot_download
weights=Path('D:/Comfy-Desktop/KingdomRush-Tools/weights')
snapshot_download('tencent/Hunyuan3D-2.1',allow_patterns=['hunyuan3d-paintpbr-v2-1/*'],local_dir=str(weights/'HunyuanPaint21'),max_workers=4)
print('PAINT_WEIGHTS_DOWNLOADED',flush=True)
snapshot_download('facebook/dinov2-giant',allow_patterns=['config.json','preprocessor_config.json','model.safetensors'],local_dir=str(weights/'dinov2-giant'),max_workers=2)
print('DINO_WEIGHTS_DOWNLOADED',flush=True)
