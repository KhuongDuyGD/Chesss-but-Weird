"""Palette-led, repeatable stylized surfaces, derived from the approved material roles.
No character paint or source PBR map is overwritten. Run with the existing paint Python.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image

SRC=Path(__file__).resolve().parents[1]; ROOT=SRC.parents[1]
ASSET=ROOT/'Assets/Skins/KingdomRushCollab'; OUT=ASSET/'Textures/StyleDirection'
OUT.mkdir(parents=True,exist_ok=True)
BASE=SRC/'AuthoringBase/faction_material_palette.json'
palette=json.loads(BASE.read_text()); n=1024
y,x=np.mgrid[0:n,0:n].astype(np.float32)/n
families={
 'Meadow':((.30,.59,.10),.94), 'Leaf':((.29,.56,.105),.91),
 'Limestone':((.79,.74,.58),.82), 'Ivory':((.84,.80,.68),.78),
 'Slate':((.075,.24,.36),.73), 'Bark':((.29,.16,.075),.92),
 'Water':((.045,.49,.68),.66), 'Blue':((.035,.25,.61),.66),
 'Basalt':((.205,.17,.255),.85), 'Scorched':((.205,.155,.225),.96),
 'Ash':((.43,.39,.47),.85), 'Bone':((.75,.69,.53),.87),
 'Cavity':((.055,.035,.065),.98)}
paths={}
def ash_noise(cells,seed):
    """Seamless, isotropic painted fields; no repeated diagonal crack function."""
    rng=np.random.default_rng(seed);grid=rng.random((cells,cells)).astype(np.float32)
    xx=x*cells;yy=y*cells;ix=xx.astype(np.int32);iy=yy.astype(np.int32)
    tx=xx-ix;ty=yy-iy;tx=tx*tx*(3-2*tx);ty=ty*ty*(3-2*ty)
    a=grid[iy%cells,ix%cells]*(1-tx)+grid[iy%cells,(ix+1)%cells]*tx
    b=grid[(iy+1)%cells,ix%cells]*(1-tx)+grid[(iy+1)%cells,(ix+1)%cells]*tx
    return a*(1-ty)+b*ty

for family,(base,roughness) in families.items():
    # Broad tonal groupings replace high-frequency grain, vein noise and dirt.
    field=np.sin(x*2*np.pi)*.45+np.cos(y*2*np.pi)*.30+np.sin((x+y)*4*np.pi)*.15
    amplitude=.055 if family=='Leaf' else .025 if family=='Cavity' else .045
    if family in ['Meadow','Scorched']:
        field=np.sin(x*16*np.pi)*.45+np.cos(y*12*np.pi)*.30+np.sin((x+y)*10*np.pi)*.15
        amplitude=.12 if family=='Meadow' else .10
    color=np.clip(np.array(base)[None,None,:]*(1+amplitude*field[...,None]),0,1)
    height=field*.003
    if family=='Water':
        phase=y*4+np.sin(x*2*np.pi)*.13+np.sin(x*4*np.pi)*.045
        line=(.5+.5*np.cos(phase*2*np.pi))**36
        broken=(.5+.5*np.cos(x*6*np.pi+y*2*np.pi))**5
        foam=line*broken*.34
        color=color*(1-foam[...,None])+np.array([.60,.88,.88])*foam[...,None]
        height=np.sin(phase*2*np.pi)*.009
    if family=='Basalt':
        crack=np.exp(-np.abs(np.sin((x*3+y*2)*np.pi+np.sin(y*2*np.pi)*.27))*85)
        color*=1-crack[...,None]*.15; height-=crack*.004
    if family=='Scorched':
        broad=ash_noise(12,4107);middle=ash_noise(32,4108);small=ash_noise(110,4109)
        ash=np.clip((broad*.72+middle*.28-.30)*1.6,0,1)
        char=np.clip((.48-middle)*1.5,0,.50)
        color=np.array([.205,.165,.225])[None,None,:]*(1-char[...,None])
        color=color*(1-ash[...,None]*.46)+np.array([.36,.32,.365])*ash[...,None]*.46
        color*=1+(small[...,None]-.5)*.075
        height=(middle-.5)*.006+(small-.5)*.004
        wx=(x-.5)*128;wy=(y-.5)*128;scorch=np.zeros_like(x)
        for px,py in [(-9,-3),(9,3),(-9,3),(9,-3),(-5,-10),(5,10)]:
            scorch=np.maximum(scorch,np.exp(-((wx-px)/1.7)**2-((wy-py)/1.25)**2))
        color*=1-scorch[...,None]*.34
    rgba=np.ones((n,n,4),np.float32); rgba[:,:,:3]=color
    family_paths=[]
    for suffix in ['Albedo','Normal','MetalSmooth']:
        if suffix=='Normal':
            dx=(np.roll(height,-1,1)-np.roll(height,1,1))*4
            dy=(np.roll(height,-1,0)-np.roll(height,1,0))*4
            normals=np.stack([-dx,-dy,np.ones_like(dx)],-1)
            normals/=np.linalg.norm(normals,axis=-1,keepdims=True)
            rgba[:,:,:3]=normals*.5+.5
        elif suffix=='MetalSmooth':
            rgba[:,:,:3]=0;rgba[:,:,3]=1-roughness
        path=OUT/('Style_'+family+'_'+suffix+'.png')
        Image.fromarray(np.uint8(np.clip(rgba,0,1)*255+.5)).save(path)
        family_paths.append(path.relative_to(ROOT).as_posix())
    paths[family]=family_paths

for material in palette['materials']:
    original=Path(material.get('albedo','')).name
    family=original.removeprefix('HQ_').removesuffix('_Albedo.png')
    if family not in paths:continue
    material['albedo'],material['normal'],material['metalSmooth']=paths[family]
    material['roughness']=families[family][1]
    if material['name']=='KR HQ Deep leaf':material['color']=[.72,.84,.88,1]
for name,family,tint in [('Leaf highlights','Leaf',[1.13,1.08,.94,1]),
                         ('Bone ivory','Bone',[1,1,1,1]),('Bone recess','Cavity',[1,1,1,1])]:
    a,norm,metal=paths[family]
    palette['materials'].append({'name':'KR HQ '+name,'color':tint,'metallic':0,
        'roughness':families[family][1],'albedo':a,'normal':norm,'metalSmooth':metal,
        'emission':[0,0,0,1],'emissionStrength':0})
palette['revision']='StylizedFantasy_ReleaseCleanup_20261107'
(ASSET/'faction_material_palette.json').write_text(json.dumps(palette,indent=2))
print('STYLE_SURFACES_READY',len(paths)*3,len(palette['materials']))
