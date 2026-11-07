"""Bake deliberately designed faction colors onto unseen surfaces only.
The left half of each atlas, containing the original visible artwork, is unchanged.
"""
from pathlib import Path
import numpy as np,cv2,json,sys
from PIL import Image
from scipy.ndimage import distance_transform_edt,gaussian_filter
SRC=Path(__file__).resolve().parents[1]
names=sys.argv[1:] or sorted(p.name.removesuffix('_textured_surface.npz') for p in (SRC/'Reconstructions').glob('*_textured_surface.npz'))
for name in names:
    D=np.load(SRC/'Reconstructions'/(name+'_textured_surface.npz'));fit=json.loads((SRC/'Reconstructions'/(name+'_artwork_camera.json')).read_text())
    V=D['vertices'];F=D['faces'];uv=D['uv'];right,up,eye=[np.array(fit[k]) for k in ['right','up','eye_direction']];sx,sy=fit['scale_xy'];ox,oy=fit['offset_xy'];W,H=fit['image_size'];N=512
    ids=np.unique(F[(uv[:,:,0]>.5).all(1)]);points=V[ids];p=np.stack([(points@right)*sx+ox,-(points@up)*sy+oy],1)*[N/W,N/H]
    xx=np.clip(p[:,0].astype(int),0,N-1);yy=np.clip(p[:,1].astype(int),0,N-1);depth=np.full(N*N,np.inf,np.float32)
    np.minimum.at(depth,yy*N+xx,points@eye);depth=depth.reshape(N,N);valid=np.isfinite(depth)
    _,nn=distance_transform_edt(~valid,return_indices=True);depth=gaussian_filter(depth[nn[0],nn[1]],1.0)
    y,x=np.mgrid[:N,:N];u=((x+.5)*W/N-ox)/sx;v=(oy-(y+.5)*H/N)/sy
    z=u*right[2]+v*up[2]+depth*eye[2];height=np.clip((z-V[:,2].min())/np.ptp(V[:,2]),0,1);px=(x+.5)/N
    dark=name.endswith('Veznan');kind=int(name[:2]);base=[93,72,94] if dark else [137,128,116]
    color=np.zeros((N,N,3),np.float32);color[:]=base
    def region(low,high,c,extra=None):
        selected=(height>=low)&(height<high)
        if extra is not None:selected&=extra
        color[selected]=c
    if kind==1:
        region(.22,.64,[61,50,64] if dark else [122,130,138]);region(.22,.43,[57,52,51])
        region(.64,.77,[139,35,151] if dark else [44,78,142])
        region(.77,.9,[106,157,37] if dark else [145,151,157])
        region(.90,1.01,[112,100,119] if dark else [41,76,148])
    elif kind==2:
        region(.22,.88,[76,59,83] if dark else [144,134,117]);region(.88,1.01,[106,91,116] if dark else [43,77,143])
        if dark:
            for low in [.20,.48,.80]:region(low,low+.025,[139,37,155])
        else:region(.85,1.01,[220,174,54],px<.46)
    elif kind==3:
        region(.22,.58,[47,45,53] if dark else [204,194,168]);region(.58,.80,[82,62,96] if dark else [40,75,142])
        region(.80,.91,[111,104,123] if dark else [146,152,157]);region(.91,1.01,[143,36,157] if dark else [40,75,145])
        if dark:region(.22,.29,[113,205,33])
    elif kind==4:
        region(.22,.73,[105,36,129] if dark else [42,76,142]);region(.73,1.01,[100,32,121] if dark else [222,209,182])
        region(.22,.87,[190,140,51],px<.28 if not dark else px>.74)
        if dark:region(.87,1.01,[128,216,35],px>.70)
    elif kind==5:
        region(.22,.70,[112,37,135] if dark else [41,76,145]);region(.70,.78,[55,39,66] if dark else [232,220,204])
        region(.78,.93,[48,35,59] if dark else [229,178,49]);region(.93,1.01,[220,168,48])
        region(.27,.91,[163,112,46] if dark else [218,165,43],px>.72)
        region(.91,1.01,[125,211,33] if dark else [53,137,220],px>.73)
    else:
        region(.21,.655,[101,33,128] if dark else [39,75,143]);region(.655,.765,[104,35,132] if dark else [227,213,196])
        region(.765,.925,[99,31,126] if dark else [225,172,47]);region(.925,1.01,[211,198,181] if dark else [238,186,53])
        if dark:
            region(.29,.89,[159,113,48],px>.73);region(.89,1.01,[130,216,31],px>.72)
    # Thin gold garment hems and dark faction base rims follow the established palette.
    if kind>=4:
        region(.225,.245,[211,160,48]);region(.245,.65,[207,155,45],(px<.25)|(px>.80))
    if dark:
        region(.195,.22,[142,39,156]);region(.005,.025,[110,35,127])
    # Bake gentle sculpt lighting from the actual inferred back depth, rather than
    # spreading random front-face skin colors onto capes, hair and stone.
    dy,dx=np.gradient(depth);dx*=N*sx/W;dy*=N*sy/H
    normal=-eye+dx[:,:,None]*right-dy[:,:,None]*up;normal/=np.maximum(np.linalg.norm(normal,axis=2,keepdims=True),1e-6)
    light=np.array([-2,4,5],float);light/=np.linalg.norm(light);shade=.76+.26*np.maximum(normal@light,0)
    color=np.clip(color*shade[:,:,None],0,255);color=cv2.GaussianBlur(color,(0,0),.8)
    path=SRC/'Reconstructions/Textures'/(name+'_OriginalAtlas.png');atlas=np.array(Image.open(path).convert('RGB'));size=atlas.shape[0]
    atlas[:,size:]=cv2.resize(color.astype(np.uint8),(size,size),interpolation=cv2.INTER_CUBIC);Image.fromarray(atlas).save(path)
    print('INFERRED_BACK_PAINTED',name,flush=True)
