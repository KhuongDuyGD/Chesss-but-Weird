"""Final, repeatable HQ pass. Blender --background --factory-startup --python this_file.
Preserves approved sculpts, public IDs, the square grid and all twelve prisoner anchors.
Meshes, seamless PBR surfaces and previews are exported together; no image billboards.
"""
import bpy, bmesh, math, random, json, sys
import numpy as np
from pathlib import Path
from mathutils import Vector, noise
from math import sin, cos, pi
sys.path.insert(0, str(Path(__file__).parent))
import build_factions as F
K=F.K
OUT=K.OUT; DEST=F.DEST; TEX=OUT/'Textures/FactionsHQ'; TEX.mkdir(parents=True,exist_ok=True)
REV=DEST/'HighQuality'; REV.mkdir(exist_ok=True)
random.seed(671106)
SPECS={}; STATS=[]
ONLY=sys.argv[sys.argv.index('--faction')+1] if '--faction' in sys.argv else None
if ONLY and (REV/'geometry_report.json').exists():
    STATS=[s for s in json.loads((REV/'geometry_report.json').read_text())['assets'] if ONLY not in s['asset']]

def image_file(name, rgba, linear=False):
    h,w=rgba.shape[:2]; img=bpy.data.images.new(name,width=w,height=h,alpha=True)
    img.colorspace_settings.name='Non-Color' if linear else 'sRGB'
    img.pixels.foreach_set(np.ascontiguousarray(rgba,dtype=np.float32).ravel())
    img.filepath_raw=str(TEX/(name+'.png'));img.file_format='PNG';img.save();return img

def textures(family, base, rough, metal=0):
    """Periodic octave fields: same surface color, relief and roughness in both engines."""
    n=1024; y,x=np.mgrid[0:n,0:n].astype(np.float32)/n; rng=np.random.default_rng(sum(map(ord,family)))
    field=np.zeros_like(x)
    frequencies=np.fft.fftfreq(n).astype(np.float32)*n
    radial=frequencies[:,None]**2+frequencies[None,:]**2
    spectrum=np.fft.fft2(rng.normal(0,1,x.shape))
    for freq,amp in [(2,.32),(5,.22),(13,.14),(31,.07),(83,.035),(181,.02)]:
        octave=np.fft.ifft2(spectrum*np.exp(-radial/(2*freq*freq))).real.astype(np.float32)
        octave-=octave.mean();octave/=max(float(octave.std()),.0001)
        field+=octave*amp*.55
    vein=np.exp(-np.abs(np.sin(2*pi*(x*3+y*2)+field*5))*27)
    grain=rng.normal(0,.018,x.shape).astype(np.float32)
    isleaf=family=='Leaf'; isground=family in ['Meadow','Scorched']; ismetal=family=='Gold'
    height=field*.15+grain*.16
    variation=field*.23+grain
    if family in ['Ivory','Slate','Basalt','Limestone']:
        height-=vein*.035;variation-=vein*(.10 if family=='Ivory' else .16)
    if family=='Bark':
        lines=(np.sin(x*2*pi*23+field*9)*.5+.5)**6;height+=lines*.18;variation-=lines*.18
    if isground:
        height+=field*.6;variation+=field*.22
    if isleaf:
        vein=np.exp(-np.abs(x-.5)*95)+np.exp(-np.abs(np.sin((y*8+np.abs(x-.5)*6)*pi))*28)*.3
        height+=vein*.1;variation+=vein*.08
    rgba=np.ones((n,n,4),np.float32);rgba[:,:,:3]=np.clip(np.array(base)[None,None,:]*(1+variation[:,:,None]),.008,.98)
    albedo=image_file('HQ_'+family+'_Albedo',rgba)
    dx=(np.roll(height,-1,1)-np.roll(height,1,1))*7;dy=(np.roll(height,-1,0)-np.roll(height,1,0))*7
    norm=np.stack([-dx,-dy,np.ones_like(dx)],-1);norm/=np.linalg.norm(norm,axis=-1,keepdims=True)
    rgba[:,:,:3]=norm*.5+.5;normal=image_file('HQ_'+family+'_Normal',rgba,True)
    rgba[:,:,:3]=metal;rgba[:,:,3]=np.clip(1-rough+field*.075,.04,.65)
    packed=image_file('HQ_'+family+'_MetalSmooth',rgba,True)
    return albedo,normal,packed

FAMILIES={
 'Limestone':((.64,.60,.50),.79,0),'Ivory':((.77,.75,.66),.62,0),
 'Slate':((.12,.22,.28),.71,0),'Basalt':((.21,.19,.23),.78,0),
 'Ash':((.36,.34,.38),.76,0),'Meadow':((.20,.30,.095),.94,0),
 'Scorched':((.17,.14,.15),.97,0),'Bark':((.20,.12,.065),.92,0),
 'Leaf':((.24,.37,.085),.88,0),'Gold':((.69,.46,.14),.40,.82),
 'Iron':((.21,.19,.23),.55,.68),'Blue':((.035,.13,.29),.56,.05),
 'Violet':((.26,.075,.33),.65,.05),'Water':((.045,.24,.27),.24,.05),
 'Soul':((.16,.34,.07),.68,0),'Cinder':((.51,.12,.022),.82,0)}
T={k:textures(k,*v) for k,v in FAMILIES.items()}

def mat(key,family,tint=(1,1,1),emission=0):
    name='KR HQ '+key; m=bpy.data.materials.get(name) or bpy.data.materials.new(name);m.use_nodes=True
    p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    albedo,normal,packed=T[family];m.diffuse_color=tuple(np.array(FAMILIES[family][0])*np.array(tint))+(1,)
    p.inputs['Metallic'].default_value=FAMILIES[family][2];p.inputs['Roughness'].default_value=FAMILIES[family][1]
    for im in [albedo,normal]:
        node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=im
        if im==albedo:m.node_tree.links.new(node.outputs['Color'],p.inputs['Base Color'])
        else:
            n=m.node_tree.nodes.new('ShaderNodeNormalMap');n.inputs['Strength'].default_value=.48;m.node_tree.links.new(node.outputs['Color'],n.inputs['Color']);m.node_tree.links.new(n.outputs['Normal'],p.inputs['Normal'])
    glow=(.08,.38,.95) if key=='cyan' else (.20,.64,.06) if key=='Sickly soul' else (.85,.19,.025) if key=='Cinder' else FAMILIES[family][0]
    if emission:
        p.inputs['Emission Color'].default_value=(*glow,1);p.inputs['Emission Strength'].default_value=emission
    K.M[key]=m
    SPECS[name]={'name':name,'color':list(tint)+[1], 'metallic':FAMILIES[family][2],'roughness':FAMILIES[family][1],
        'albedo':Path(albedo.filepath_raw).relative_to(K.ROOT).as_posix(),
        'normal':Path(normal.filepath_raw).relative_to(K.ROOT).as_posix(),
        'metalSmooth':Path(packed.filepath_raw).relative_to(K.ROOT).as_posix(),
        'emission':list(glow)+[1],'emissionStrength':emission}
    return key

for key,fam in [('Royal limestone','Limestone'),('Ivory tile','Ivory'),('Blue slate','Slate'),('Azure enamel','Blue'),('Crown gold','Gold'),
 ('Obsidian','Basalt'),('Ash tile','Ash'),('Onyx tile','Basalt'),('Tyrant violet','Violet'),('Sickly soul','Soul'),
 ('Charred earth','Scorched'),('Ash rock','Basalt'),('Cinder','Cinder'),('Meadow','Meadow'),('New leaf','Leaf'),('Deep leaf','Leaf'),
 ('River','Water'),('Mountain blue','Slate'),('Mist blue','Limestone'),('rock','Limestone'),('ironedge','Iron'),('trunk','Bark'),('cyan','Blue'),('leather','Bark')]:
    mat(key,fam,emission=1.8 if key=='Sickly soul' else 2.2 if key=='Cinder' else 2 if key=='cyan' else 0)

# Smooth, fine bevels retain the authored flat tile tops and weighted face normals.
old_finish=K.finish
def finish(o,name,m,smooth=False,bevel=0):
    o=old_finish(o,name,m,smooth,bevel)
    for mod in o.modifiers:
        if mod.type=='BEVEL':mod.segments=5
    return o
K.finish=finish

# Data API construction avoids an O(objects²) dependency update for every flower/leaf.
_templates={}
def template(kind,seg=24,rings=12):
    key=(kind,seg,rings)
    if key not in _templates:
        bm=bmesh.new()
        if kind=='sphere':bmesh.ops.create_uvsphere(bm,u_segments=seg,v_segments=rings,radius=1)
        else:bmesh.ops.create_icosphere(bm,subdivisions=seg,radius=1)
        data=bpy.data.meshes.new('HQ unit '+str(key));bm.to_mesh(data);bm.free();_templates[key]=data
    return _templates[key]
def shaped(name,p,s,m,data,smooth=True):
    data=data.copy()
    for v in data.vertices:v.co=Vector((v.co.x*s[0],v.co.y*s[1],v.co.z*s[2]))
    o=bpy.data.objects.new(name,data);bpy.context.scene.collection.objects.link(o);o.location=p
    return K.finish(o,name,m,smooth)
def ell(name,p,s,m,seg=24,rings=12):return shaped(name,p,s,m,template('sphere',seg,rings))
def ico(name,p,s,m,sub=3):return shaped(name,p,s,m,template('ico',sub),True)
def box(name,p,s,m,bevel=.025):
    x,y,z=[v/2 for v in s];vs=[(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]
    o=K.mesh(name,vs,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],m,False,bevel);o.location=p;return o
def cone(name,p,r1,r2,h,m,n=48,bevel=.015):
    n=max(n,48);vs=[(cos(i*2*pi/n)*r,sin(i*2*pi/n)*r,z) for z,r in [(-h/2,r1),(h/2,r2)] for i in range(n)]
    fs=[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]+[tuple(reversed(range(n))),tuple(range(n,2*n))]
    o=K.mesh(name,vs,fs,m,True,bevel);o.location=p
    for poly in list(o.data.polygons)[-2:]:poly.use_smooth=False
    return o
K.ell=ell;K.ico=ico;K.box=box;K.cone=cone
def apply_meshes(obs):
    bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get()
    evaluated=[(o,bpy.data.meshes.new_from_object(o.evaluated_get(dg),preserve_all_data_layers=True,depsgraph=dg)) for o in obs if o.modifiers]
    for o,data in evaluated:o.modifiers.clear();o.data=data
K.apply_meshes=apply_meshes

def ring(name,p,r,thick,m,axis='Z'):
    bpy.ops.mesh.primitive_torus_add(major_segments=64,minor_segments=10,location=p,major_radius=r,minor_radius=thick)
    o=bpy.context.object
    if axis=='Y':o.rotation_euler.x=pi/2
    return K.finish(o,name,m,True)

def rock(p,size,evil=False):
    o=K.ico('Weathered sculpted stone',p,size,'Ash rock' if evil else 'rock',3)
    for v in o.data.vertices:
        f=noise.noise_vector(v.co*2.8+Vector((3,8,1)));v.co+=f*.18*max(size)
    for q in o.data.polygons:q.use_smooth=True
    return o

def leaf_cloud(x,y,z,r,seed):
    rng=random.Random(seed);vs=[];fs=[]
    # Individually curved, closed leaves catch light from either side without alpha cards.
    for i in range(430):
        a=rng.uniform(0,2*pi);zz=rng.uniform(-.83,.9);rr=(1-zz*zz)**.5
        shell=rng.uniform(.52,1)**.5
        c=Vector((x+cos(a)*rr*r*shell,y+sin(a)*rr*r*.86*shell,z+zz*r*.8))
        t=Vector((cos(a),sin(a),rng.uniform(-.35,.55))).normalized();w=t.cross(Vector((0,0,1))).normalized()
        n=t.cross(w);length=rng.uniform(.12,.24);width=length*.43;idx=len(vs)
        for u,v,h in [(-1,0,0),(-.3,-1,0),(.5,-.8,0),(1,0,0),(.5,.8,0),(-.3,1,0),(0,0,.28),(0,0,-.10)]:
            vs.append(c+t*(u*length)+w*(v*width)+n*(h*length))
        for j in range(6):fs.extend([(idx+j,idx+(j+1)%6,idx+6),(idx+(j+1)%6,idx+j,idx+7)])
    K.mesh('Sculpted layered broadleaf canopy',vs,fs,'New leaf',True)
    # A dense small inner canopy prevents hollow foliage silhouettes.
    o=K.ico('Canopy shaded inner growth',(x,y,z),(r*.63,r*.53,r*.50),'Deep leaf',3)
    for v in o.data.vertices:v.co+=noise.noise_vector(v.co*6)*.06
    for q in o.data.polygons:q.use_smooth=True

def tree(x,y,evil,scale,seed):
    z=F.height(x,y);rng=random.Random(seed)
    K.path('Organic bark trunk',[(x,y,z-.08),(x+.12*scale,y,z+scale),(x-.08*scale,y+.05,z+2.3*scale)],.16*scale,'trunk',[1.4,.8,.20])
    for j in range(5):
        a=j*2*pi/5+.3;dx=cos(a)*scale*.95;dy=sin(a)*scale*.75
        end=(x+dx,y+dy,z+scale*(1.6+(j%3)*.35))
        K.path('Tapered twisting branch',[(x+.08,y,z+scale*.6),(x+dx*.45,y+dy*.45,z+scale*1.3),end,(end[0]+dx*.2,end[1]+dy*.2,end[2]+scale*.6)],.085*scale,'trunk',[1,.8,.4,.035])
        if not evil and j in [0,2,4]:leaf_cloud(end[0],end[1],end[2]+scale*.45,scale*.78,seed+j)
    if not evil:leaf_cloud(x,y,z+2.6*scale,.90*scale,seed+61)
    for a in [0,1.7,3.4,5.1]:
        K.path('Tree root flare',[(x,y,z+.35),(x+cos(a)*.30*scale,y+sin(a)*.30*scale,z+.04),(x+cos(a)*.55*scale,y+sin(a)*.55*scale,z-.08)],.07*scale,'trunk',[1,.7,.01])

def tower(x,y,z,h,r,evil=False):
    stone='Obsidian' if evil else 'Royal limestone';trim='ironedge' if evil else 'Crown gold'
    K.cone('Dressed round foundation',(x,y,z+.15),r*1.3,r*1.2,.3,stone,64,.04)
    K.cone('Smooth carved tower',(x,y,z+h*.5),r,r*.94,h,stone,64,.03)
    for i in range(1,7):
        zz=z+h*i/7;ring('Masonry string course',(x,y,zz),r*(1-.06*i/7),.018,trim)
        for j in range(10):
            a=(j+(i%2)*.5)*2*pi/10;px=x+cos(a)*r*.998;py=y+sin(a)*r*.998
            K.path('Fine mortar joint',[(px,py,zz+.035),(px,py,zz+h/7-.035)],.007,trim)
    ring('Sculpted roof collar',(x,y,z+h),r*1.08,.045,trim)
    if evil:
        for j in range(8):
            a=j*pi/4;K.path('Curved crown talon',[(x+cos(a)*r,y+sin(a)*r,z+h-.1),(x+cos(a)*r*1.1,y+sin(a)*r*1.1,z+h+.35),(x+cos(a)*r*.80,y+sin(a)*r*.80,z+h+.8)],r*.12,stone,[1,.65,.01])
        ring('Soul brazier rim',(x,y,z+h+.1),r*.48,.04,trim)
    else:
        K.cone('Azure slate spire',(x,y,z+h+.55),r*1.25,.035,1.1,'Azure enamel',64,.015)
        for k in range(1,6):ring('Roof shingle course',(x,y,z+h+k*.17),r*1.25*(1-k*.17/1.1),.018,'Azure enamel')
        K.ell('Gilded finial',(x,y,z+h+1.2),(.06,.06,.12),trim,24,12)

def curlicue(p,side,evil=False):
    x,y,z=p;matr='Sickly soul' if evil else 'Crown gold'
    points=[]
    for j in range(45):
        t=j/44;theta=t*pi*3;rr=.11*(1-t)+.012
        points.append((x+cos(theta)*rr,y+side*(t*.45-.2),z+sin(theta)*rr*.25))
    K.path('Engraved vine scroll' if not evil else 'Etched binding rune',points,.009,matr)

def board(faction,evil):
    # Recreate only the board scaffold; substitute high resolution detail below.
    saved=F.finalize;F.finalize=lambda *a,**kw:None
    s=F.board(faction,evil);F.finalize=saved
    remove=['Corner octagon','Brazier','Magic • sculpted flame','Soul flame','Flame','Throne spike','Side carved block','Malevolent inlay','Azure crown jewel']
    for o in list(K.PARTS):
        if any(o.name.startswith(k) for k in remove):K.PARTS.remove(o);bpy.data.objects.remove(o,do_unlink=True)
    stone='Obsidian' if evil else 'Royal limestone';trim='ironedge' if evil else 'Crown gold'
    for side in [-1,1]:
        K.box('Inlaid continuous side gallery',(side*4.72,0,-.035),(.48,8.02,.20),stone,.045)
        for j in range(20):curlicue((side*4.72,-3.8+j*.4,.072),side,evil)
        for x in [-4.63,4.63]:
            for z,r,h in [(-.05,.39,.36),(.17,.30,.10),(.29,.24,.15)]:K.cone('Turned brazier pedestal',(x,side*5.33,z),r*1.10,r,h,stone,64,.025)
            for z,r in [(.05,.39),(.20,.28),(.36,.25)]:ring('Brazier chased metal band',(x,side*5.33,z),r,.025,trim)
            for j in range(4):
                a=j*pi/2;pts=[]
                for k in range(18):
                    t=k/17;ang=a+t*pi*1.45;rr=.13*(1-t)
                    pts.append((x+cos(ang)*rr,side*5.33+sin(ang)*rr,.38+t*.43))
                K.path('Curved living soul' if evil else 'Azure ritual flame',pts,.050,'Sickly soul' if evil else 'cyan',[1-k/17 for k in range(18)])
        for i in range(6):
            x=-3.15+i*1.26
            for dy in [-.39,.39]:K.box('Prison seat fine edge',(x,side*4.93+dy,.025),(.99,.014,.018),trim,.005)
            for dx in [-.5,.5]:K.ell('Recessed rivet',(x+dx,side*4.93,.033),(.018,.025,.012),trim,16,8)
        # Geometric gold/violet inlay on the fascia leaves every playing square unobstructed.
        for j in range(24):
            x=-3.95+j*.34;K.path('Repeating royal frieze' if not evil else 'Dominion frieze',[(x-.12,side*5.78,.07),(x,side*5.78,.19),(x+.12,side*5.78,.07)],.012,trim)
    return s

def arch(x,y,z,evil,angle=0):
    stone='Obsidian' if evil else 'Royal limestone'
    for side in [-1,1]:
        tower(x+side*1.85,y,z,2.0,.26,evil)
    for i in range(22):
        if evil and i in [5,6,16]:continue
        a=i*pi/21;px=x+cos(a)*1.85;pz=z+1.5+sin(a)*1.8
        o=K.box('Dressed arch voussoir',(px,y,pz),(.29,.52,.40),stone,.025);o.rotation_euler.y=a-pi/2
    if evil:
        for j in range(12):
            a=j*pi/11;K.ell('Portal-bound ember',(x+cos(a)*1.5,y-.28,z+1.5+sin(a)*1.45),(.025,.025,.07),'Sickly soul',16,8)

def environment(faction,evil):
    print('HQ_ENVIRONMENT_START',faction,flush=True)
    s=K.fresh('KR_'+faction+'_Environment');K.PARTS=[]
    n=161;vs=[];fs=[]
    for j in range(n):
        for i in range(n):
            x=-64+i*.8;y=-64+j*.8;edge=min(1,max(0,(math.hypot(x,y)-7.5)/6))
            d=noise.fractal(Vector((x*.25,y*.25,1)),1.0,2.0,4)*.20*edge
            vs.append((x,y,F.height(x,y)+d))
    for j in range(n-1):
        for i in range(n-1):
            a=j*n+i;cx=-64+(i+.5)*.8;cy=-64+(j+.5)*.8
            if evil and abs(cy)<49 and abs(abs(cx)-(12.4+1.5*sin(cy*.17)))<.82:continue
            fs.append((a,a+1,a+n+1,a+n))
    K.mesh('Detailed continuous sculpted terrain',vs,fs,'Charred earth' if evil else 'Meadow',True)
    # Interlocking stone terrace around the arena; fully clear of board and prisoner pads.
    for axis in [0,1]:
        for side in [-1,1]:
            for i in range(24):
                v=-8.5+i*.74
                x,y=(side*7.0,v) if axis==0 else (v,side*7.35)
                for row in range(2):
                    px=x+(row-.5)*.64 if axis==0 else x;py=y if axis==0 else y+(row-.5)*.64
                    K.box('Weathered courtyard flagstone',(px,py,F.height(px,py)+.07),(.61,.70,.12),'Ash tile' if evil else 'Royal limestone',.038)
    for side in [-1,1]:
        # Readable flowing stream / glowing depth fissure with real bank rocks.
        vv=[];banks=[]
        for i in range(165):
            y=-48+i*.6;x=side*(12.4+1.5*sin(y*.17));w=.60 if evil else .84
            if evil:
                # A deep, irregular rock trench with a narrow luminous bottom, never a green ribbon.
                inner=.15+.06*sin(y*4.7);outer=1.65+.13*sin(y*3.6)
                for d in [-1,1]:vv.append((x+d*inner,y,F.height(x,y)-.74))
                banks.extend([(x-outer,y,F.height(x-outer,y)+.035),(x-inner,y,F.height(x,y)-.70),
                              (x+inner,y,F.height(x,y)-.70),(x+outer,y,F.height(x+outer,y)+.035)])
            else:
                for d in [-1,1]:vv.append((x+d*w,y,F.height(x,y)+.07))
        K.mesh('Flowing soul rift' if evil else 'Clear rippled stream',vv,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(164)],'Sickly soul' if evil else 'River',True)
        if evil:
            faces=[]
            for i in range(164):
                a=i*4;faces.extend([(a,a+1,a+5,a+4),(a+2,a+3,a+7,a+6)])
            K.mesh('Weathered fissure cliff walls',banks,faces,'Obsidian',True)
        for i in range(32):
            y=-25+i*1.6;x=side*(12.4+1.5*sin(y*.17))
            for d in [-1,1]:rock((x+d*1.0,y,F.height(x,y)+.1),(.24,.38,.18),evil)
        for y in [-15,0,15]:
            x=side*(12.4+1.5*sin(y*.17));z=F.height(x,y)
            if not evil:
                for j in range(13):
                    px=x-1.8+j*.3;zz=z+.4+.25*cos((j-6)*pi/12)
                    K.box('Arched bridge paving',(px,y,zz),(.295,1.2,.23),'Royal limestone',.02)
                for d in [-1,1]:K.path('Bridge curved coping',[(x-1.8,y+d*.64,z+.7),(x,y+d*.64,z+1),(x+1.8,y+d*.64,z+.7)],.10,'Royal limestone')
        for y in [-8,8]:
            x=side*9.5;z=F.height(x,y);arch(x,y,z,evil)
    # Near-field gardens / burned ruins: deliberately framed at actual gameplay distance.
    for x,y in [(-9,-3),(9,3),(-9,3),(9,-3),(-5,-10),(5,10)]:
        z=F.height(x,y)
        if not evil:
            K.cone('Garden stone planter',(x,y,z+.12),.85,.89,.26,'Royal limestone',64,.035)
            K.cone('Planter earth',(x,y,z+.26),.81,.81,.03,'leather',64,.005)
            for j in range(12):
                a=j*2*pi/12;xx=x+cos(a)*.58;yy=y+sin(a)*.58
                K.ell('Rounded clipped boxwood',(xx,yy,z+.48),(.34,.30,.30),'Deep leaf',32,16)
                for k in range(3):
                    aa=j+k*2;px=xx+cos(aa)*.15;py=yy+sin(aa)*.15
                    K.path('Flower stem',[(px,py,z+.35),(px+.02,py,z+.68)],.012,'New leaf')
                    for petal in range(5):
                        a2=petal*2*pi/5;K.ell('Ivory garden petal',(px+cos(a2)*.045,py+sin(a2)*.045,z+.69),(.045,.025,.015),'Ivory tile',16,8)
        else:
            for j in range(5):
                rock((x+(j-2)*.38,y+sin(j)*.4,z+.15),(.35,.28,.24),True)
            K.box('Broken sanctuary wall',(x,y+.4,z+.55),(1.7,.33,1.1),'Obsidian',.05)
            for j in range(4):
                K.path('Molten carved fissure',[(x-.7+j*.4,y,z+.13),(x-.65+j*.4,y+.3,z+.16),(x-.5+j*.4,y+.5,z+.18)],.016,'Cinder')
    positions=[(-10,-6),(10,6),(-10,6),(10,-6),(-5,-12),(5,12),(-18,7),(18,-7),(-20,-10),(20,10),(-8,20),(8,-20),(-23,20),(23,-20),(-22,-22),(22,22)]
    for i,(x,y) in enumerate(positions):tree(x,y,evil,.85+(i%4)*.11,876+i)
    for i in range(40):
        a=i*2*pi/40;r=32+random.uniform(0,10);x=cos(a)*r;y=sin(a)*r
        if i%2==0:tree(x,y,evil,1.2,1800+i)
        rock((x,y,F.height(x,y)+.6),(1.5,1.3,1.1),evil)
    for x,y in [(-25,24),(25,-24)]:
        z=F.height(x,y);stone='Obsidian' if evil else 'Royal limestone'
        K.box('Citadel terrace',(x,y,z+.2),(7.5,5.8,.55),stone,.14)
        K.box('Citadel dressed keep',(x,y+1,z+2),(3.6,2.8,3.8),stone,.07)
        for d in [-1,1]:tower(x+d*2.7,y,z,4,.67,evil)
        arch(x,y-.8,z,evil)
        for j in range(10):
            zz=z+.4+j*.33;K.box('Keep masonry band',(x,y-.43,zz),(3.6,.06,.026),stone,.01)
        if not evil:K.cone('High royal slate roof',(x,y+1,z+4.7),2.5,0,1.8,'Azure enamel',4,.045).rotation_euler.z=pi/4
    for i in range(28):
        a=i*2*pi/28;r=48+random.uniform(-4,5);h=random.uniform(4,8);x=cos(a)*r;y=sin(a)*r
        rock((x,y,F.height(x,y)+h*.25),(random.uniform(4,6),random.uniform(4,6),h*.6),evil)
    # Fine meadow/ash detail is authored mesh, not sparse flat triangles or image cutouts.
    for i in range(170):
        a=random.uniform(0,2*pi);r=random.uniform(8.3,25);x=cos(a)*r;y=sin(a)*r
        if abs(abs(x)-(12.4+1.5*sin(y*.17)))<1.2:continue
        z=F.height(x,y)
        if evil:
            if i%4==0:rock((x,y,z+.09),(.17,.12,.12),True)
        else:
            for j in range(4):
                b=j*pi*.5;K.path('Curved fine grass',[(x,y,z),(x+cos(b)*.04,y+sin(b)*.04,z+.13),(x+cos(b)*.10,y+sin(b)*.10,z+.19)],.011,'New leaf',[1,.7,.02])
    return s

def render(s,name,context=False):
    for o in list(s.objects):
        if o.type in ['LIGHT','CAMERA']:bpy.data.objects.remove(o,do_unlink=True)
    K.render_settings(s,1600,1200);s.cycles.samples=64
    bg=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Strength'].default_value=.32
    evil='Veznan' in name
    K.light(s,'Large soft sun',(-10,-12,23),2900 if not evil else 2100,(1,.89,.70) if not evil else (.75,.80,1),7)
    K.light(s,'Broad sky bounce',(12,8,18),1700,(.66,.79,1) if not evil else (.58,.43,.75),16)
    K.camera(s,(14,-20,23) if context else (11,-16,19),(0,0,0),25 if context else 17)
    s.render.filepath=str(REV/(name+'.png'));bpy.ops.render.render(write_still=True)

def finalize(s,name):
    obs=list(K.PARTS);print('HQ_MESHES_PREPARE',name,len(obs),flush=True);K.apply_meshes(obs);K.uv(obs)
    for o in obs:
        if 'Square cell' in o.name:
            shift=random.uniform(0,4)
            for uv in o.data.uv_layers.active.data:uv.uv.x+=shift;uv.uv.y+=shift*.3
    K.root_objects(name,obs);K.export_fbx(name,obs);K.save_blend(DEST/(name+'.blend'))
    stat={'asset':name,'meshParts':len(obs),'vertices':sum(len(o.data.vertices) for o in obs),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in obs)}
    STATS.append(stat);print('HQ_ASSET_DONE',json.dumps(stat),flush=True)
    return obs

for faction,evil in [('Denas',False),('Veznan',True)]:
    if ONLY and faction!=ONLY:continue
    random.seed(671106+(1 if evil else 0))
    s=board(faction,evil);bo=finalize(s,'KR_'+faction+'_Board');render(s,faction+'_Board')
    (OUT/'Previews'/('KR_'+faction+'_Board.png')).write_bytes((REV/(faction+'_Board.png')).read_bytes())
    s=environment(faction,evil);finalize(s,'KR_'+faction+'_Environment')
    # Save environment-only scene above; context copies are review-only, never exported.
    for o in bo:
        c=o.copy();c.data=o.data;c.parent=None;c.matrix_world=o.matrix_world;s.collection.objects.link(c)
    render(s,faction+'_Context',True)
    (DEST/(faction+'_Context.png')).write_bytes((REV/(faction+'_Context.png')).read_bytes())
    # Environment shop art shows the same detailed setting at a wider angle.
    K.camera(s,(23,-31,29),(0,2,0),48,'Landscape preview');s.render.filepath=str(OUT/'Previews'/('KR_'+faction+'_Environment.png'));bpy.ops.render.render(write_still=True)

(OUT/'faction_material_palette.json').write_text(json.dumps({'materials':list(SPECS.values()),'revision':'HQ_20261106'},indent=2))
(REV/'geometry_report.json').write_text(json.dumps({'assets':STATS,'surfaceTextures':len(T)*3,'prisonContractUnchanged':True},indent=2))
print('FACTIONS_HQ_DONE',flush=True)
