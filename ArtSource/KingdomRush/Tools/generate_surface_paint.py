"""Generate mutually conditioned surface albedo/material views locally, without CUDA extensions."""
from pathlib import Path
import os,sys,gc,json,time
os.environ['HF_HUB_OFFLINE']='1';os.environ['TRANSFORMERS_OFFLINE']='1';os.environ['HF_HUB_DISABLE_TELEMETRY']='1'
import torch,numpy as np
from PIL import Image
SRC=Path(__file__).resolve().parents[1];OUT=SRC/'Polish3D';VENDOR=Path('D:/Comfy-Desktop/KingdomRush-Tools/Hunyuan3D-2.1/hy3dpaint');WEIGHTS=Path('D:/Comfy-Desktop/KingdomRush-Tools/weights')
sys.path.insert(0,str(VENDOR))
from hunyuanpaintpbr.unet.modules import Dino_v2,UNet2p5DConditionModel
from hunyuanpaintpbr.pipeline import HunyuanPaintPipeline
from diffusers import AutoencoderKL,UniPCMultistepScheduler
from transformers import CLIPTextModel,CLIPTokenizer,CLIPImageProcessor
names=sys.argv[1:] or ['06_Quan_vua_Denas'];MODEL=WEIGHTS/'HunyuanPaint21/hunyuan3d-paintpbr-v2-1'
references={}
dino=Dino_v2(str(WEIGHTS/'dinov2-giant')).to(dtype=torch.float16,device='cuda').eval()
for name in names:
    image=Image.open(SRC.parents[1]/'Assets/Audio/MusicPackage/KingdomRushCollab'/(name+'.png')).convert('RGB').resize((768,768),Image.Resampling.LANCZOS)
    with torch.inference_mode():features=dino(image).detach().cpu()
    references[name]=(image,features)
    print('DINO_REFERENCE_ENCODED',name,flush=True)
del dino;gc.collect();torch.cuda.empty_cache()
print('LOADING_MULTIVIEW_PAINT',flush=True)
unet=UNet2p5DConditionModel.from_pretrained(str(MODEL/'unet'),torch_dtype=torch.float16)
vae=AutoencoderKL.from_pretrained(str(MODEL/'vae'),torch_dtype=torch.float16,local_files_only=True)
text=CLIPTextModel.from_pretrained(str(MODEL/'text_encoder'),torch_dtype=torch.float16,local_files_only=True)
tokenizer=CLIPTokenizer.from_pretrained(str(MODEL/'tokenizer'),local_files_only=True)
processor=CLIPImageProcessor.from_pretrained(str(MODEL/'feature_extractor'),local_files_only=True)
scheduler=UniPCMultistepScheduler.from_pretrained(str(MODEL/'scheduler'),timestep_spacing='trailing',local_files_only=True)
pipeline=HunyuanPaintPipeline(vae,text,tokenizer,unet,scheduler,processor);pipeline.eval();pipeline.view_size=512
pipeline.to('cuda');pipeline.vae.enable_slicing()
print('MULTIVIEW_PAINT_LOADED',flush=True)
for name in names:
    folder=OUT/'Views'/name
    while not (folder/'cameras.json').exists():time.sleep(2)
    generated=folder/'Generated';generated.mkdir(exist_ok=True)
    normals=[Image.open(folder/f'normal_{i}.png').convert('RGB') for i in range(6)]
    positions=[Image.open(folder/f'position_{i}.png').convert('RGB') for i in range(6)]
    image,features=references[name]
    resolution=normals[0].width
    with torch.inference_mode():
        result=pipeline([image],num_inference_steps=int(os.environ.get('KR_PAINT_STEPS','15')),prompt='high quality',width=resolution,height=resolution,num_in_batch=6,images_normal=[normals],images_position=[positions],dino_hidden_states=features.to('cuda'),guidance_scale=3.0,generator=torch.Generator(device='cuda').manual_seed(int(os.environ.get('KR_PAINT_SEED','62061106'))),sync_condition=None).images
    for i,picture in enumerate(result):picture.save(generated/(('albedo_' if i<6 else 'mr_')+str(i%6)+'.png'))
    sheet=Image.new('RGB',(512*3,512*2),'white')
    for i,picture in enumerate(result[:6]):sheet.paste(picture.resize((512,512)),(i%3*512,i//3*512))
    sheet.save(generated/'SurfaceViews.jpg',quality=95)
    print('SURFACE_PAINT_GENERATED',name,'views',len(result),'peak_vram_GB',torch.cuda.max_memory_allocated()/2**30,flush=True)
    torch.cuda.empty_cache()
