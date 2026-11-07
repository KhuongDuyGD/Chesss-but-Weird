"""Deterministic seamless material grain, separate from the approved color field."""
from pathlib import Path
import numpy as np
from scipy.ndimage import gaussian_filter
from PIL import Image
SRC=Path(__file__).resolve().parents[1];OUT=SRC.parents[1]/'Assets/Skins/KingdomRushCollab/Textures';OUT.mkdir(exist_ok=True)
rng=np.random.default_rng(62061106);N=1024
for material in ['Stone','Meadow']:
    random=rng.normal(size=(N,N)).astype(np.float32)
    broad=gaussian_filter(random,5,mode='wrap');broad/=broad.std()
    fine=gaussian_filter(random,1.2,mode='wrap');fine/=fine.std()
    height=broad*.65+fine*.35
    if material=='Meadow':height=gaussian_filter(height,(.8,2.3),mode='wrap')
    detail=np.clip(.5+height*.012,.46,.54)
    rgb=np.repeat(np.round(detail[:,:,None]*255).astype(np.uint8),3,axis=2)
    Image.fromarray(rgb).save(OUT/f'KR_{material}_Detail.png')
    dx=(np.roll(height,-1,1)-np.roll(height,1,1))*.18;dy=(np.roll(height,-1,0)-np.roll(height,1,0))*.18
    normal=np.stack([-dx,dy,np.ones_like(dx)],2);normal/=np.linalg.norm(normal,axis=2,keepdims=True)
    Image.fromarray(np.round((normal*.5+.5)*255).astype(np.uint8)).save(OUT/f'KR_{material}_Normal.png')
print('ARENA_SEAMLESS_DETAIL_READY',flush=True)
