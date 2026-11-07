"""Wait for the reconstruction pipeline, then bake, export, assemble and validate."""
from pathlib import Path
import subprocess,time
SRC=Path(__file__).resolve().parents[1];ROOT=SRC.parents[1]
PY='D:/Comfy-Desktop/ComfyUI-Installs/ComfyUI/ComfyUI/.venv/Scripts/python.exe';BL='D:/Steam/steamapps/common/Blender/blender.exe'
while 'ALL_CONTOURS_FINAL' not in (SRC/'Reconstructions/contours_all.log').read_text(errors='replace'):time.sleep(5)
for script,is_blender in [('paint_inferred_backs.py',False),('export_original_artwork_set.py',True),('assemble_artwork_showcase.py',True),('../validate_assets.py',True),('compare_original_artwork.py',False)]:
    print('FINAL_STAGE_START',script,flush=True)
    command=[BL,'--background','--factory-startup','--python',str(SRC/'Tools'/script)] if is_blender else [PY,str(SRC/'Tools'/script)]
    with (SRC/'Reconstructions'/('final_'+Path(script).stem+'.log')).open('w',encoding='utf-8') as log:
        subprocess.run(command,cwd=str(ROOT),stdout=log,stderr=subprocess.STDOUT,check=True)
    print('FINAL_STAGE_DONE',script,flush=True)
print('ARTWORK_EXPORT_COMPLETE',flush=True)
