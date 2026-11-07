from pathlib import Path
import numpy as np
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
SRC=Path(__file__).resolve().parents[1];G=SRC/'Polish3D/Geometry'
for path in sorted((SRC.parents[1]/'Assets/Audio/MusicPackage/KingdomRushCollab').glob('0*.png')):
    name=path.stem;d=np.load(G/(name+'.npz'));v=d['vertices'];f=d['faces'];a=f[:,[0,1,2]].ravel();b=f[:,[1,2,0]].ravel()
    k,labels=connected_components(coo_matrix((np.ones(len(a)),(a,b)),shape=(len(v),len(v))).tocsr(),directed=False);counts=np.bincount(labels)
    print(name,'components',k,'small',[(int(counts[i]),np.round(v[labels==i].mean(0),3).tolist()) for i in np.where(counts<450)[0]])
