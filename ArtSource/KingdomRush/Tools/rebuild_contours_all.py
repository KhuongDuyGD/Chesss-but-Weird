"""Finalize each available review mesh and render comparison evidence."""
from pathlib import Path
import subprocess,json,time
SRC=Path(__file__).resolve().parents[1];ROOT=SRC.parents[1]
PY='D:/Comfy-Desktop/ComfyUI-Installs/ComfyUI/ComfyUI/.venv/Scripts/python.exe';BL='D:/Steam/steamapps/common/Blender/blender.exe'
names=[p.stem for p in sorted((ROOT/'Assets/Audio/MusicPackage/KingdomRushCollab').glob('0*.png'))]
for name in names:
    review=SRC/'Reconstructions/Review'/(name+'_OriginalArtwork.blend')
    while not review.exists():time.sleep(10)
    # Original image and masks are always read again, including tiny white highlights.
    print('CONTOUR_FINAL_START',name,flush=True)
    with (SRC/'Reconstructions'/(name+'_contour_final.log')).open('w',encoding='utf-8') as log:
        for script in ['project_original_colors.py','build_contour_volume.py']:
            subprocess.run([PY,str(SRC/'Tools'/script),name],cwd=str(ROOT),stdout=log,stderr=subprocess.STDOUT,check=True)
        subprocess.run([BL,'--background','--factory-startup','--python',str(SRC/'Tools/render_textured_reconstruction.py'),'--',name],cwd=str(ROOT),stdout=log,stderr=subprocess.STDOUT,check=True)
    print('CONTOUR_FINAL_DONE',name,flush=True)
print('ALL_CONTOURS_FINAL',flush=True)
