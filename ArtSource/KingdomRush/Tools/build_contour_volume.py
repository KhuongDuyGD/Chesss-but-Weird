"""Rebuild a closed 3D volume from inferred front/back depths and the exact art contour.
The depth is taken from the volumetric sculpt. This is not an image plane: front,
back, thickness and silhouette walls form one closed editable mesh.
"""
import numpy as np,cv2,json,sys
from scipy.ndimage import distance_transform_edt,gaussian_filter
from scipy.spatial import cKDTree
from PIL import Image
from pathlib import Path
SRC=Path(__file__).resolve().parents[1];ROOT=SRC.parents[1];name=sys.argv[1]
fit=json.loads((SRC/'Reconstructions'/(name+'_artwork_camera.json')).read_text());D=np.load(SRC/'Reconstructions'/(name+'_textured_surface.npz'))
V=D['vertices'];F=D['faces'];right,up,eye=[np.array(fit[k]) for k in ['right','up','eye_direction']];sx,sy=fit['scale_xy'];ox,oy=fit['offset_xy'];W,H=fit['image_size']
N=512;p=np.stack([(V@right)*sx+ox,-(V@up)*sy+oy],axis=1)*[N/W,N/H];depth=V@eye
front=np.full((N,N),-np.inf,np.float32);back=np.full((N,N),np.inf,np.float32)
for face in F:
    tri=p[face];lo=np.maximum(np.floor(tri.min(0)).astype(int),0);hi=np.minimum(np.ceil(tri.max(0)).astype(int),N-1)
    if np.any(hi<lo):continue
    xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1)+.5,np.arange(lo[1],hi[1]+1)+.5)
    a,b,c=tri;den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
    if abs(den)<1e-10:continue
    wa=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den;wb=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;wc=1-wa-wb
    z=wa*depth[face[0]]+wb*depth[face[1]]+wc*depth[face[2]];inside=(wa>=0)&(wb>=0)&(wc>=0)
    area=front[lo[1]:hi[1]+1,lo[0]:hi[0]+1];area[inside]=np.maximum(area[inside],z[inside])
    area=back[lo[1]:hi[1]+1,lo[0]:hi[0]+1];area[inside]=np.minimum(area[inside],z[inside])
mask=cv2.imread(str(SRC/'Reconstructions/Masks'/(name+'_mask.png')),0)>127
target=cv2.resize(mask.astype(np.uint8),(N,N),interpolation=cv2.INTER_AREA)>.5
valid=np.isfinite(front)&np.isfinite(back)&target
_,nearest=distance_transform_edt(~valid,return_indices=True)
front=front[nearest[0],nearest[1]];back=back[nearest[0],nearest[1]]
front=gaussian_filter(front,.6);back=gaussian_filter(back,.6)
mid=(front+back)*.5;thickness=np.maximum(front-back,.028)
front=mid+thickness*.5;back=mid-thickness*.5
yy,xx=np.where(target);index=np.full((N,N),-1,np.int32);index[yy,xx]=np.arange(len(xx));count=len(xx)
base=((xx+.5)*W/N-ox)[:,None]/sx*right+(oy-(yy+.5)*H/N)[:,None]/sy*up
newV=np.concatenate([base+front[yy,xx,None]*eye,base+back[yy,xx,None]*eye]).astype(np.float32)
a=index[:-1,:-1].ravel();b=index[:-1,1:].ravel();c=index[1:,1:].ravel();d=index[1:,:-1].ravel()
first=np.stack([a,c,b],1);second=np.stack([a,d,c],1);frontF=np.concatenate([first[(first>=0).all(1)],second[(second>=0).all(1)]])
backF=frontF[:,::-1]+count
edges=np.concatenate([frontF[:,[0,1]],frontF[:,[1,2]],frontF[:,[2,0]]]);sorted_edges=np.sort(edges,axis=1)
_,indices,counts=np.unique(sorted_edges,axis=0,return_index=True,return_counts=True);boundary=edges[indices[counts==1]]
sideF=np.concatenate([np.stack([boundary[:,1],boundary[:,0],boundary[:,0]+count],1),np.stack([boundary[:,1],boundary[:,0]+count,boundary[:,1]+count],1)])
newF=np.concatenate([frontF,backF,sideF]).astype(np.int32)
old_colors=D['vertex_colors'] if 'vertex_colors' in D else np.tile([110,100,110],(len(V),1))
_,near=cKDTree(V).query(newV[count:]);colors=old_colors[near]
# Bake inferred back colors into a continuous back image, sharing the front contour.
back_rgb=np.zeros((N,N,3),np.float32);back_rgb[yy,xx]=colors
_,nn=distance_transform_edt(~target,return_indices=True);back_rgb=back_rgb[nn[0],nn[1]]
back_rgb=cv2.GaussianBlur(back_rgb,(0,0),1.4)
rgb=np.array(Image.open(ROOT/'Assets/Audio/MusicPackage/KingdomRushCollab'/(name+'.png')).convert('RGB'))
_,nn=distance_transform_edt(~mask,return_indices=True);extended=rgb[nn[0],nn[1]]
SIZE=2048;atlas=np.concatenate([cv2.resize(extended,(SIZE,SIZE),interpolation=cv2.INTER_CUBIC),cv2.resize(np.clip(back_rgb,0,255).astype(np.uint8),(SIZE,SIZE),interpolation=cv2.INTER_CUBIC)],axis=1)
Image.fromarray(atlas).save(SRC/'Reconstructions/Textures'/(name+'_OriginalAtlas.png'))
xy=np.stack([(xx+.5)/N,(yy+.5)/N],1);xy=np.concatenate([xy,xy]);uv=xy[newF].copy();uv[:,:,0]*=.5;uv[:,:,1]=1-uv[:,:,1]
uv[len(frontF):,:,0]+=.5
np.savez_compressed(SRC/'Reconstructions'/(name+'_textured_surface.npz'),vertices=newV,faces=newF,uv=uv)
report=json.loads((SRC/'Reconstructions'/(name+'_projection_report.json')).read_text())
report.update({'triangles':len(newF),'front_visible_faces':len(frontF),'inferred_faces':len(backF)+len(sideF),'contour_rebuilt':True,'contour_grid':N,'depth_source':'Front and back ray intersections of the reconstructed volumetric sculpt','exact_likeness_certified':False})
(SRC/'Reconstructions'/(name+'_projection_report.json')).write_text(json.dumps(report,indent=2))
print('CLOSED_CONTOUR_VOLUME',name,'vertices',len(newV),'triangles',len(newF),flush=True)
