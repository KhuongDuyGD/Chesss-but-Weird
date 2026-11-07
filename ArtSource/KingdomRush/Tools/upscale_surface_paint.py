"""Tiled RRDB surface-detail upscaling using the official Real-ESRGAN x4 weights.
RRDB architecture follows BasicSR (BSD-3-Clause), https://github.com/XPixelGroup/BasicSR.
"""
from pathlib import Path
import torch,torch.nn as nn,torch.nn.functional as F,numpy as np,sys
from PIL import Image
class Dense(nn.Module):
    def __init__(self):
        super().__init__()
        for i in range(1,6):setattr(self,'conv'+str(i),nn.Conv2d(64+32*(i-1),32 if i<5 else 64,3,1,1))
    def forward(self,x):
        values=[x]
        for i in range(1,5):values.append(F.leaky_relu(getattr(self,'conv'+str(i))(torch.cat(values,1)),.2,inplace=True))
        return x+self.conv5(torch.cat(values,1))*.2
class RRDB(nn.Module):
    def __init__(self):
        super().__init__();self.rdb1=Dense();self.rdb2=Dense();self.rdb3=Dense()
    def forward(self,x):return x+self.rdb3(self.rdb2(self.rdb1(x)))*.2
class Net(nn.Module):
    def __init__(self):
        super().__init__();self.conv_first=nn.Conv2d(3,64,3,1,1);self.body=nn.Sequential(*[RRDB() for _ in range(23)])
        for key in ['conv_body','conv_up1','conv_up2','conv_hr']:setattr(self,key,nn.Conv2d(64,64,3,1,1))
        self.conv_last=nn.Conv2d(64,3,3,1,1)
    def forward(self,x):
        h=self.conv_first(x);h=h+self.conv_body(self.body(h))
        h=F.leaky_relu(self.conv_up1(F.interpolate(h,scale_factor=2,mode='nearest')),.2,inplace=True)
        h=F.leaky_relu(self.conv_up2(F.interpolate(h,scale_factor=2,mode='nearest')),.2,inplace=True)
        return self.conv_last(F.leaky_relu(self.conv_hr(h),.2,inplace=True))
SRC=Path(__file__).resolve().parents[1];names=sys.argv[1:] or ['06_Quan_vua_Denas']
net=Net();weights=torch.load('D:/Comfy-Desktop/KingdomRush-Tools/weights/RealESRGAN_x4plus.pth',map_location='cpu',weights_only=True)
net.load_state_dict(weights.get('params_ema',weights.get('params',weights)));net=net.to(device='cuda',dtype=torch.float16).eval()
for name in names:
    folder=SRC/'Polish3D/Views'/name/'Generated'
    for view in range(6):
        if (folder/f'albedo_{view}_detail.png').exists() and (folder/f'albedo_{view}_detail.png').stat().st_mtime>(folder/f'albedo_{view}.png').stat().st_mtime:continue
        source=Image.open(folder/f'albedo_{view}.png').convert('RGB');array=np.array(source).astype(np.float32)/255
        tensor=torch.from_numpy(array.transpose(2,0,1)).unsqueeze(0).to(device='cuda',dtype=torch.float16);h,w=array.shape[:2];result=np.zeros((h*4,w*4,3),np.uint8)
        tile=256;pad=20
        with torch.inference_mode():
            for y in range(0,h,tile):
                for x in range(0,w,tile):
                    x0=max(0,x-pad);y0=max(0,y-pad);x1=min(w,x+tile+pad);y1=min(h,y+tile+pad)
                    out=net(tensor[:,:,y0:y1,x0:x1]).clamp(0,1)[0].permute(1,2,0).float().cpu().numpy()
                    th=min(tile,h-y);tw=min(tile,w-x);result[y*4:(y+th)*4,x*4:(x+tw)*4]=np.round(out[(y-y0)*4:(y-y0+th)*4,(x-x0)*4:(x-x0+tw)*4]*255).astype(np.uint8)
        Image.fromarray(result).save(folder/f'albedo_{view}_detail.png')
        print('SURFACE_DETAIL_UPSCALED',name,view,flush=True)
