"""Non-destructive derivatives of approved painted surfaces and URP foliage shader.
No mesh, UV, emblem or source albedo is replaced. Run with the existing paint Python.
"""
from pathlib import Path
import json, numpy as np, cv2
from PIL import Image, ImageFilter

SRC=Path(__file__).resolve().parents[1]; ROOT=SRC.parents[1]
ASSETS=ROOT/'Assets/Skins/KingdomRushCollab'
OUT=ASSETS/'Textures/Advanced'; OUT.mkdir(parents=True,exist_ok=True)
manifest=json.loads((SRC/'model_manifest.json').read_text())
records=[]

def positions(name,n=1024):
    data=np.load(SRC/'Polish3D/Geometry'/(name+'_UV.npz'))
    v=data['vertices'].copy(); lo=v.min(0);hi=v.max(0)
    v-=[(lo[0]+hi[0])/2,(lo[1]+hi[1])/2,lo[2]];v/=(hi[2]-lo[2])
    pos=np.zeros((n,n,3),np.float32); valid=np.zeros((n,n),bool)
    for face,uv in zip(data['faces'],data['uv']):
        p=uv.copy()*[n-1,-(n-1)]+[0,n-1]
        x0,y0=np.maximum(np.floor(p.min(0)).astype(int),0);x1,y1=np.minimum(np.ceil(p.max(0)).astype(int),n-1)
        if x1<x0 or y1<y0:continue
        a,b,c=p;den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(den)<1e-8:continue
        yy,xx=np.mgrid[y0:y1+1,x0:x1+1]
        w0=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
        w1=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;w2=1-w0-w1
        inside=(w0>=-.001)&(w1>=-.001)&(w2>=-.001)
        pp=w0[...,None]*v[face[0]]+w1[...,None]*v[face[1]]+w2[...,None]*v[face[2]]
        pos[y0:y1+1,x0:x1+1][inside]=pp[inside];valid[y0:y1+1,x0:x1+1]|=inside
    return pos,valid

for spec in manifest['models']:
    name=spec['name']; image=Image.open(ROOT/spec['texture']).convert('RGB')
    # Small-radius sharpening restores texture sampling clarity without painting new insignias.
    sharp=np.asarray(image.filter(ImageFilter.UnsharpMask(radius=1.15,percent=48,threshold=3)),dtype=np.float32)/255
    # Lift only baked shadow values; white fur, gold and faction hues retain headroom.
    lum=sharp@np.array([.2126,.7152,.0722]);lift=.030*np.exp(-((lum-.16)/.20)**2)
    sharp=np.clip(sharp+lift[...,None]*(1-sharp),0,1)
    albedo=OUT/(name+'_Clarity.png'); Image.fromarray(np.uint8(sharp*255+.5)).save(albedo)
    record={'name':name,'albedo':albedo.relative_to(ROOT).as_posix(),'emission':None,'magicAnchor':None,'emissiveCoverage':0}
    if name.endswith('Veznan'):
        pos,valid=positions(name); rgb=np.asarray(image.resize((1024,1024),Image.Resampling.LANCZOS),np.float32)/255
        x,y,z=pos[:,:,0],pos[:,:,1],pos[:,:,2]
        # Existing lime magic only. Green hands/skin, purple fabric and gold never become fire.
        green=np.clip((rgb[:,:,1]-np.maximum(rgb[:,:,0]*.85,rgb[:,:,2]*1.25)-.07)*4,0,1)
        kind=int(name[:2]); magical=np.zeros(valid.shape,bool)
        if kind==2:magical=(z>.30)&(z<.80) # existing portal and skull magic
        elif kind==3:magical=((z>.77)|((z>.32)&(z<.56))) # enchanted weapon and mount mouth
        elif kind>=4:magical=(z>.79)|((z>.63)&(z<.83)&(abs(x)<.14)&(y<-.025))
        # Goblin skin is deliberately excluded; its eyes keep the approved paint.
        mask=green*magical*valid
        emission=np.clip(rgb*1.55,0,1)*mask[...,None]
        emission=cv2.dilate(emission,np.ones((3,3),np.uint8))
        path=OUT/(name+'_Magic.png');Image.fromarray(np.uint8(emission*255+.5)).save(path)
        record['emission']=path.relative_to(ROOT).as_posix();record['emissiveCoverage']=float((mask>.05).sum()/max(1,valid.sum()))
        if kind>=4:
            staff=(mask>.15)&(z>.80)&(abs(x)>.15)
            if staff.sum()>4:
                weight=mask[staff]; p=np.average(pos[staff],axis=0,weights=weight)
                h=spec['dimensions_m'][2]; record['magicAnchor']=[float(p[0]*h),float(p[2]*h),float(-p[1]*h)]
        print(name,'magic coverage',record['emissiveCoverage'],'anchor',record['magicAnchor'],flush=True)
    records.append(record)

(ASSETS/'advanced_surface_manifest.json').write_text(json.dumps({'models':records},indent=2))

# Preserve URP's Lit lighting, GBuffer, shadows, depth normals, instancing and material UI.
# Only its foliage vertices receive the same gentle displacement in all rendering passes.
package=next((ROOT/'Library/PackageCache').glob('com.unity.render-pipelines.universal@*'))
shader=(package/'Shaders/Lit.shader').read_text().replace('Shader "Universal Render Pipeline/Lit"','Shader "ChessButWeird/KingdomRush/Foliage"',1)
passes={'LitForwardPass.hlsl':'LitPassVertex','ShadowCasterPass.hlsl':'ShadowPassVertex','LitGBufferPass.hlsl':'LitGBufferPassVertex','DepthOnlyPass.hlsl':'DepthOnlyVertex','LitDepthNormalsPass.hlsl':'DepthNormalsVertex'}
for include,vertex in passes.items():
    shader=shader.replace('#pragma vertex '+vertex,'#pragma vertex KRWindVertex')
    line='#include "Packages/com.unity.render-pipelines.universal/Shaders/'+include+'"'
    field='position' if vertex=='DepthOnlyVertex' else 'positionOS'
    shader=shader.replace(line,line+'\n            #include "KRWind.hlsl"\n            Varyings KRWindVertex(Attributes input) { input.'+field+'.xyz = KRWindPosition(input.'+field+'.xyz); return '+vertex+'(input); }')
folder=ASSETS/'Shaders/Advanced';folder.mkdir(parents=True,exist_ok=True)
(folder/'KRFoliage.shader').write_text('// Derived from installed URP 17.4 Lit.shader, Unity Companion License.\n'+shader)
(folder/'KRWind.hlsl').write_text('''#ifndef KR_WIND_INCLUDED
#define KR_WIND_INCLUDED
float _KRWindStrength;
float3 KRWindPosition(float3 p)
{
    // Imported FBX meshes retain Blender's local axes: animate in world space.
    float3 world = TransformObjectToWorld(p);
    float scale = max(0.001, length(TransformObjectToWorldDir(float3(1,0,0), false)));
    float height = (world.y - TransformObjectToWorld(float3(0,0,0)).y) / scale;
    float weight = smoothstep(0.0, 2.0, height);
    float phase = (world.x * 0.63 + world.z * 0.41) / scale;
    float sway = sin(_Time.y * 0.78 + phase) + 0.28 * sin(_Time.y * 1.23 + phase * 1.7);
    world.xz += float2(0.82, 0.38) * (sway * 0.027 * weight * _KRWindStrength * scale);
    return TransformWorldToObject(world);
}
#endif
''')
print('ADVANCED_SURFACES_READY',len(records),flush=True)
