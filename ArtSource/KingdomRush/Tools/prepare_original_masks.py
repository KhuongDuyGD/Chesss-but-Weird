"""Computer-vision masks for mesh silhouette checks; original RGB images stay untouched."""
from pathlib import Path
import numpy as np,cv2,json
from PIL import Image
SRC=Path(__file__).resolve().parents[1];ROOT=SRC.parents[1];REF=ROOT/'Assets/Audio/MusicPackage/KingdomRushCollab';OUT=SRC/'Reconstructions/Masks';OUT.mkdir(parents=True,exist_ok=True)
report=[]
for f in sorted(REF.glob('0*.png')):
    rgb=np.array(Image.open(f).convert('RGB'));h,w=rgb.shape[:2]
    diff=255-rgb.min(axis=2).astype(np.int16);sat=rgb.max(axis=2).astype(np.int16)-rgb.min(axis=2).astype(np.int16)
    mask=((diff>25)|(sat>13)).astype(np.uint8)
    n,labels,stats,cents=cv2.connectedComponentsWithStats(mask,8)
    valid=[i for i in range(1,n) if stats[i,cv2.CC_STAT_AREA]>max(25,w*h*.00003)]
    mask=np.isin(labels,valid).astype(np.uint8)*255
    # Only close subpixel gaps; retain fingers, weapons and cape silhouettes.
    mask=cv2.morphologyEx(mask,cv2.MORPH_CLOSE,np.ones((2,2),np.uint8))
    # Tiny enclosed white highlights on silver/fur are paint, not holes in geometry.
    n,labels,stats,_=cv2.connectedComponentsWithStats((mask==0).astype(np.uint8),8)
    exterior=set(np.r_[labels[0,:],labels[-1,:],labels[:,0],labels[:,-1]].tolist())
    for component in range(1,n):
        if component not in exterior and stats[component,cv2.CC_STAT_AREA]<w*h*.00025:mask[labels==component]=255
    yy,xx=np.where(mask>0);bbox=[int(xx.min()),int(yy.min()),int(xx.max()+1),int(yy.max()+1)]
    Image.fromarray(mask).save(OUT/(f.stem+'_mask.png'))
    report.append({'name':f.stem,'original_size':[w,h],'bbox_pixels':bbox,'source_unchanged':True})
(OUT/'mask_manifest.json').write_text(json.dumps(report,indent=2))
print('Original masks prepared',len(report))
