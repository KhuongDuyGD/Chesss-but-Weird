"""Editable Kingdom Rush board, valley environment and complete 32-piece showcase."""
import sys,math,random,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent))
import build_kingdom_rush as K
import bpy
import numpy as np
from mathutils import Vector
from math import sin,cos,pi
random.seed(1709)
SRC,OUT,ROOT=K.SRC,K.OUT,K.ROOT

def text_obj(name,text,p,size,m,rotation=(0,0,0)):
    d=bpy.data.curves.new(name,'FONT');d.body=text;d.align_x='CENTER';d.align_y='CENTER';d.size=size;d.extrude=.001;d.bevel_depth=.0008
    o=bpy.data.objects.new(name,d);bpy.context.scene.collection.objects.link(o);o.location=p;o.rotation_euler=rotation;o.data.materials.append(K.M[m])
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH');K.PARTS.append(bpy.context.object)
    return bpy.context.object

def board():
    s=K.fresh('Kingdom Rush • Citadel board');K.PARTS=[]
    # 8 units wide, playable surface exactly Z=0, no embellishment inside the grid.
    K.box('Foundation • lower silhouette',(0,0,-.55),(11.45,11.50,.42),'stone3',.15)
    K.box('Foundation • limestone slab',(0,0,-.30),(11.20,11.24,.32),'stone',.10)
    K.box('Foundation • inset brass reveal',(0,0,-.12),(10.98,11.02,.055),'gold',.02)
    K.box('Board • recessed backing',(0,0,-.14),(8.30,8.30,.26),'stone3',.035)
    light1=K.mat('KR • Warm travertine tile',(.73,.58,.37),0,.54)
    light2=K.mat('KR • Travertine tile variation',(.70,.54,.34),0,.58)
    dark1=K.mat('KR • Smoky basalt tile',(.27,.22,.24),.12,.48)
    dark2=K.mat('KR • Basalt tile variation',(.24,.20,.22),.12,.51)
    for x in range(8):
        for y in range(8):
            # a1 is dark. Each beveled tile has an independent mesh for editability.
            m=[light1,light2][(x+y*3)%5==0] if (x+y)%2 else [dark1,dark2][(x+y*3)%5==0]
            K.box('Tile • '+chr(97+x)+str(y+1),(x-3.5,y-3.5,-.065),(.982,.982,.13),m,.012)
    for sign in [-1,1]:
        K.box('Grid • fine gold file border',(sign*4.08,0,-.008),(.052,8.20,.05),'gold',.012)
        K.box('Grid • fine gold rank border',(0,sign*4.08,-.008),(8.20,.052,.05),'gold',.012)
        for i in range(8):
            text_obj('Coordinates • file',chr(65+i),(i-3.5,sign*4.28,.027),.16,'goldlight',(0,0,0 if sign<0 else pi))
            text_obj('Coordinates • rank',str(i+1),(sign*4.28,i-3.5,.027),.16,'goldlight',(0,0,sign*pi/2))
        # Six prisoner pads per side: game positions, rather than decorative extra slots.
        K.box('Prison • side limestone rail',(sign*4.93,0,-.12),(1.04,8.39,.18),'stone2',.08)
        K.box('Prison • enamel channel',(sign*4.93,0,.009),(.86,7.80,.04),'navy' if sign>0 else 'plum',.06)
        for j in range(6):
            y=3.15-j*1.26
            K.box('Prison • gold seat rim',(sign*4.93,y,-.005),(.82,1.08,.056),'gold',.045)
            K.box('Prison • seat surface '+str(j),(sign*4.93,y,.020),(.75,1.01,.032),'darkstone' if sign<0 else 'stone3',.055)
            # Seat top = .036, matching Chessboard.GetPrisonSlotWorld.
        for y in [-4.07,4.07]:
            K.cone('Prison • end boss',(sign*4.93,y,.09),.42,.39,.16,'gold',12,.025)
            K.cone('Prison • crest field',(sign*4.93,y,.177),.32,.32,.035,'blue' if sign>0 else 'purple',12,.006)
            ico=K.ico('Prison • jewel',(sign*4.93,y,.23),(.12,.12,.055),'cyan' if sign>0 else 'pink',1)
    # Dressed masonry at the two ends, with corresponding heraldry.
    for side in [-1,1]:
        dark=side>0;m='darkstone' if dark else 'stone2'
        for i in range(11):
            K.box('Citadel • dressed coping',(i*.91-4.55,side*4.69,.05),(.88,1.05,.24),m,.055)
        K.box('Citadel • faction enamel frieze',(0,side*4.86,.19),(7.8,.43,.09),'purple' if dark else 'blue',.035)
        K.box('Citadel • gold fillet',(0,side*4.62,.25),(8.1,.045,.05),'gold',.012)
        for i in [-3,-1.65,1.65,3]:
            K.box('Citadel • crenel tooth',(i,side*5.15,.30),(.34,.40,.52),m,.06)
            K.gem('Citadel • merlon inlay',(i,side*5.37,.31),(.10,.025,.22),'pink' if dark else 'cyan')
        # Upright end plaque: front for Denas, rear rotated for VezNan.
        before=len(K.PARTS)
        K.badge(0,0,.48,.98,.78,dark)
        for o in K.PARTS[before:]:
            o.location.y+=side*5.18
        for x in [-2.9,2.9]:
            K.box('Citadel • banner gold border',(x,side*5.32,.16),(.57,.06,.72),'gold',.025)
            K.box('Citadel • banner enamel',(x,side*5.36,.18),(.46,.03,.59),'purple' if dark else 'blue',.012)
        # Corner octagonal bastions with flames kept clear of playable squares.
        for x in [-5.14,5.14]:
            y=side*4.9
            K.cone('Bastion • foot',(x,y,-.19),.67,.62,.36,m,8,.04)
            K.cone('Bastion • drum',(x,y,.10),.49,.46,.39,m,12,.035)
            K.cone('Bastion • brass belt',(x,y,.25),.49,.49,.06,'gold',12,.008)
            for j in range(6):
                a=2*pi*j/6
                K.box('Bastion • crenel',(x+.39*cos(a),y+.39*sin(a),.44),(.22,.22,.34),m,.045)
            K.cone('Brazier • stem',(x,y,.53),.12,.09,.35,'gold',16,.015)
            K.cone('Brazier • bowl',(x,y,.76),.15,.30,.22,'ironedge' if dark else 'gold',20,.025)
            K.flame((x,y,.97),.52,'pink' if dark else 'cyan')
            for j in range(4):
                a=2*pi*j/4
                K.path('Brazier • crown finger',[(x+.17*cos(a),y+.17*sin(a),.76),(x+.27*cos(a),y+.27*sin(a),.95),(x+.24*cos(a),y+.24*sin(a),1.08)],.025,'gold')
    # Cracks are restrained shallow dark veins at the perimeter, never over tile centers.
    for side in [-1,1]:
        for x in [-3.7,-1.1,2.4]:
            K.path('Citadel • stone fissure',[(x,side*4.48,.178),(x+.1,side*4.66,.178),(x-.04,side*4.85,.178)],.009,'stone3')
    # Rock underside, giving the board a substantial diorama silhouette.
    for i in range(24):
        a=2*pi*i/24;x=5.35*cos(a);y=5.38*sin(a)
        K.ico('Foundation • cliff fragments',(x,y,-.64),(.53,.45,.39),'rock',1)
    obs=list(K.PARTS);K.apply_meshes(obs);K.uv(obs);root=K.root_objects('KR_Citadel_Board',obs)
    root['playable_grid']='8 x 8, centered, 1m squares, surface Z=0';root['prison_surface']=.036;root['prison_layout_yaw']=0
    K.export_fbx('KR_Citadel_Board',[root]+obs)
    K.render_settings(s,1600,1600,True)
    K.camera(s,(13,-18,22),(0,0,.05),16.6)
    K.light(s,'Key • afternoon',(-7,-10,18),2100,(1,.86,.64),10)
    K.light(s,'Fill • sky',(9,3,15),1500,(.65,.81,1),12)
    K.light(s,'Rim • far side',(-3,12,14),2200,(1,.68,.42),9)
    s.render.filepath=str(OUT/'Previews/KR_Citadel_Board.png')
    K.save_blend(SRC/'KR_Citadel_Board.blend');bpy.ops.render.render(write_still=True)
    print('KR_BOARD_DONE',len(obs),flush=True)
    return s,root,obs

def terrain_height(x,y):
    r=math.sqrt((x/1.1)**2+y*y)
    ramp=max(0,min(1,(r-7)/12))
    height=-1.05+ramp*(.85+.50*sin(x*.26)+.48*cos(y*.25)+.26*sin(x*.39+y*.23))
    river_distance=abs(x-river_x(y))
    if river_distance<2.6:
        bank=-1.30+max(0,river_distance-1.35)*1.6
        height=min(height,bank)
    return height

def river_x(y):return -11.8+2.0*sin(y*.12)+1.4*sin(y*.21)

def tree(x,y,scale=1):
    z=terrain_height(x,y)
    K.cone('Valley • tree trunk',(x,y,z+scale*.65),.16*scale,.10*scale,1.3*scale,'trunk',10,.015)
    if random.random()<.45:
        for j in range(3):K.cone('Valley • pine tier',(x,y,z+scale*(1.1+j*.42)),scale*(.72-j*.12),0,scale*.99,'leaf' if j%2 else 'leaflight',9,.015)
    else:
        for dx,dy,dz,r in [(0,0,1.7,.83),(-.42,.04,1.45,.57),(.38,.08,1.6,.64),(0,.15,2.1,.61)]:
            K.ico('Valley • broadleaf canopy',(x+dx*scale,y+dy*scale,z+dz*scale),(r*scale,r*.83*scale,r*.81*scale),'leaflight' if dz>1.8 else 'leaf',2)

def castle(x,y,scale=1):
    z=terrain_height(x,y)
    for sx in [-1,1]:
        px=x+sx*scale*1.7
        K.cone('Linirea • watchtower',(px,y,z+1.9*scale),.63*scale,.57*scale,3.8*scale,'stone2',12,.05)
        K.cone('Linirea • red tiled roof',(px,y,z+4.4*scale),.95*scale,0,1.5*scale,'roof',12,.02)
        for zz in [1.1,2.2,3.3]:
            K.box('Linirea • tower window',(px,y-.58*scale,z+zz*scale),(.18*scale,.035,.35*scale),'navy',.025)
        K.link('Linirea • pennant mast',(px,y,z+5.05*scale),(px,y,z+5.75*scale),.025*scale,'gold')
        K.profile('Linirea • red pennant',[(px,z+5.75*scale),(px+.63*scale,z+5.62*scale),(px,z+5.40*scale)],y,.025,'roof',.005)
    K.box('Linirea • curtain wall',(x,y+.10,z+1.14*scale),(3.4*scale,.64*scale,2.28*scale),'stone',.06)
    for i in range(8):K.box('Linirea • wall crenel',(x+(i-3.5)*.43*scale,y+.1,z+2.46*scale),(.27*scale,.68*scale,.43*scale),'stone2',.035)
    K.profile('Linirea • gate',[(x-.50*scale,z),(x+.50*scale,z),(x+.50*scale,z+1.35*scale),(x,z+1.80*scale),(x-.50*scale,z+1.35*scale)],y-.25*scale,.045,'leather',.03)
    if scale<1:
        K.box('Linirea • royal keep',(x,y+1.4*scale,z+2.25*scale),(2.3*scale,2.0*scale,4.5*scale),'stone2',.08)
        K.cone('Linirea • keep roof',(x,y+1.4*scale,z+5.1*scale),1.8*scale,0,1.8*scale,'roof',4,.025).rotation_euler.z=pi/4
        for dx in [-.65,0,.65]:
            K.box('Linirea • keep window',(x+dx*scale,y+.385*scale,z+3.5*scale),(.22*scale,.035,.60*scale),'navy',.025)

def environment():
    s=K.fresh('Kingdom Rush • Valley of Linirea');K.PARTS=[]
    vs=[];fs=[];nx=65;ny=69
    for j in range(ny):
        y=-38+j*1.85
        for i in range(nx):
            x=-48+i*1.5;vs.append((x,y,terrain_height(x,y)))
    for j in range(ny-1):
        for i in range(nx-1):a=j*nx+i;fs.append((a,a+1,a+1+nx,a+nx))
    o=K.mesh('Valley • rolling meadow',vs,fs,'grass',True)
    # A baked, continuous color field avoids visible square material patches in the meadow.
    texdir=OUT/'Textures';texdir.mkdir(exist_ok=True)
    n=1024;xx,yy=np.meshgrid(np.linspace(-48,48,n),np.linspace(-38,88,n))
    variation=.5+.20*np.sin(xx*.23+np.sin(yy*.18))+.12*np.cos(yy*.32-xx*.16)+.07*np.sin(xx*1.6+yy*.9)
    variation=np.clip(variation,0,1)
    low=np.array([.072,.16,.015]);high=np.array([.16,.29,.036]);rgb=low+(high-low)*variation[:,:,None]
    pixels=np.concatenate([rgb,np.ones((n,n,1))],axis=2).astype(np.float32)
    img=bpy.data.images.new('KR_Meadow_Albedo',width=n,height=n);img.pixels.foreach_set(pixels.ravel());img.filepath_raw=str(texdir/'KR_Meadow_Albedo.png');img.file_format='PNG';img.save();img.pack()
    m=K.mat('KR • Baked meadow',(.4,.56,.18),0,.94);p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=img;m.node_tree.links.new(t.outputs['Color'],p.inputs['Base Color'])
    o.data.materials.clear();o.data.materials.append(m);uv=o.data.uv_layers.new(name='UVMap')
    for loop in o.data.loops:
        co=o.data.vertices[loop.vertex_index].co;uv.data[loop.index].uv=((co.x+48)/96,(co.y+38)/126)
    o['preserve_uv']=True
    # A winding river and narrow, broken foam edges, like the supplied landscape.
    v=[]
    for j in range(165):
        y=-39+j*.78;x=river_x(y);v.extend([(x-1.1,y,-.94),(x+1.1,y,-.94)])
    K.mesh('Valley • winding river',v,[(j*2,j*2+1,j*2+3,j*2+2) for j in range(164)],'water',True)
    for side in [-1,1]:
        for j in range(45):
            y=-29+j*1.63;x=river_x(y)+side*1.20
            K.ico('River • bank boulder',(x,y,-.88),(random.uniform(.3,.7),random.uniform(.3,.8),random.uniform(.25,.60)),'rock',1)
    for j in range(22):
        y=-28+j*3.27;x=river_x(y)+random.uniform(-.4,.2)
        K.path('River • glint',[(x-.18,y,-.925),(x+.02,y+.05,-.925),(x+.21,y+.02,-.925)],.009,'fur')
    # Distant cliff ring and tiered blue mountains; central sight line stays open.
    for i in range(21):
        a=.08+pi*.88*i/20
        x=cos(a)*43;y=sin(a)*15+43;z=terrain_height(x,y)
        h=random.uniform(5,9)
        K.ico('Horizon • mountain',(x,y,z+h*.34),(random.uniform(4,7),random.uniform(3,6),h*.65),'snow',1)
        if i%3==0:K.ico('Horizon • snow cap',(x,y,z+h*.86),(1.8,1.5,2.0),'fur',1)
    # Trees cluster around the perimeter rather than covering the playable board.
    for i in range(105):
        x=random.uniform(-29,29);y=random.uniform(-19,37)
        if (abs(x)<8 and -9<y<10) or abs(x-river_x(y))<2.0:continue
        if abs(x)<13 and y<16:continue
        if any((x-cx)**2+(y-cy)**2<16 for cx,cy in [(-20,7),(18,18),(7,30),(-4,37)]):continue
        tree(x,y,random.uniform(.7,1.55))
    for x,y,sc in [(-20,7,1.2),(18,18,1.0),(7,30,.85),(-4,37,.75)]:castle(x,y,sc)
    # A road winding from the board's far end toward the distant kingdom.
    vv=[]
    for j in range(50):
        y=6+j*.61;x=2.3*sin((y-6)*.16)
        for side in [-1,1]:
            px=x+side*.65;vv.append((px,y,terrain_height(px,y)+.085))
    K.mesh('Valley • winding king road',vv,[(j*2,j*2+1,j*2+3,j*2+2) for j in range(49)],'stone2')
    # Rocks, flowers and tufts around the arena make its connection to the meadow intentional.
    for i in range(140):
        a=random.uniform(0,2*pi);r=random.uniform(7.3,17);x=cos(a)*r;y=sin(a)*r
        if abs(x-river_x(y))<1.8:continue
        z=terrain_height(x,y)
        if i%4==0:K.ico('Meadow • rock',(x,y,z+.10),(.23,.19,.21),'rock',1)
        else:
            for j in range(3):
                dx=(j-1)*.065
                K.profile('Meadow • grass tuft',[(x+dx-.03,z),(x+dx+.03,z),(x+dx+.07,z+random.uniform(.16,.29))],y+dx,.016,'grasslight',0)
    for x,y,z in [(-25,62,13),(-7,65,15),(14,64,13),(32,65,16)]:
        for dx,dz,sc in [(-1,0,1),(0,.5,1.2),(1.2,0,.9)]:
            K.ell('Sky • stylized cloud',(x+dx*1.4,y,z+dz),(sc*2.0,sc*.70,sc*.65),'fur',16,10)
    obs=list(K.PARTS);K.apply_meshes(obs);K.uv(obs);root=K.root_objects('KR_Linirea_Valley',obs)
    K.export_fbx('KR_Linirea_Valley',[root]+obs)
    K.render_settings(s,2560,1440)
    world=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND');world.inputs['Color'].default_value=(.38,.61,.85,1);world.inputs['Strength'].default_value=.8
    s.view_settings.exposure=.7
    # The wide landscape camera includes the clear center and readable horizon.
    cam=K.camera(s,(18,-35,17),(0,12,2.5),51);cam.data.type='PERSP';cam.data.lens=29
    d=bpy.data.lights.new('Valley • sun','SUN');d.energy=3.2;d.angle=.14;o=bpy.data.objects.new('Valley • sun',d);s.collection.objects.link(o);o.rotation_euler=(.48,-.4,-.7)
    K.light(s,'Valley • sky fill',(-8,0,20),1500,(.67,.82,1),25,(0,6,0))
    s.render.filepath=str(OUT/'Previews/KR_Linirea_Background.png')
    K.save_blend(SRC/'KR_Linirea_Valley.blend');bpy.ops.render.render(write_still=True)
    print('KR_ENVIRONMENT_DONE',len(obs),flush=True)
    return s,root,obs

def load_piece(name):
    with bpy.data.libraries.load(str(SRC/'Pieces'/(name+'.blend')),link=False) as (a,b):
        b.objects=[n for n in a.objects if n==name]
    # A root loaded without its children does not recursively load all children.
    with bpy.data.libraries.load(str(SRC/'Pieces'/(name+'.blend')),link=False) as (a,b):
        b.scenes=a.scenes
    source=b.scenes[0]
    roots=[o for o in source.objects if o.type=='EMPTY' and o.name.startswith(name) and not o.name.startswith('REFERENCE')]
    root=roots[0]
    return source,root,[o for o in source.objects if o.type=='MESH']

def showcase(board_data,env_data):
    s=K.fresh('Kingdom Rush • Complete 32-piece showcase')
    # Linked mesh data, independent transforms: source piece files remain editable.
    for source,root,obs in [board_data,env_data]:
        for ob in obs:
            o=ob.copy();s.collection.objects.link(o);world=ob.matrix_world.copy();o.parent=None;o.matrix_world=world
    loaded={}
    for prefix,kind,fn in K.BUILDERS:
        for faction in ['Denas','Veznan']:
            name=prefix+'_'+faction
            source,root,obs=load_piece(name)
            loaded[(kind,faction)]=(root,obs)
    back=['Rook','Knight','Bishop','Queen','King','Bishop','Knight','Rook']
    for dark in [False,True]:
        faction='Veznan' if dark else 'Denas'
        for rank in [0,1]:
            for file in range(8):
                kind='Pawn' if rank else back[file];root,obs=loaded[(kind,faction)]
                parent=bpy.data.objects.new(faction+' '+kind+' '+chr(97+file)+str((8-rank) if dark else rank+1),None);s.collection.objects.link(parent)
                for ob in obs:
                    o=ob.copy();s.collection.objects.link(o);world=ob.matrix_world.copy();o.parent=parent;o.matrix_local=world
                parent.location=(file-3.5,(3.5-rank) if dark else (-3.5+rank),0)
                if dark:parent.rotation_euler.z=pi
    K.render_settings(s,2400,1700)
    s.view_settings.exposure=.4
    s.cycles.samples=64
    world=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND');world.inputs['Color'].default_value=(.26,.40,.57,1);world.inputs['Strength'].default_value=.5
    K.camera(s,(15,-21,23),(0,.3,.12),20.8)
    K.light(s,'Hero • warm key',(-7,-11,16),2500,(1,.84,.66),10)
    K.light(s,'Hero • sky fill',(10,-2,14),2000,(.62,.78,1),11)
    K.light(s,'Hero • sunset rim',(-3,12,16),3000,(1,.74,.47),9)
    d=bpy.data.lights.new('Hero • sun','SUN');d.energy=1.4;d.angle=.15;o=bpy.data.objects.new('Hero • sun',d);s.collection.objects.link(o);o.rotation_euler=(.5,-.4,-.7)
    s.render.filepath=str(SRC/'Renders/KingdomRush_Hero.png')
    K.save_blend(SRC/'KingdomRush_Complete.blend');bpy.ops.render.render(write_still=True)
    # Alternate directly overhead view proves grid alignment and both complete armies.
    K.camera(s,(0,-.01,26),(0,0,0),14.2,'Camera • grid verification')
    s.render.resolution_x=1800;s.render.resolution_y=1800;s.cycles.samples=32
    s.render.filepath=str(SRC/'Renders/KingdomRush_Top.png');bpy.ops.render.render(write_still=True)
    print('KR_SHOWCASE_DONE',flush=True)

if __name__=='__main__':
    b=board();e=environment();showcase(b,e)
    palette=[]
    for m in bpy.data.materials:
        if not m.name.startswith('KR •') or m.name[-4:-3]=='.':continue
        p=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None) if m.use_nodes else None
        if not p:continue
        palette.append({'name':m.name,'color':list(m.diffuse_color),'metallic':p.inputs['Metallic'].default_value,
            'roughness':p.inputs['Roughness'].default_value,'emission':list(p.inputs['Emission Color'].default_value),'emissionStrength':p.inputs['Emission Strength'].default_value})
    (OUT/'material_palette.json').write_text(json.dumps({'materials':palette},indent=2),encoding='utf-8')
