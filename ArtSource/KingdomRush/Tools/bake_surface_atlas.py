"""Bake continuous surface albedo to unique UVs from occlusion-tested 3D views."""
from pathlib import Path
import numpy as np,cv2,json,sys
from scipy.ndimage import distance_transform_edt
from scipy.spatial import cKDTree
from PIL import Image
SRC=Path(__file__).resolve().parents[1];OUT=SRC/'Polish3D';name=sys.argv[1]
D=np.load(OUT/'Geometry'/(name+'_UV.npz'));V=D['vertices'];F=D['faces'];UV=D['uv'];normals=D['normals'];folder=OUT/'Views'/name
camera=json.loads((folder/'cameras.json').read_text());R=(-V-np.array(camera['render_center']))*(1.15/camera['diameter']);SIZE=4096
position=np.zeros((SIZE,SIZE,3),np.float32);normal=np.zeros_like(position);mask=np.zeros((SIZE,SIZE),bool)
points=UV.copy();points[:,:,0]*=SIZE;points[:,:,1]=(1-points[:,:,1])*SIZE
for fi,face in enumerate(F):
    tri=points[fi];lo=np.maximum(np.floor(tri.min(0)).astype(int),0);hi=np.minimum(np.ceil(tri.max(0)).astype(int),SIZE-1)
    if np.any(hi<lo):continue
    xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1)+.5,np.arange(lo[1],hi[1]+1)+.5)
    a,b,c=tri;den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
    if abs(den)<1e-10:continue
    wa=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den;wb=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;wc=1-wa-wb
    take=(wa>=0)&(wb>=0)&(wc>=0);sl=np.s_[lo[1]:hi[1]+1,lo[0]:hi[0]+1]
    p=wa[:,:,None]*R[face[0]]+wb[:,:,None]*R[face[1]]+wc[:,:,None]*R[face[2]]
    n=wa[:,:,None]*(-normals[face[0]])+wb[:,:,None]*(-normals[face[1]])+wc[:,:,None]*(-normals[face[2]])
    position[sl][take]=p[take];normal[sl][take]=n[take];mask[sl][take]=True
print('UV_SURFACE_RASTERIZED',name,'texels',int(mask.sum()),flush=True)
views=[]
for c in camera['cameras']:
    i=c['index'];depth=np.load(folder/f'depth_{i}.npy');valid=np.isfinite(depth)
    core=cv2.erode(valid.astype(np.uint8),np.ones((3,3),np.uint8))>0
    _,near=distance_transform_edt(~core,return_indices=True)
    image_path=folder/'Generated'/f'albedo_{i}_detail.png'
    if not image_path.exists():image_path=folder/'Generated'/f'albedo_{i}.png'
    image=np.array(Image.open(image_path).convert('RGB'))
    # Extend predicted edge colors before any bilinear UV sampling.
    size=image.shape[0];core_big=cv2.resize(core.astype(np.uint8),(size,size),interpolation=cv2.INTER_NEAREST)>0
    _,big_near=distance_transform_edt(~core_big,return_indices=True);image=image[big_near[0],big_near[1]]
    filled=depth[near[0],near[1]]
    views.append((c,image,filled,valid))
albedo=np.zeros((SIZE,SIZE,3),np.uint8);weightmap=np.zeros((SIZE,SIZE),np.float32)
for y in range(0,SIZE,64):
    p=position[y:y+64];n=normal[y:y+64];n/=np.maximum(np.linalg.norm(n,axis=2,keepdims=True),1e-8)
    color=np.zeros_like(p);total=np.zeros(p.shape[:2],np.float32)
    for c,image,depth,valid in views:
        eye=np.array(c['eye']);right=np.array(c['right']);up=np.array(c['up']);resolution=depth.shape[0]
        u=(p@right)/camera['scale']+.5;v=(p@up)/camera['scale']+.5
        ix=(u*resolution-.5).astype(np.float32);iy=(v*resolution-.5).astype(np.float32)
        observed=cv2.remap(depth,ix,iy,cv2.INTER_LINEAR,borderMode=cv2.BORDER_CONSTANT,borderValue=-100)
        valid_sample=cv2.remap(valid.astype(np.uint8),ix,iy,cv2.INTER_NEAREST,borderMode=cv2.BORDER_CONSTANT)>0
        visible=(np.abs(p@eye-observed)<.007)&valid_sample&(u>=0)&(u<1)&(v>=0)&(v<1)
        weight=np.maximum(n@eye,0)**6*visible
        weight*=1.2 if c['index']==0 else 1
        sampled=cv2.remap(image,(u*image.shape[1]-.5).astype(np.float32),(v*image.shape[0]-.5).astype(np.float32),cv2.INTER_LINEAR,borderMode=cv2.BORDER_REPLICATE)
        color+=sampled*weight[:,:,None];total+=weight
    albedo[y:y+64]=np.clip(color/np.maximum(total[:,:,None],1e-8),0,255).astype(np.uint8);weightmap[y:y+64]=total
valid=mask&(weightmap>.000001);coverage=float(valid.sum()/max(mask.sum(),1))
# Fill occluded crevices in physical surface space. UV islands are unrelated
# neighbors; propagating colors between their pixels creates gold/skin speckles.
sample_indices=np.flatnonzero(valid)[::5]
sample_p=position.reshape(-1,3)[sample_indices];sample_n=normal.reshape(-1,3)[sample_indices]
sample_c=albedo.reshape(-1,3)[sample_indices];tree=cKDTree(sample_p)
for y in range(0,SIZE,32):
    missing=mask[y:y+32]&~valid[y:y+32]
    if not missing.any():continue
    p=position[y:y+32][missing];n=normal[y:y+32][missing]
    distance,indices=tree.query(p,k=16,workers=4)
    alignment=np.clip(np.einsum('ik,ijk->ij',n,sample_n[indices]),-1,1)
    metric=distance**2+(1-alignment)**2*.00012
    best=np.argsort(metric,axis=1)[:,:4];chosen=np.take_along_axis(indices,best,axis=1)
    score=1/np.maximum(np.take_along_axis(metric,best,axis=1),1e-8)**2
    color=np.sum(sample_c[chosen]*score[:,:,None],axis=1)/score.sum(1,keepdims=True)
    albedo[y:y+32][missing]=np.clip(color,0,255).astype(np.uint8)
print('OCCLUDED_SURFACE_FILLED_IN_3D',name,flush=True)
_,near=distance_transform_edt(~mask,return_indices=True)
albedo=albedo[near[0],near[1]]
# Padding is extended into every empty UV texel, preventing dark mip seams.
textures=OUT/'Textures';textures.mkdir(exist_ok=True)
# R=-B: reconstruct original sculpture height from normalized renderer coordinates.
world_z=-(position[:,:,2]/(1.15/camera['diameter'])+np.array(camera['render_center'])[2]);height=(world_z-V[:,2].min())/np.ptp(V[:,2])
rgb=albedo.astype(np.float32)/255;mx=rgb.max(2);mn=rgb.min(2);saturation=(mx-mn)/np.maximum(mx,.001)
# The pedestal underside is stone. Do not retain the paint model's imagined
# blue cloth or boots on the bottom of the miniature.
stone_sample=mask&(height>.02)&(height<.13)&(mx>.12)&(mx<.55)
if name.endswith('Denas'):stone_sample&=saturation<.30
if stone_sample.sum()>100:
    underside=mask&(height<.13)&(normal[:,:,2]>.55)
    albedo[underside]=np.median(albedo[stone_sample],axis=0).astype(np.uint8)
    albedo=albedo[near[0],near[1]]
    rgb=albedo.astype(np.float32)/255;mx=rgb.max(2);mn=rgb.min(2);saturation=(mx-mn)/np.maximum(mx,.001)
if name.endswith('Denas'):
    cutoff={'01':.28,'02':.18,'03':.19,'04':.20,'05':.19,'06':.22}[name[:2]]
    world_y=-(position[:,:,1]/(1.15/camera['diameter'])+camera['render_center'][1])
    # The badge remains colored. Clean stray projected trim on the upward-facing
    # stone shelf beneath the character, behind the front badge.
    stone=(height<cutoff)&mask&(saturation<.28)&(mx>.18)&(mx<.65)
    if stone.sum()>100:
        base=np.median(albedo[stone],axis=0)
        stray=(height<cutoff)&mask&(normal[:,:,2]<-.65)&(world_y>V[:,1].min()+np.ptp(V[:,1])*.12)
        albedo[stray]=np.clip(base,0,255).astype(np.uint8)
        albedo=albedo[near[0],near[1]]
        rgb=albedo.astype(np.float32)/255;mx=rgb.max(2);mn=rgb.min(2);saturation=(mx-mn)/np.maximum(mx,.001)
Image.fromarray(albedo).save(textures/(name+'_Albedo.png'))
gold=(rgb[:,:,0]>.40)&(rgb[:,:,1]>.24)&(rgb[:,:,1]<rgb[:,:,0]*.86)&(rgb[:,:,2]<rgb[:,:,1]*.72)&(saturation>.32)
silver=(saturation<.14)&(mx>.32)&(mx<.80)&(rgb[:,:,2]>rgb[:,:,0]*.97)
if name=='06_Quan_vua_Denas':gold&=(height>.86)|(height<.65)
if name=='05_Quan_hau_Denas':gold&=(height>.91)|(height<.70)
# Skin and leather can share the reference's warm gold hue. Keep the known hand
# regions dielectric instead of treating every yellow pixel as reflective metal.
world_x=-(position[:,:,0]/(1.15/camera['diameter'])+camera['render_center'][0]);x=(world_x-(V[:,0].min()+V[:,0].max())*.5)/np.ptp(V[:,2])
skin=np.zeros_like(mask)
if name=='01_Quan_tot_Denas':skin=(x>.10)&(height>.39)&(height<.57)
if name=='03_Quan_ngua_Denas':skin=(abs(x)<.18)&(height>.56)&(height<.80)
if name=='04_Quan_tuong_Denas':skin=((x<-.12)&(height>.48)&(height<.72))|((x>.10)&(height>.60)&(height<.82))
if name=='05_Quan_hau_Denas':skin=((x<-.12)&(height>.48)&(height<.73))|((x>.10)&(height>.55)&(height<.77))
if name=='06_Quan_vua_Denas':skin=(abs(x)<.16)&(height>.45)&(height<.65)
gold&=~skin;silver&=~skin
metal=np.where(gold,.58,np.where(silver,.32,0)).astype(np.float32);roughness=np.where(gold,.38,np.where(silver,.49,.77)).astype(np.float32)
# Keep the stone pedestal and soft fur nonmetallic.
metal[height<.22]=0;roughness[height<.22]=.88
metal[~mask]=metal[near[0],near[1]][~mask];roughness[~mask]=roughness[near[0],near[1]][~mask]
Image.fromarray(np.round(metal*255).astype(np.uint8)).save(textures/(name+'_Metallic.png'));Image.fromarray(np.round(roughness*255).astype(np.uint8)).save(textures/(name+'_Roughness.png'))
unity=np.zeros((SIZE,SIZE,4),np.uint8);unity[:,:,0]=np.round(metal*255);unity[:,:,3]=np.round((1-roughness)*255);Image.fromarray(unity).save(textures/(name+'_MetallicSmoothness.png'))
report={'name':name,'atlas_size':[SIZE,SIZE],'surface_texels':int(mask.sum()),'direct_multiview_surface_coverage':coverage,'occluded_surface_fill':'Physical position + surface normal neighbors, never unrelated UV islands','unique_surface_UV':True,'all_uv_gutters_dilated':True,'geometry_source':'Coherent volumetric sculpt, not contour volume','source_views':len(views)}
(OUT/'Geometry'/(name+'_bake_report.json')).write_text(json.dumps(report,indent=2));print('SURFACE_ATLAS_BAKED',json.dumps(report),flush=True)
