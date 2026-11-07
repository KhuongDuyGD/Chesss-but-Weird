"""Download public local-paint weights, with resumable curl transfers."""
from pathlib import Path
import urllib.request,json,time,subprocess,concurrent.futures
BASE=Path('D:/Comfy-Desktop/KingdomRush-Tools/weights')
jobs=[]
for repo,folder,prefix in [('tencent/Hunyuan3D-2.1','HunyuanPaint21','hunyuan3d-paintpbr-v2-1/'),('facebook/dinov2-giant','dinov2-giant','')]:
    for attempt in range(5):
        try:
            req=urllib.request.Request('https://huggingface.co/api/models/'+repo+'?blobs=true',headers={'User-Agent':'KingdomRushLocalArt/1.0'})
            data=json.load(urllib.request.urlopen(req,timeout=30));break
        except Exception:
            if attempt==4:raise
            time.sleep(2)
    for item in data['siblings']:
        file=item['rfilename']
        if prefix and not file.startswith(prefix):continue
        if not prefix and file not in ['config.json','preprocessor_config.json','model.safetensors']:continue
        jobs.append((repo,file,BASE/folder/file,item.get('size',0)))
def download(job):
    repo,file,dest,size=job;dest.parent.mkdir(parents=True,exist_ok=True)
    if size and dest.exists() and dest.stat().st_size==size:return
    print('DOWNLOADING',file,flush=True)
    command=['curl.exe','--fail','--location','--retry','5','--retry-all-errors','--connect-timeout','30','--silent','--show-error','--continue-at','-','--output',str(dest),'https://huggingface.co/'+repo+'/resolve/main/'+file+'?download=true']
    subprocess.run(command,check=True)
    if size and dest.stat().st_size!=size:raise RuntimeError('Incomplete download: '+file)
    print('DOWNLOADED',file,dest.stat().st_size,flush=True)
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:list(pool.map(download,jobs))
print('ALL_LOCAL_PAINT_WEIGHTS_READY',flush=True)
