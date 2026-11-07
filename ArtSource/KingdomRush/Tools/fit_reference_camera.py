"""Fit an orthographic projection to the original silhouette. Reports actual overlap.
This measures a silhouette only; it is not proof of pixel-exact geometry or texturing.
"""
import numpy as np,cv2,json,sys,math
from pathlib import Path
from scipy.optimize import minimize
SRC=Path(__file__).resolve().parents[1]
name=sys.argv[1] if len(sys.argv)>1 else '06_Quan_vua_Denas'
data=np.load(SRC/'Reconstructions'/(name+'_proxy.npz'));V=data['vertices'];F=data['faces']
mask=cv2.imread(str(SRC/'Reconstructions/Masks'/(name+'_mask.png')),0)
H,W=mask.shape;N=256;target=cv2.resize(mask,(N,N),interpolation=cv2.INTER_NEAREST)>127
ys,xs=np.where(target);bb=np.array([xs.min(),ys.min(),xs.max(),ys.max()])
def project(yaw,pitch):
    a,b=math.radians(yaw),math.radians(pitch)
    right=np.array([math.cos(a),math.sin(a),0]);up=np.array([-math.sin(b)*math.sin(a),math.sin(b)*math.cos(a),math.cos(b)])
    xy=np.stack([V@right,-V@up],axis=1)
    lo=xy.min(axis=0);hi=xy.max(axis=0)
    scale=(bb[2:]-bb[:2])/(hi-lo);offset=bb[:2]-lo*scale
    return xy*scale+offset,right,up,scale,offset
count=0
def loss(p,ret=False):
    global count
    yaw,pitch=p
    if abs(pitch)>60 or abs(yaw)>80:return 1+abs(pitch)/100+abs(yaw)/100
    pts,*_=project(yaw,pitch);pts=np.round(pts).astype(np.int32);image=np.zeros((N,N),np.uint8)
    for f in F:cv2.fillConvexPoly(image,pts[f],1)
    pred=image>0;inter=np.logical_and(pred,target).sum();union=np.logical_or(pred,target).sum();iou=float(inter/union)
    count+=1
    return (iou,image) if ret else 1-iou
coarse=[]
for yaw in range(-60,61,15):
    for pitch in [0,12,25,40]:coarse.append((loss([yaw,pitch]),yaw,pitch))
best=[]
for l,y,p in sorted(coarse)[:3]:
    opt=minimize(loss,[y,p],method='Nelder-Mead',options={'maxiter':65,'xatol':.03,'fatol':.0001});best.append(opt)
r=min(best,key=lambda q:q.fun);yaw,pitch=r.x
xy,right,up,scale,offset=project(yaw,pitch);iou,image=loss([yaw,pitch],True)
original_scale=scale*np.array([W/N,H/N]);original_offset=offset*np.array([W/N,H/N])
fit={'name':name,'yaw_degrees':float(yaw),'pitch_degrees':float(pitch),'silhouette_iou':iou,'right':right.tolist(),'up':up.tolist(),
     'eye_direction':np.cross(right,up).tolist(),'image_size':[W,H],'scale_xy':original_scale.tolist(),'offset_xy':original_offset.tolist(),
     'note':'Camera fit permits separate X/Y scale to expose proportion differences; does not certify exact likeness.'}
(SRC/'Reconstructions'/(name+'_camera_fit.json')).write_text(json.dumps(fit,indent=2))
compare=np.zeros((N,N,3),np.uint8);compare[target]=(80,180,80);compare[image>0]=(200,80,80);compare[np.logical_and(target,image>0)]=(210,210,210)
cv2.imwrite(str(SRC/'Reconstructions/Checks'/(name+'_silhouette_fit.png')),cv2.resize(compare,(768,768),interpolation=cv2.INTER_NEAREST))
print('CAMERA_FIT',json.dumps(fit),flush=True)
