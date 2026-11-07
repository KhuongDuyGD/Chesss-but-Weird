"""Run local image-to-mesh reconstruction from unaltered supplied original PNGs.
This creates full volumetric hypotheses; it does not certify exact visual likeness.
Uses the native ComfyUI Hunyuan3D 2.1 workflow, no paid API or image upload.
"""
import json,time,urllib.request,sys
from pathlib import Path
SRC=Path(__file__).resolve().parents[1];BASE='http://127.0.0.1:8190'
def request(path,data=None):
    req=urllib.request.Request(BASE+path,data=None if data is None else json.dumps(data).encode(),headers={'Content-Type':'application/json'})
    return json.load(urllib.request.urlopen(req,timeout=30))
def graph(name,seed=62061106):
    return {
        '1':{'class_type':'ImageOnlyCheckpointLoader','inputs':{'ckpt_name':'hunyuan_3d_v2.1.safetensors'}},
        '2':{'class_type':'LoadImage','inputs':{'image':name+'.png'}},
        '3':{'class_type':'CLIPVisionEncode','inputs':{'clip_vision':['1',1],'image':['2',0],'crop':'none'}},
        '4':{'class_type':'Hunyuan3Dv2Conditioning','inputs':{'clip_vision_output':['3',0]}},
        '5':{'class_type':'EmptyLatentHunyuan3Dv2','inputs':{'resolution':4096,'batch_size':1}},
        '6':{'class_type':'ModelSamplingAuraFlow','inputs':{'model':['1',0],'shift':1.0}},
        '7':{'class_type':'KSampler','inputs':{'model':['6',0],'positive':['4',0],'negative':['4',1],'latent_image':['5',0],'seed':seed,'steps':40,'cfg':5.0,'sampler_name':'euler','scheduler':'normal','denoise':1.0}},
        '8':{'class_type':'VAEDecodeHunyuan3D','inputs':{'vae':['1',2],'samples':['7',0],'num_chunks':8000,'octree_resolution':384}},
        '9':{'class_type':'VoxelToMesh','inputs':{'voxel':['8',0],'algorithm':'surface net','threshold':.6}},
        '10':{'class_type':'SaveGLB','inputs':{'mesh':['9',0],'filename_prefix':name}}
    }
names=sys.argv[1:] or ['06_Quan_vua_Denas']
for name in names:
    g=graph(name);(SRC/'Reconstructions').mkdir(exist_ok=True)
    (SRC/'Reconstructions'/(name+'_workflow.json')).write_text(json.dumps(g,indent=2))
    response=request('/prompt',{'prompt':g,'client_id':'kingdom-rush-art'})
    if response.get('node_errors'):raise RuntimeError(response['node_errors'])
    job=response['prompt_id'];print('RECONSTRUCTION_QUEUED',name,job,flush=True)
    while True:
        history=request('/history/'+job)
        if job in history:
            h=history[job]
            (SRC/'Reconstructions'/(name+'_history.json')).write_text(json.dumps(h,indent=2))
            if h['status']['status_str']!='success':raise RuntimeError(h['status'])
            print('RECONSTRUCTION_DONE',name,json.dumps(h['outputs']),flush=True);break
        time.sleep(3)
