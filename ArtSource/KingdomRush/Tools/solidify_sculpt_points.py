"""Resolve intersecting surface-net shells without relying on their winding."""
from pathlib import Path
import numpy as np,sys
from scipy.ndimage import binary_closing,binary_fill_holes,binary_dilation,label,gaussian_filter
from skimage.measure import marching_cubes
SRC=Path(__file__).resolve().parents[1];name=sys.argv[1];D=np.load(SRC/'Reconstructions'/(name+'_geometry.npz'));V=D['vertices'];pitch=.0055
padding=20 if name=='05_Quan_hau_Veznan' else 4
origin=V.min(0)-pitch*padding;shape=np.ceil((V.max(0)-origin)/pitch).astype(int)+padding+1
grid=np.zeros(shape,bool);indices=np.round((V-origin)/pitch).astype(int);grid[tuple(indices.T)]=True
grid=binary_closing(grid,iterations=2);grid=binary_dilation(grid,iterations=1);grid=binary_fill_holes(grid)
if name=='05_Quan_hau_Veznan':
    x=origin[0]+np.arange(shape[0])*pitch;y=origin[1]+np.arange(shape[1])*pitch;z=origin[2]+np.arange(shape[2])*pitch
    remove=((x[:,None,None]>.40)&(z[None,None,:]>.62))|((x[:,None,None]<-.41)&(y[None,:,None]<.12)&(z[None,None,:]>.24))
    grid[remove]=False
    for base,top in [((.59,.35,.60),.985),((-.59,-.11,.27),.70)]:
        bx,by,bz=base
        for branch in range(3):
            h=(top-bz)*(1 if branch==0 else .76);offset=(branch-1)*.09
            for i in range(25):
                t=i/24;r=(.105 if branch==0 else .07)*(1-t)**.7+.004
                center=np.array([bx+offset+(.05 if branch==0 else .07)*np.sin(t*5.2+branch*.6),by+.04*np.sin(t*5+branch*2),bz+t*h]);radii=np.array([r,r*.9,r*1.3])
                lo=np.maximum(np.floor((center-radii-origin)/pitch).astype(int),0);hi=np.minimum(np.ceil((center+radii-origin)/pitch).astype(int)+1,shape)
                xyz=np.meshgrid(*[origin[k]+np.arange(lo[k],hi[k])*pitch for k in range(3)],indexing='ij')
                inside=sum(((xyz[k]-center[k])/radii[k])**2 for k in range(3))<=1
                grid[lo[0]:hi[0],lo[1]:hi[1],lo[2]:hi[2]]|=inside
labels,count=label(grid);sizes=np.bincount(labels.ravel());sizes[0]=0;keep=sizes>max(sizes.max()*.0002,30);grid=keep[labels]
field=gaussian_filter(grid.astype(np.float32),.65)
vertices,faces,normals,values=marching_cubes(field,.501,spacing=(pitch,)*3,allow_degenerate=False)
vertices+=origin
np.savez_compressed(SRC/'Polish3D/Geometry'/(name+'_Solid.npz'),vertices=vertices,faces=faces)
print('SCULPT_POINTS_SOLIDIFIED',name,'voxels',int(grid.sum()),'faces',len(faces),flush=True)
