"""Process completed local 3D reconstructions sequentially, while GPU jobs continue."""
from pathlib import Path
import subprocess,sys,time,json
SRC=Path(__file__).resolve().parents[1]
ROOT=SRC.parents[1]
BLENDER='D:/Steam/steamapps/common/Blender/blender.exe'
PYTHON='D:/Comfy-Desktop/ComfyUI-Installs/ComfyUI/ComfyUI/.venv/Scripts/python.exe'
names=[p.stem for p in sorted((ROOT/'Assets/Audio/MusicPackage/KingdomRushCollab').glob('0*.png'))]
done=set()
for name in names:
    if (SRC/'Reconstructions/Review'/(name+'_OriginalArtwork.blend')).exists():done.add(name)
started=time.time()
while len(done)<len(names):
    ready=[name for name in names if name not in done and list((SRC/'Reconstructions/Raw').glob(name+'*.glb'))]
    if not ready:
        if time.time()-started>7200:raise TimeoutError('Reconstruction output not available after two hours')
        time.sleep(10);continue
    name=ready[0]
    log=SRC/'Reconstructions'/(name+'_processing.log')
    print('PROCESSING_START',name,flush=True)
    with log.open('w',encoding='utf-8') as stream:
        for script,is_blender in [('inspect_reconstruction.py',True),('fit_reference_camera.py',False),('prepare_textured_mesh.py',True),('project_original_colors.py',False),('build_contour_volume.py',False),('render_textured_reconstruction.py',True)]:
            cached={'inspect_reconstruction.py':name+'_Reconstructed.blend','fit_reference_camera.py':name+'_camera_fit.json'}.get(script)
            if cached and (SRC/'Reconstructions'/cached).exists():continue
            command=[BLENDER,'--background','--factory-startup','--python',str(SRC/'Tools'/script),'--',name] if is_blender else [PYTHON,str(SRC/'Tools'/script),name]
            subprocess.run(command,cwd=str(ROOT),stdout=stream,stderr=subprocess.STDOUT,check=True)
    done.add(name)
    report=json.loads((SRC/'Reconstructions'/(name+'_projection_report.json')).read_text())
    print('PROCESSING_DONE',name,'silhouette_iou',report['silhouette_iou_after'],'completed',len(done),flush=True)
print('ALL_TWELVE_PROCESSED',flush=True)
