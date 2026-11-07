"""Texture true 3D surfaces with original artwork, and propagate color over unseen surfaces.
No image synthesis or billboard substitution. The original visible image pixels are retained.
"""
import numpy as np,cv2,json,sys
from pathlib import Path
from scipy import sparse
from scipy.sparse.csgraph import dijkstra
from scipy.ndimage import distance_transform_edt,gaussian_filter1d
from PIL import Image
SRC=Path(__file__).resolve().parents[1];ROOT=SRC.parents[1]
name=sys.argv[1] if len(sys.argv)>1 else '06_Quan_vua_Denas'
fit=json.loads((SRC/'Reconstructions'/(name+'_camera_fit.json')).read_text());D=np.load(SRC/'Reconstructions'/(name+'_surface.npz'))
V=D['vertices'].copy();F=D['faces'];visible=D['visible'];right=np.array(fit['right']);up=np.array(fit['up']);sx,sy=fit['scale_xy'];ox,oy=fit['offset_xy'];W,H=fit['image_size']
rgb=np.array(Image.open(ROOT/'Assets/Audio/MusicPackage/KingdomRushCollab'/(name+'.png')).convert('RGB'))
mask=cv2.imread(str(SRC/'Reconstructions/Masks'/(name+'_mask.png')),0)>127
_,nearest=distance_transform_edt(~mask,return_indices=True)
extended=rgb[nearest[0],nearest[1]]
def projected(v):return np.stack([(v@right)*sx+ox,-(v@up)*sy+oy],axis=1)
def silhouette(v,n=512):
    pts=np.round(projected(v)*[n/W,n/H]).astype(np.int32);out=np.zeros((n,n),np.uint8)
    for f in F:cv2.fillConvexPoly(out,pts[f],1)
    return out
def score(pred,target):return float(np.logical_and(pred,target).sum()/np.logical_or(pred,target).sum())
target=cv2.resize(mask.astype(np.uint8),(512,512),interpolation=cv2.INTER_NEAREST)
before=score(silhouette(V),target)
def row_edges(m):
    left=np.argmax(m>0,axis=1).astype(float);r=m.shape[1]-1-np.argmax(m[:,::-1]>0,axis=1).astype(float);valid=m.any(axis=1)
    yy=np.arange(m.shape[0]);left=np.interp(yy,yy[valid],left[valid]);r=np.interp(yy,yy[valid],r[valid])
    return left,r
# Match the silhouette's row extents while preserving actual depth and closed topology.
# This is a small sculpt correction to the volume, not a replacement image plane.
tL,tR=row_edges(target)
for iteration in range(2):
    current=silhouette(V);sL,sR=row_edges(current)
    sL=gaussian_filter1d(sL,.6);sR=gaussian_filter1d(sR,.6)
    p=projected(V)*[512/W,512/H];y=np.clip(p[:,1],0,511)
    lo=np.interp(y,np.arange(512),sL);hi=np.interp(y,np.arange(512),sR)
    a=np.interp(y,np.arange(512),tL);b=np.interp(y,np.arange(512),tR)
    fraction=np.clip((p[:,0]-lo)/np.maximum(hi-lo,1),0,1)
    x=a+(b-a)*fraction;delta=np.clip((x-p[:,0])*W/512/sx,-.12,.12)
    V+=(delta[:,None]*right).astype(np.float32)
after=score(silhouette(V),target)
# Bake the image aspect calibration into the sculpt, so likeness does not depend
# on non-square render pixels. The exported 3D mesh uses this corrected shape.
V+=((V@up)*(sy/sx-1))[:,None]*up
sy=sx
artwork_fit=dict(fit);artwork_fit['scale_xy']=[sx,sy]
(SRC/'Reconstructions'/(name+'_artwork_camera.json')).write_text(json.dumps(artwork_fit,indent=2))
p=projected(V);px=np.clip(np.round(p[:,0]).astype(int),0,W-1);py=np.clip(np.round(p[:,1]).astype(int),0,H-1)
# Re-evaluate visibility after the silhouette sculpt. Visibility from the uncorrected
# mesh leaves small strips using the inferred atlas inside the reference view.
N=1024;screen=p*np.array([N/W,N/H]);depth=V@np.array(fit['eye_direction'])
zbuffer=np.full((N,N),-np.inf,np.float32);owner=np.full((N,N),-1,np.int32)
for fi,face in enumerate(F):
    tri=screen[face];lo=np.maximum(np.floor(tri.min(0)).astype(int),0);hi=np.minimum(np.ceil(tri.max(0)).astype(int),N-1)
    if np.any(hi<lo):continue
    xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1)+.5,np.arange(lo[1],hi[1]+1)+.5)
    a,b,c=tri;den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
    if abs(den)<1e-10:continue
    wa=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
    wb=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;wc=1-wa-wb
    z=wa*depth[face[0]]+wb*depth[face[1]]+wc*depth[face[2]]
    region=zbuffer[lo[1]:hi[1]+1,lo[0]:hi[0]+1];ids=owner[lo[1]:hi[1]+1,lo[0]:hi[0]+1]
    take=(wa>=-.001)&(wb>=-.001)&(wc>=-.001)&(z>region)
    region[take]=z[take];ids[take]=fi
visible=np.zeros(len(F),bool);visible[np.unique(owner[owner>=0])]=True
# Include subpixel triangles adjacent to the visible surface, for render-filter coverage.
centers=screen[F].mean(1);ix=np.clip(centers[:,0].astype(int),0,N-1);iy=np.clip(centers[:,1].astype(int),0,N-1)
visible|=(depth[F].mean(1)>=zbuffer[iy,ix]-.012)&np.isfinite(zbuffer[iy,ix])
sampled=extended[py,px].astype(np.float32)
seed_mask=np.zeros(len(V),bool);seed_mask[np.unique(F[visible])]=True
edges=np.concatenate([F[:,[0,1]],F[:,[1,2]],F[:,[2,0]]]);edges.sort(axis=1);edges=np.unique(edges,axis=0)
weights=np.linalg.norm(V[edges[:,0]]-V[edges[:,1]],axis=1)+1e-8
adj=sparse.coo_matrix((np.r_[weights,weights],(np.r_[edges[:,0],edges[:,1]],np.r_[edges[:,1],edges[:,0]])),shape=(len(V),len(V))).tocsr()
dist,pred,sources=dijkstra(adj,directed=False,indices=np.where(seed_mask)[0],min_only=True,return_predecessors=True)
sources=np.where(sources>=0,sources,np.arange(len(V)));colors=sampled[sources]
colors[seed_mask]=sampled[seed_mask]
# A few restricted smooth passes soften only inferred color transitions.
degree=np.asarray(adj.astype(bool).sum(axis=1)).ravel();neighbor=adj.astype(bool).astype(np.float32)
for _ in range(3):
    avg=(neighbor@colors)/np.maximum(degree[:,None],1)
    colors[~seed_mask]=.65*colors[~seed_mask]+.35*avg[~seed_mask]
# The unseen Denas king has a blue cloak, cream fur and golden hair/crown.
# Paint these broad back surfaces deliberately instead of stretching face/hand
# colors across the back of his head. Projected, visible artwork stays untouched.
if name=='06_Quan_vua_Denas':
    height=(V[:,2]-V[:,2].min())/np.ptp(V[:,2]);width=np.abs(V[:,0])/max(abs(V[:,0]).max(),1e-6)
    paint=np.tile([119.,111.,101.],(len(V),1))
    cloak=(height>.205)&(height<.655);paint[cloak]=[40,75,143]
    hem=cloak&((height<.25)|(width>.82));paint[hem]=[222,173,49]
    fur=(height>=.655)&(height<.765);paint[fur]=[225,211,196]
    hair=height>=.765;paint[hair]=[224,170,47]
    crown=height>.90;paint[crown]=[239,188,59]
    amount=np.clip((dist-.045)/.24,0,1);amount[seed_mask]=0
    colors=colors*(1-amount[:,None])+paint*amount[:,None]
SIZE=2048;atlas=np.zeros((SIZE,SIZE*2,3),np.uint8)
atlas[:,:SIZE]=cv2.resize(extended,(SIZE,SIZE),interpolation=cv2.INTER_CUBIC)
uv=np.zeros((len(F),3,2),np.float32)
uv[visible,:,0]=p[F[visible],0]/W*.5;uv[visible,:,1]=1-p[F[visible],1]/H
back=np.where(~visible)[0];tile=8;grid=SIZE//tile
if len(back)>grid*grid:raise RuntimeError('Atlas back-face capacity exceeded')
gy,gx=np.mgrid[:tile,:tile];bx=(gx-1)/5;by=(gy-1)/5
weights=np.stack([1-bx-by,bx,by],axis=-1);weights=np.maximum(weights,0);weights/=weights.sum(axis=-1,keepdims=True)
corners=np.array([[1,1],[6,1],[1,6]],np.float32)
for i,fi in enumerate(back):
    x=(i%grid)*tile;y=(i//grid)*tile
    patch=np.einsum('ijk,kc->ijc',weights,colors[F[fi]])
    atlas[y:y+tile,SIZE+x:SIZE+x+tile]=np.clip(patch,0,255).astype(np.uint8)
    uv[fi,:,0]=(SIZE+x+corners[:,0])/(SIZE*2);uv[fi,:,1]=1-(y+corners[:,1])/SIZE
out=SRC/'Reconstructions/Textures';out.mkdir(exist_ok=True)
Image.fromarray(atlas).save(out/(name+'_OriginalAtlas.png'))
np.savez_compressed(SRC/'Reconstructions'/(name+'_textured_surface.npz'),vertices=V,faces=F,uv=uv,vertex_colors=colors)
report={'name':name,'volumetric_mesh':True,'triangles':len(F),'front_visible_faces':int(visible.sum()),'inferred_faces':len(back),
        'silhouette_iou_before':before,'silhouette_iou_after':after,'texture_size':[SIZE*2,SIZE],
        'visible_color_source':'Unaltered original RGB pixels projected onto reconstructed 3D surface',
        'unseen_color_source':'Surface-geodesic propagation from visible colors; inferred, not original evidence',
        'exact_likeness_certified':False}
(SRC/'Reconstructions'/(name+'_projection_report.json')).write_text(json.dumps(report,indent=2))
print('ORIGINAL_COLORS_PROJECTED',json.dumps(report),flush=True)
