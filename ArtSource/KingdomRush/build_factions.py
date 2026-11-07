"""Faction arenas: real Blender geometry, square 8x8 grids and two rank-end prisoner rails.
Never rebuilds or modifies the approved twelve character sculpts or archived arena.
"""
import bpy, math, random, json, sys
from pathlib import Path
from mathutils import Vector
from math import sin, cos, pi
sys.path.insert(0,str(Path(__file__).parent))
import build_kingdom_rush as K
from build_arena import text_obj
ROOT,SRC,OUT=K.ROOT,K.SRC,K.OUT
DEST=SRC/'Factions';DEST.mkdir(exist_ok=True)
random.seed(61106)
REPORT={'coordinate_system':'Blender Z up; Unity Y up; one cell = one metre', 'factions':[]}

def material(key,color,metal=0,rough=.65,glow=0):
    K.M[key]=K.mat('KR Factions • '+key,color,metal,rough,glow)
    return key
for args in [('Royal limestone',(.69,.64,.49)),('Ivory tile',(.83,.78,.63)),('Blue slate',(.12,.23,.31)),
             ('Azure enamel',(.025,.17,.49),.18,.32),('Crown gold',(.91,.64,.13),.72,.29),
             ('Obsidian',(.07,.055,.09),.2,.48),('Ash tile',(.40,.36,.40)),('Onyx tile',(.10,.075,.13),.2,.48),
             ('Tyrant violet',(.25,.025,.32),.25,.35),('Sickly soul',(.28,.88,.045),.12,.3,1.7),
             ('Charred earth',(.12,.105,.12)),('Ash rock',(.22,.18,.23)),('Cinder',(.95,.16,.02),.1,.4,1.5),
             ('Meadow',(.25,.42,.12)),('New leaf',(.38,.56,.16)),('Deep leaf',(.08,.25,.12)),
             ('River',(.055,.38,.50),.35,.2),('Mountain blue',(.19,.30,.37)),('Mist blue',(.34,.44,.49))]:
    material(*args)

def pillar(x,y,z,h,r,stone,evil=False):
    K.cone('Carved plinth',(x,y,z+.12),r*1.28,r*1.16,.24,stone,8,.035)
    K.cone('Faceted tower',(x,y,z+h*.5),r,r*.88,h,stone,12,.03)
    K.cone('Collar',(x,y,z+h-.12),r*1.04,r*1.04,.18,'Crown gold' if not evil else 'ironedge',12,.015)
    for i in range(6):
        a=i*pi/3
        K.box('Battlement',(x+cos(a)*r*.85,y+sin(a)*r*.85,z+h+.1),(r*.48,r*.48,.38),stone,.035)
    if evil:
        K.cone('Cruel spike',(x,y,z+h+.65),r*.6,0,1.15,'Obsidian',7,.01)
        K.ell('Captured soul',(x,y,z+h+.2),(r*.4,r*.4,.38),'Sickly soul',12,8)
    else:
        K.cone('Cerulean roof',(x,y,z+h+.52),r*1.25,0,1.05,'Azure enamel',8,.015)
        K.link('Gilded spire',(x,y,z+h+.95),(x,y,z+h+1.45),.023,'Crown gold')
        K.profile('Royal pennant',[(x,z+h+1.45),(x+.65,z+h+1.28),(x,z+h+1.1)],y,.035,'Azure enamel',.008)

def setup_render(s,pos,target,scale,path,w=1200,h=1000):
    K.render_settings(s,w,h,False);s.cycles.samples=24
    s.world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.65
    K.camera(s,pos,target,scale)
    K.light(s,'Warm key',(-12,-16,24),3400,(1,.88,.67),14,target)
    K.light(s,'Cool fill',(15,4,22),2600,(.68,.80,1),14,target)
    K.light(s,'Edge light',(0,20,15),2300,(.86,.75,1),12,target)
    s.render.filepath=str(path)

def finalize(name,s,preview,env=False):
    obs=list(K.PARTS);K.apply_meshes(obs);K.uv(obs)
    K.root_objects(name,obs);K.export_fbx(name,obs)
    bpy.context.preferences.filepaths.save_version=0
    # All material inputs are independent of projected source photographs.
    for mat in {m for o in obs for m in o.data.materials if m}:
        if not mat.use_nodes:continue
        p=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
        if not p:continue
        if any(t in mat.name.lower() for t in ['tile','stone','earth','limestone','rock']):
            noise=mat.node_tree.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=22
            bump=mat.node_tree.nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.18;bump.inputs['Distance'].default_value=.025
            mat.node_tree.links.new(noise.outputs['Fac'],bump.inputs['Height']);mat.node_tree.links.new(bump.outputs['Normal'],p.inputs['Normal'])
    K.save_blend(DEST/(name+'.blend'))
    s.render.filepath=str(preview);bpy.ops.render.render(write_still=True)
    print('FACTION_ASSET_DONE',name,'mesh_parts',len(obs),flush=True)
    return obs

def board(faction,evil):
    name='KR_'+faction+'_Board';s=K.fresh(name);K.PARTS=[]
    stone='Obsidian' if evil else 'Royal limestone';trim='ironedge' if evil else 'Crown gold';enamel='Tyrant violet' if evil else 'Azure enamel';magic='Sickly soul' if evil else 'cyan'
    K.box('Monolithic foundation',(0,0,-.53),(10.45,12.15,.42),stone,.16)
    K.box('Stepped cornice',(0,0,-.27),(10.20,11.95,.24),trim,.06)
    K.box('Carved platform',(0,0,-.15),(10.04,11.78,.19),stone,.06)
    K.box('Square grid recess',(0,0,-.14),(8.18,8.18,.24),stone,.025)
    for x in range(8):
        for y in range(8):
            # The two board-plane axes reverse together through FBX + board layout.
            light=(x+y)%2==1
            tile=('Ash tile' if light else 'Onyx tile') if evil else ('Ivory tile' if light else 'Blue slate')
            K.box('Square cell '+chr(97+x)+str(8-y),(x-3.5,y-3.5,-.065),(.982,.982,.13),tile,.009)
    for side in [-1,1]:
        K.box('Straight file border',(side*4.08,0,.015),(.055,8.22,.06),trim,.01)
        K.box('Straight rank border',(0,side*4.08,.015),(8.22,.055,.06),trim,.01)
        for i in range(8):
            text_obj('File notation',chr(72-i),(i-3.5,side*4.27,.027),.15,trim,(0,0,pi if side>0 else 0))
            text_obj('Rank notation',str(8-i),(side*4.27,i-3.5,.027),.15,trim,(0,0,side*pi/2))
        # 6 seats at each rank end; no side-file seats. Top surface exactly .036m.
        K.box('Rank-end prisoner rail',(0,side*4.93,-.08),(8.4,1.10,.14),stone,.06)
        K.box('Prisoner enamel',(0,side*4.93,-.01),(8.05,.94,.06),enamel,.045)
        for slot in range(6):
            x=-3.15+slot*1.26
            K.box('Prison rim '+str(side)+'_'+str(slot),(x,side*4.93,-.008),(1.08,.84,.056),trim,.035)
            K.box('Prison seat '+str(side)+'_'+str(slot),(x,side*4.93,.020),(1.01,.76,.032),'Onyx tile' if evil else 'Blue slate',.025)
        K.box('Outer heraldic fascia',(0,side*5.64,-.02),(8.4,.30,.32),enamel,.04)
        K.box('Fascia bright edge',(0,side*5.81,.12),(8.55,.07,.07),trim,.012)
        for x in [-4.63,4.63]:
            K.cone('Corner octagon',(x,side*5.33,-.03),.48,.42,.48,stone,8,.025)
            K.cone('Brazier',(x,side*5.33,.28),.15,.31,.22,trim,12,.015)
            K.flame((x,side*5.33,.46),.55,magic)
            if evil:
                for d in [-1,1]:K.cone('Throne spike',(x+d*.32,side*5.33,.38),.09,0,.85,stone,6,.008)
        for x in [-2.8,0,2.8]:
            if evil:
                K.gem('Soul seal',(x,side*5.82,-.015),(.20,.035,.19),magic)
            else:
                K.cone('Sunburst medallion',(x,side*5.64,.16),.18,.18,.04,trim,12,.006)
                K.gem('Azure crown jewel',(x,side*5.64,.21),(.14,.14,.10),magic)
        # Short side parapets retain a low, unobstructed view of all 64 squares.
        for y in [-3.55,-2.25,-.95,.95,2.25,3.55]:
            K.box('Side carved block',(side*4.7,y,-.02),(.48,1.04,.27),stone,.035)
            if evil:K.gem('Malevolent inlay',(side*4.7,y,.14),(.17,.24,.10),magic)
    if evil:
        for side in [-1,1]:
            for x in [-3.8,-1.3,1.4,3.9]:
                K.path('Soul seam outside grid',[(x,side*5.48,.02),(x+.2,side*5.61,.02),(x+.4,side*5.77,.02)],.017,magic)
    setup_render(s,(11,-17,21),(0,0,0),17.3,OUT/'Previews'/(name+'.png'))
    obs=finalize(name,s,OUT/'Previews'/(name+'.png'))
    REPORT['factions'].append({'id':'kingdom_rush_'+faction.lower(),'board':name,'cells':64,'grid_bounds':[-4,4,-4,4], 'prison_layout_yaw':-90,'prison_top_y':.036,'prison_rank_z':[-4.93,4.93], 'prison_files':[-3.15,-1.89,-.63,.63,1.89,3.15]})
    return s

def height(x,y):
    ramp=max(0,min(1,(math.hypot(x,y)-8)/15))
    return -1.10+ramp*(.65+.6*sin(x*.19)*cos(y*.21)+.30*sin(y*.36+x*.17))

def fortress(x,y,evil):
    z=height(x,y);stone='Obsidian' if evil else 'Royal limestone'
    K.box('Fortress bedrock',(x,y,z+.15),(6.5,5,.5),stone,.12)
    K.box('Keep',(x,y+1,z+2.45),(3.4,2.8,4.6),stone,.09)
    for side in [-1,1]:pillar(x+side*2.65,y,z,4.1,.72,stone,evil)
    K.box('Gate wall',(x,y,z+1.3),(4.5,.65,2.6),stone,.05)
    K.profile('Gate arch',[(x-.72,z+.3),(x+.72,z+.3),(x+.72,z+1.8),(x,z+2.4),(x-.72,z+1.8)],y-.34,.05,'Sickly soul' if evil else 'leather',.02)
    for dx in [-.85,0,.85]:K.box('High window',(x+dx,y-.43,z+3.5),(.22,.06,.8),'Sickly soul' if evil else 'Azure enamel',.025)
    if evil:
        for i in range(5):K.cone('Tyrant crown',(x+(i-2)*.65,y+1,z+5.35),.23,0,1.65,'Obsidian',6,.008)
    else:K.cone('Royal keep roof',(x,y+1,z+5.35),2.45,0,1.7,'Azure enamel',4,.025).rotation_euler.z=pi/4

def tree(x,y,evil,scale):
    z=height(x,y)
    if evil:
        K.link('Burnt trunk',(x,y,z),(x+.10,y,z+2*scale),.13*scale,'trunk',.06)
        for sign in [-1,1]:
            K.path('Dead branch',[(x,y,z+.7*scale),(x+sign*.7*scale,y+.13,z+1.4*scale),(x+sign*.8*scale,y+.1,z+2*scale)],.07*scale,'trunk',[1,.7,.1])
    else:
        K.link('Living trunk',(x,y,z),(x,y,z+1.5*scale),.13*scale,'trunk',.08)
        for dx,dy,dz,r in [(0,0,1.7,.9),(-.5,.1,1.6,.65),(.48,.1,1.8,.65),(0,.15,2.4,.55)]:
            K.ico('Layered leafy crown',(x+dx*scale,y+dy*scale,z+dz*scale),(r*scale,r*.86*scale,r*.75*scale),'New leaf' if dz>1.7 else 'Deep leaf',2)

def environment(faction,evil):
    name='KR_'+faction+'_Environment';s=K.fresh(name);K.PARTS=[]
    vs=[];fs=[];n=81
    for j in range(n):
        for i in range(n):
            x=-64+i*1.6;y=-64+j*1.6;vs.append((x,y,height(x,y)))
    for j in range(n-1):
        for i in range(n-1):a=j*n+i;fs.append((a,a+1,a+n+1,a+n))
    K.mesh('Continuous rolling terrain',vs,fs,'Charred earth' if evil else 'Meadow',True)
    # Rivers/fissures remain outside the arena's 8 metre clearance ring.
    for side in [-1,1]:
        vv=[]
        for i in range(121):
            y=-58+i;x=side*(15+2*sin(y*.12))
            for d in [-1,1]:vv.append((x+d*(.65 if evil else 1.0),y,height(x,y)+.035))
        K.mesh('Soul fissure' if evil else 'Turquoise river',vv,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(120)],'Sickly soul' if evil else 'River',True)
        if not evil:
            for y in [-19,0,19]:
                x=side*(15+2*sin(y*.12));z=height(x,y)+.38
                K.box('Stone bridge deck',(x,y,z),(3.9,1.4,.25),'Royal limestone',.07)
                for d in [-1,1]:K.box('Bridge parapet',(x,y+d*.64,z+.26),(3.8,.16,.48),'Royal limestone',.04)
    for i in range(32):
        a=2*pi*i/32;r=48+random.uniform(-4,4);x=cos(a)*r;y=sin(a)*r;h=random.uniform(5,10)
        K.ico('Horizon jagged peak',(x,y,height(x,y)+h*.35),(random.uniform(4,7),random.uniform(4,7),h*.7),'Ash rock' if evil else 'Mountain blue',1)
        if not evil and i%3==0:K.ico('Pale mountain summit',(x,y,height(x,y)+h*.85),(1.8,1.6,1.5),'Mist blue',1)
    for x,y in [(-24,22),(24,-22)]:fortress(x,y,evil)
    for i in range(160):
        x=random.uniform(-39,39);y=random.uniform(-39,39)
        if math.hypot(x,y)<11 or abs(abs(x)-(15+2*sin(y*.12)))<2.3 or any((x-a)**2+(y-b)**2<50 for a,b in [(-24,22),(24,-22)]):continue
        tree(x,y,evil,random.uniform(.7,1.4))
    # Four readable approach roads; all terrain below the playable and prisoner surfaces.
    for angle in [0,pi/2,pi,3*pi/2]:
        vv=[]
        for i in range(35):
            r=6.8+i*.7
            for d in [-1,1]:
                bend=sin((r-6.8)*.13)*1.2
                x=cos(angle)*r-sin(angle)*(d*.6+bend);y=sin(angle)*r+cos(angle)*(d*.6+bend)
                vv.append((x,y,height(x,y)+.045))
        K.mesh('Broken invasion road' if evil else 'King road',vv,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(34)],'Ash rock' if evil else 'Royal limestone')
    for i in range(90):
        a=random.uniform(0,2*pi);r=random.uniform(8.7,32);x=cos(a)*r;y=sin(a)*r;z=height(x,y)
        K.ico('Scattered blasted stone' if evil else 'Meadow boulder',(x,y,z+.17),(random.uniform(.18,.65),.3,.4),'Ash rock' if evil else 'rock',1)
        if evil and i%3==0:
            K.box('Ruined foundation',(x,y,z+.15),(1.4,1,.3),'Ash rock',.03)
            K.box('Shattered wall',(x+.45,y,z+.7),(.22,1,1.3),'Obsidian',.025)
            K.flame((x,y,z+.38),.6,'Cinder')
        elif not evil and i%3==0:
            for d in [-.14,.14]:K.ell('Golden wildflower',(x+d,y,z+.25),(.065,.065,.055),'goldlight',8,6)
    # Near-field vegetation and settlements give depth at the actual gameplay scale.
    for i in range(240):
        a=random.uniform(0,2*pi);r=random.uniform(9,24);x=cos(a)*r;y=sin(a)*r;z=height(x,y)
        if abs(abs(x)-(15+2*sin(y*.12)))<1.8:continue
        if evil:
            if i%3==0:
                K.ico('Scorched splinter',(x,y,z+.10),(.10,.08,.35),'Ash rock',1)
                K.ico('Last ember',(x+.04,y,z+.12),(.025,.035,.05),'Cinder',1)
        else:
            for j in range(3):
                dx=(j-1)*.09;h=random.uniform(.17,.32)
                K.mesh('Solid meadow blade',[(x+dx-.025,y,z),(x+dx+.025,y,z),(x+dx+.04,y+.025,z+h),(x+dx,y+.045,z)],[(0,1,2),(0,2,3),(1,3,2),(0,3,1)],'New leaf')
            if i%6==0:K.ell('Wildflower head',(x,y,z+.30),(.07,.07,.045),'goldlight',8,6)
    for index,(x,y) in enumerate([(-26,-10),(25,12),(-9,30),(10,-30)]):
        z=height(x,y)
        for j in range(3):
            px=x+(j-1)*2.3;py=y+(j%2)*2.0
            if evil:
                K.box('Ruined village footing',(px,py,z+.12),(1.7,1.6,.24),'Ash rock',.06)
                K.box('Broken village wall',(px-.70,py,z+.65),(.24,1.5,1.1),'Obsidian',.035)
                K.box('Collapsed lintel',(px+.2,py+.6,z+.38),(1.4,.28,.36),'Ash rock',.03).rotation_euler.y=.25
            else:
                K.box('Linirea cottage',(px,py,z+.7),(1.7,1.55,1.4),'Royal limestone',.06)
                K.cone('Cottage azure roof',(px,py,z+1.65),1.5,0,1.1,'Azure enamel',4,.02).rotation_euler.z=pi/4
                K.box('Cottage door',(px,py-.79,z+.4),(.38,.04,.75),'leather',.025)
                K.box('Warm cottage window',(px+.53,py-.80,z+.9),(.26,.04,.30),'Crown gold',.02)
    if evil:
        # Actual stone arch and free-standing floating shards, visible from every orbit.
        for side in [-1,1]:
            x=side*9;y=side*25;z=height(x,y)
            for i in range(10):
                a=pi*i/9;px=x+cos(a)*2.2;pz=z+.5+sin(a)*3.6
                o=K.box('Soul portal voussoir',(px,y,pz),(.65,.85,.85),'Obsidian',.06);o.rotation_euler.y=a-pi/2
                K.ico('Suspended soul shard',(px,y-.5,pz),(.13,.12,.28),'Sickly soul',1)
    setup_render(s,(22,-35,28),(0,2,0),63,OUT/'Previews'/(name+'.png'),1280,900)
    finalize(name,s,OUT/'Previews'/(name+'.png'),True)
    REPORT['factions'][-1]['environment']=name
    # A context render combines the true arena with its landscape, not a background image plane.
    with bpy.data.libraries.load(str(DEST/('KR_'+faction+'_Board.blend')),link=False) as (a,b):b.scenes=a.scenes
    arena=max((scene for scene in b.scenes if scene.name.startswith('KR_'+faction+'_Board')),key=lambda scene:sum(o.type=='MESH' for o in scene.objects))
    for o in arena.objects:
        if o.type=='MESH':
            c=o.copy();c.data=o.data;c.parent=None;c.matrix_world=o.matrix_world;s.collection.objects.link(c)
    K.camera(s,(13,-19,23),(0,0,.1),25,'Context camera')
    s.render.filepath=str(DEST/(faction+'_Context.png'));bpy.ops.render.render(write_still=True)

def write_palette():
    palette=[]
    for m in bpy.data.materials:
        if not m.use_nodes:continue
        p=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
        if p:palette.append({'name':m.name,'color':list(m.diffuse_color),'metallic':p.inputs['Metallic'].default_value,'roughness':p.inputs['Roughness'].default_value,'emission':list(p.inputs['Emission Color'].default_value),'emissionStrength':p.inputs['Emission Strength'].default_value})
    (OUT/'faction_material_palette.json').write_text(json.dumps({'materials':palette},indent=2))

if __name__=='__main__':
    for faction,evil in [('Denas',False),('Veznan',True)]:
        board(faction,evil);environment(faction,evil)
    write_palette()
    (DEST/'manifest.json').write_text(json.dumps(REPORT,indent=2))
    print('FACTIONS_BUILD_DONE',flush=True)
