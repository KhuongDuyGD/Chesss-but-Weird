"""Sequential GPU stages; parallel independent CPU surface bakes; do not auto-publish."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import subprocess,time,os,sys
SRC=Path(__file__).resolve().parents[1];ROOT=SRC.parents[1];PY=sys.executable;BL='D:/Steam/steamapps/common/Blender/blender.exe'
names=sorted(p.stem for p in (ROOT/'Assets/Audio/MusicPackage/KingdomRushCollab').glob('0*.png'))
while 'SURFACE_PAINT_GENERATED 06_Quan_vua_Veznan' not in (SRC/'polish_paint_all.log').read_text(errors='replace'):time.sleep(5)
# A failed sample is rejected before publication, never patched over in the atlas.
env=dict(os.environ,KR_PAINT_STEPS='20',KR_PAINT_SEED='62061107')
if '--rebake' not in sys.argv:
    with (SRC/'polish_paint_pawn_retry.log').open('w',encoding='utf-8') as log:subprocess.run([PY,str(SRC/'Tools/generate_surface_paint.py'),'01_Quan_tot_Denas'],stdout=log,stderr=subprocess.STDOUT,env=env,check=True)
print('PAWN_COLOR_RETRY_READY',flush=True)
with (SRC/'polish_upscale_all.log').open('w',encoding='utf-8') as log:subprocess.run([PY,str(SRC/'Tools/upscale_surface_paint.py')]+names,stdout=log,stderr=subprocess.STDOUT,check=True)
print('ALL_SURFACE_DETAILS_READY',flush=True)
def bake(name):
    with (SRC/('polish_bake_'+name+'.log')).open('w',encoding='utf-8') as log:subprocess.run([PY,str(SRC/'Tools/bake_surface_atlas.py'),name],stdout=log,stderr=subprocess.STDOUT,check=True)
    print('PBR_ATLAS_READY',name,flush=True)
with ThreadPoolExecutor(max_workers=3) as executor:list(executor.map(bake,names))
for name in names:
    with (SRC/('polish_review_'+name+'.log')).open('w',encoding='utf-8') as log:subprocess.run([BL,'--background','--factory-startup','--python-exit-code','1','--python',str(SRC/'Tools/review_polished_sculpt.py'),'--',name,'--gpu'],stdout=log,stderr=subprocess.STDOUT,check=True)
    print('NINE_ANGLE_REVIEW_READY',name,flush=True)
print('ALL_POLISH_REVIEWS_READY',flush=True)
