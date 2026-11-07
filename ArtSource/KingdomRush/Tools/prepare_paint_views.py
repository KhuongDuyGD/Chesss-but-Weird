"""CPU rasterization of the official paint conditioning conventions and unique UVs."""
from pathlib import Path
import numpy as np,cv2,json,sys,xatlas
SRC=Path(__file__).resolve().parents[1];OUT=SRC/'Polish3D';name=sys.argv[1];D=np.load(OUT/'Geometry'/(name+'.npz'));V=D['vertices'];F=D['faces']
folder=OUT/'Views'/name;folder.mkdir(parents=True,exist_ok=True)
# Match the official MeshRender's GLTF-to-renderer transform and normalization.
R=-V;center=(R.min(0)+R.max(0))/2;diameter=2*np.linalg.norm(R-center,axis=1).max();R=(R-center)*(1.15/diameter)
normal=np.cross(R[F[:,1]]-R[F[:,0]],R[F[:,2]]-R[F[:,0]]);normal/=np.maximum(np.linalg.norm(normal,axis=1,keepdims=True),1e-12)
N=768;scale=1.2;metadata=[]
for index,(elev,azim) in enumerate([(0,0),(0,90),(0,180),(0,270),(90,0),(-90,180)]):
    a=np.deg2rad(azim+90);e=np.deg2rad(-elev)
    eye=np.array([np.cos(e)*np.cos(a),np.cos(e)*np.sin(a),np.sin(e)])
    right=np.cross(-eye,[0,0,1]);right/=np.linalg.norm(right);up=np.cross(right,-eye)
    # The official CUDA rasterizer stores increasing clip Y in increasing image
    # rows. Keep that convention, including the model's renderer-axis inversion.
    p=np.stack([R@right,R@up],1)/scale*N+N/2;z=R@eye
    depth=np.full((N,N),-np.inf,np.float32);nmap=np.ones((N,N,3),np.float32);pos=np.ones((N,N,3),np.float32)
    for fi,face in enumerate(F):
        tri=p[face];lo=np.maximum(np.floor(tri.min(0)).astype(int),0);hi=np.minimum(np.ceil(tri.max(0)).astype(int),N-1)
        if np.any(hi<lo):continue
        xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1)+.5,np.arange(lo[1],hi[1]+1)+.5)
        t0,t1,t2=tri;den=(t1[1]-t2[1])*(t0[0]-t2[0])+(t2[0]-t1[0])*(t0[1]-t2[1])
        if abs(den)<1e-10:continue
        w0=((t1[1]-t2[1])*(xx-t2[0])+(t2[0]-t1[0])*(yy-t2[1]))/den;w1=((t2[1]-t0[1])*(xx-t2[0])+(t0[0]-t2[0])*(yy-t2[1]))/den;w2=1-w0-w1
        zz=w0*z[face[0]]+w1*z[face[1]]+w2*z[face[2]];area=depth[lo[1]:hi[1]+1,lo[0]:hi[0]+1]
        take=(w0>=0)&(w1>=0)&(w2>=0)&(zz>area);area[take]=zz[take]
        nmap[lo[1]:hi[1]+1,lo[0]:hi[0]+1][take]=normal[fi]*.5+.5
        world=w0[:,:,None]*R[face[0]]+w1[:,:,None]*R[face[1]]+w2[:,:,None]*R[face[2]]
        pos[lo[1]:hi[1]+1,lo[0]:hi[0]+1][take]=(.5-world/1.15)[take]
    cv2.imwrite(str(folder/f'normal_{index}.png'),np.clip(nmap[:,:,::-1]*255,0,255).astype(np.uint8));cv2.imwrite(str(folder/f'position_{index}.png'),np.clip(pos[:,:,::-1]*255,0,255).astype(np.uint8))
    np.save(folder/f'depth_{index}.npy',depth)
    metadata.append({'index':index,'elevation':elev,'azimuth':azim,'eye':eye.tolist(),'right':right.tolist(),'up':up.tolist()})
    print('PAINT_CONDITION_READY',name,index,flush=True)
(folder/'cameras.json').write_text(json.dumps({'diameter':float(diameter),'render_center':center.tolist(),'scale':scale,'resolution':N,'cameras':metadata},indent=2))
if (OUT/'Geometry'/(name+'_UV.npz')).exists():
    print('UNIQUE_UV_REUSED',name,flush=True);sys.exit(0)
atlas=xatlas.Atlas();atlas.add_mesh(V.astype(np.float32),F.astype(np.uint32));pack=xatlas.PackOptions();pack.resolution=4096;pack.padding=16
charts=xatlas.ChartOptions();charts.max_iterations=4;atlas.generate(charts,pack)
mapping,indices,uv=atlas[0]
assert np.array_equal(mapping[indices],F),'UV mapping must preserve source triangle order'
np.savez_compressed(OUT/'Geometry'/(name+'_UV.npz'),vertices=V,faces=F,normals=D['normals'],uv=uv[indices])
print('UNIQUE_UV_READY',name,'atlas',atlas.width,atlas.height,'utilization',atlas.utilization,flush=True)
