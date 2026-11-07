from pathlib import Path
import subprocess,sys,time
SRC=Path(__file__).resolve().parents[1]
names=sys.argv[1:] or sorted(p.stem for p in (SRC.parents[1]/'Assets/Audio/MusicPackage/KingdomRushCollab').glob('0*.png') if p.stem!='06_Quan_vua_Denas')
for name in names:
    while not (SRC/'Polish3D/Geometry'/(name+'.npz')).exists():time.sleep(2)
    subprocess.run([sys.executable,str(SRC/'Tools/prepare_paint_views.py'),name],check=True)
