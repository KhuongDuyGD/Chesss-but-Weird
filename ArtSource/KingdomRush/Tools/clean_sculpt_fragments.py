"""Remove isolated reconstruction debris; keep the king's intended magic motes."""
from pathlib import Path
import numpy as np,json
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
SRC=Path(__file__).resolve().parents[1];G=SRC/'Polish3D/Geometry'
for path in sorted((SRC.parents[1]/'Assets/Audio/MusicPackage/KingdomRushCollab').glob('0*.png')):
    name=path.stem;d=np.load(G/(name+'_UV.npz'));V=d['vertices'];F=d['faces'];N=d['normals'];UV=d['uv'];a=F.ravel();b=F[:,[1,2,0]].ravel()
    k,labels=connected_components(coo_matrix((np.ones(len(a)),(a,b)),shape=(len(V),len(V))).tocsr(),directed=False);counts=np.bincount(labels)
    drop=counts<100
    if name=='06_Quan_vua_Veznan':drop&=np.array([V[labels==i,2].mean()<.7 for i in range(k)])
    if name=='03_Quan_ngua_Denas':drop|=(counts<450)&np.array([V[labels==i,2].mean()<-.8 for i in range(k)])
    keepv=~drop[labels];keepf=keepv[F].all(1);remap=np.cumsum(keepv)-1
    v=V[keepv];f=remap[F[keepf]];n=N[keepv];uv=UV[keepf]
    np.savez_compressed(G/(name+'.npz'),vertices=v,faces=f,normals=n);np.savez_compressed(G/(name+'_UV.npz'),vertices=v,faces=f,normals=n,uv=uv)
    report=json.loads((G/(name+'_report.json')).read_text());report['removed_isolated_debris_vertices']=int((~keepv).sum());report['triangles']=len(f);report['vertices']=len(v);report['dimensions']=np.ptp(v,axis=0).tolist();(G/(name+'_report.json')).write_text(json.dumps(report,indent=2))
    print('ISOLATED_DEBRIS_REMOVED',name,int((~keepv).sum()),flush=True)
