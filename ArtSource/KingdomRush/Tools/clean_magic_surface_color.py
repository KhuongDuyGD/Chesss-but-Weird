"""Material-region correction on the newly rounded green flames' side surfaces."""
from pathlib import Path
import numpy as np,cv2,json
from PIL import Image
SRC=Path(__file__).resolve().parents[1];P=SRC/'Polish3D'
for name in ['04_Quan_tuong_Veznan','05_Quan_hau_Veznan']:
    D=np.load(P/'Geometry'/(name+'_UV.npz'));V=D['vertices'];F=D['faces'];UV=D['uv'];h=(V[:,2]-V[:,2].min())/np.ptp(V[:,2]);region=(V[:,0]>(.25 if name.startswith('04') else .40))&(h>.85)
    mask=np.zeros((4096,4096),np.uint8)
    for triangle in UV[region[F].all(1)]:
        xy=triangle.copy();xy[:,1]=1-xy[:,1];cv2.fillConvexPoly(mask,np.round(xy*4096).astype(np.int32),1)
    image=np.array(Image.open(P/'Textures'/(name+'_Albedo.png')).convert('RGB'));rgb=image.astype(np.float32)/255
    green=(rgb[:,:,1]>rgb[:,:,0]*1.15)&(rgb[:,:,1]>rgb[:,:,2]*1.3)
    valid=(mask>0)&green
    if valid.any():
        color=np.median(image[valid],axis=0);wrong=(mask>0)&~green;image[wrong]=color.astype(np.uint8)
        Image.fromarray(image).save(P/'Textures'/(name+'_Albedo.png'))
        print('MAGIC_SIDE_COLOR_CLEANED',name,'texels',int(wrong.sum()),flush=True)
