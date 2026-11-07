"""Kingdom Rush collaboration art, constructed as editable Blender geometry.
Blender --background --factory-startup --python ArtSource/KingdomRush/build_kingdom_rush.py
Uses only the supplied references. No downloaded or generated third-party models.
"""
import bpy, math, random, json, sys
from pathlib import Path
from mathutils import Vector
from math import sin, cos, pi

ROOT=Path(__file__).resolve().parents[2]
SRC=Path(__file__).parent
OUT=ROOT/'Assets/Skins/KingdomRushCollab'
REF=ROOT/'Assets/Audio/MusicPackage/KingdomRushCollab'
for p in [SRC/'Pieces', SRC/'Renders',OUT/'Models',OUT/'Previews']:
    p.mkdir(parents=True,exist_ok=True)
random.seed(2406)
MEAS=json.loads((SRC/'existing_dimensions.json').read_text())
REPORT={'authoring':'Editable original mesh construction in Blender from supplied reference images',
        'coordinates':'Blender Z up / front -Y; FBX Unity Y up, forward -Z; meters; ground pivot',
        'pieces':[], 'board':{'playable_grid':8,'cell_size':1,'surface_z':0,'prison_x':4.93,'prison_y':[3.15,1.89,.63,-.63,-1.89,-3.15],'prison_surface':.036}}

def fresh(name):
    s=bpy.data.scenes.new(name)
    if bpy.context.window:bpy.context.window.scene=s
    s.unit_settings.system='METRIC';s.unit_settings.scale_length=1
    return s

def mat(name,c,metal=0,rough=.48,emission=0):
    m=bpy.data.materials.get(name)
    if m:return m
    m=bpy.data.materials.new(name);m.diffuse_color=(*c,1);m.use_nodes=True
    p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    linear=tuple(v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in c)
    p.inputs['Base Color'].default_value=(*linear,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    if emission:
        p.inputs['Emission Color'].default_value=(*c,1);p.inputs['Emission Strength'].default_value=emission
    return m

M={
 'stone':mat('KR • Linirea limestone',(.34,.29,.23),0,.7),
 'stone2':mat('KR • Limestone highlights',(.47,.40,.31),0,.63),
 'stone3':mat('KR • Limestone shadows',(.23,.20,.18),0,.75),
 'darkstone':mat('KR • Dark Tower basalt',(.095,.064,.105),.12,.61),
 'darkstone2':mat('KR • Basalt cut edges',(.18,.12,.19),.1,.6),
 'darkstone3':mat('KR • Basalt shadows',(.055,.041,.065),.05,.7),
 'gold':mat('KR • Royal gold',(.98,.70,.055),.65,.30),
 'goldlight':mat('KR • Gold edge',(1,.83,.16),.6,.3),
 'silver':mat('KR • Hand forged silver',(.49,.55,.59),.7,.3),
 'silverlight':mat('KR • Silver blade edge',(.8,.84,.85),.75,.25),
 'iron':mat('KR • Blackened armor',(.095,.10,.13),.65,.36),
 'ironedge':mat('KR • Dark armor edges',(.27,.22,.31),.65,.31),
 'blue':mat('KR • Denas royal blue',(.022,.095,.44),.06,.46),
 'bluelight':mat('KR • Blue fabric highlight',(.04,.17,.65),.06,.48),
 'navy':mat('KR • Cape lining blue',(.012,.025,.10),0,.62),
 'purple':mat('KR • VezNan imperial violet',(.30,.018,.36),.06,.47),
 'purplelight':mat('KR • Violet silk edge',(.53,.025,.53),.08,.42),
 'plum':mat('KR • Cape lining purple',(.075,.008,.105),0,.65),
 'skin':mat('KR • Warm skin',(.76,.42,.18),0,.58),
 'skinlight':mat('KR • Skin highlight',(.95,.60,.30),0,.55),
 'orc':mat('KR • Orc skin',(.27,.48,.035),0,.57),
 'bone':mat('KR • Warm ivory bone',(.84,.75,.52),0,.5),
 'fur':mat('KR • Ermine fur',(.88,.82,.69),0,.8),
 'ivory':mat('KR • Ivory robes',(.78,.74,.62),0,.65),
 'hair':mat('KR • Denas golden hair',(.96,.66,.025),.04,.55),
 'darkhair':mat('KR • Raven hair',(.045,.021,.055),.04,.52),
 'eye':mat('KR • Eye and recess ink',(.009,.012,.014),0,.58),
 'green':mat('KR • Soul fire',(.25,.95,.008),.05,.3,.85),
 'greendark':mat('KR • Fel crystal body',(.055,.32,.005),.25,.3,.4),
 'cyan':mat('KR • Azure crystal',(.015,.40,.86),.32,.22,.35),
 'pink':mat('KR • Arcane violet',(.66,.018,.93),.12,.27,1.1),
 'leather':mat('KR • Worn leather',(.12,.061,.025),0,.7),
 'hoof':mat('KR • Iron horseshoe',(.075,.065,.059),.3,.5),
 'horse':mat('KR • Palfrey ivory',(.76,.68,.48),0,.7),
 'grass':mat('KR • Meadow grass',(.35,.52,.17),0,.95),
 'grasslight':mat('KR • Sunlit grass',(.45,.61,.20),0,.95),
 'leaf':mat('KR • Pine green',(.14,.38,.10),0,.9),
 'leaflight':mat('KR • Tree canopy',(.30,.53,.13),0,.92),
 'trunk':mat('KR • Timber',(.17,.085,.035),0,.83),
 'rock':mat('KR • Valley rock',(.28,.34,.31),0,.88),
 'water':mat('KR • River turquoise',(.025,.36,.48),.32,.2),
 'roof':mat('KR • Terracotta roofs',(.53,.11,.025),0,.7),
 'snow':mat('KR • Distant mountain mist',(.32,.48,.55),0,.94),
}
PARTS=[]
def finish(o,name,m,smooth=False,bevel=0):
    o.name=name
    if m:o.data.materials.append(M.get(m,m))
    if smooth:
        for p in o.data.polygons:p.use_smooth=True
    if bevel:
        mod=o.modifiers.new('Soft sculpted edges','BEVEL');mod.width=bevel;mod.segments=2
        mod=o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    PARTS.append(o);return o

def box(name,p,s,m,bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.scale=s
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(o,name,m,False,bevel)

def ell(name,p,s,m,seg=20,rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg,ring_count=rings,radius=1,location=p)
    o=bpy.context.object;o.scale=s;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(o,name,m,True)

def ico(name,p,s,m,sub=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub,radius=1,location=p);o=bpy.context.object;o.scale=s
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return finish(o,name,m,False)

def cone(name,p,r1,r2,h,m,n=32,bevel=.015):
    bpy.ops.mesh.primitive_cone_add(vertices=n,radius1=r1,radius2=r2,depth=h,location=p)
    return finish(bpy.context.object,name,m,False,bevel)

def link(name,a,b,r,m,r2=None):
    a,b=Vector(a),Vector(b);o=cone(name,(a+b)/2,r,r if r2 is None else r2,(b-a).length,m,16,.01)
    o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o

def path(name,pts,r,m,radii=None):
    # Tapered swept mesh with evenly sampled control points, all editable topology.
    vs=[];fs=[];pts=[Vector(p) for p in pts];n=10
    if len(pts)>2:
        original=pts;rr=radii or [1]*len(pts);pts=[];radii=[]
        for j in range(len(original)-1):
            p0=original[max(0,j-1)];p1=original[j];p2=original[j+1];p3=original[min(len(original)-1,j+2)]
            for k in range(3):
                t=k/3
                pts.append(.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t))
                radii.append(rr[j]*(1-t)+rr[j+1]*t)
        pts.append(original[-1]);radii.append(rr[-1])
    for j,p in enumerate(pts):
        tangent=(pts[min(j+1,len(pts)-1)]-pts[max(j-1,0)]).normalized()
        axis=Vector((0,1,0)) if abs(tangent.y)<.9 else Vector((1,0,0))
        u=tangent.cross(axis).normalized();v=tangent.cross(u).normalized()
        rad=r*(radii[j] if radii else 1)
        for i in range(n):vs.append(p+rad*(u*cos(2*pi*i/n)+v*sin(2*pi*i/n)))
    for j in range(len(pts)-1):
        for i in range(n):a=j*n+i;b=j*n+(i+1)%n;fs.append((a,b,b+n,a+n))
    fs.extend([tuple(reversed(range(n))),tuple((len(pts)-1)*n+i for i in range(n))])
    return mesh(name,vs,fs,m,True)

def mesh(name,vs,fs,m,smooth=False,bevel=0):
    d=bpy.data.meshes.new(name);d.from_pydata(vs,[],fs);d.update();o=bpy.data.objects.new(name,d);bpy.context.scene.collection.objects.link(o)
    return finish(o,name,m,smooth,bevel)

def profile(name,points,y,depth,m,bevel=.015):
    # X/Z silhouette extruded along Y. Front is negative Y.
    n=len(points);v=[(x,y-depth/2,z) for x,z in points]+[(x,y+depth/2,z) for x,z in points]
    f=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh(name,v,f,m,False,bevel)

def gem(name,p,s,m):
    x,y,z=p;w,d,h=s
    v=[(x,y-d,z),(x-w/2,y,z),(x,y,z-h/2),(x+w/2,y,z),(x,y,z+h/2),(x,y+d*.35,z)]
    return mesh(name,v,[(0,1,2),(0,2,3),(0,3,4),(0,4,1),(5,2,1),(5,3,2),(5,4,3),(5,1,4)],m)

def ring_sector(name,r1,r2,z1,z2,a,b,m,steps=6):
    vs=[]
    for z in [z1,z2]:
        for r in [r1,r2]:
            vs.extend([(r*cos(a+(b-a)*i/steps),r*sin(a+(b-a)*i/steps),z) for i in range(steps+1)])
    k=steps+1;fs=[]
    for i in range(steps):
        fs.extend([(i,i+1,k+i+1,k+i),(2*k+i,3*k+i,3*k+i+1,2*k+i+1),
                   (i,2*k+i,2*k+i+1,i+1),(k+i,k+i+1,3*k+i+1,3*k+i)])
    fs.extend([(0,k,3*k,2*k),(steps,2*k-1,4*k-1,3*k-1)])
    return mesh(name,vs,fs,m,False,.012)

def skull(x,y,z,s=.18,eyes='eye'):
    ell('Skull • forehead',(x,y,z+.015*s),(s*.73,s*.28,s*.7),'bone',16,10)
    box('Skull • jaw',(x,y-.01*s,z-.55*s),(s*.85,s*.38,s*.65),'bone',.025*s)
    for sign in [-1,1]:
        o=ell('Skull • socket',(x+sign*s*.31,y-s*.267,z+.015*s),(s*.25,s*.06,s*.29),eyes,12,8);o.rotation_euler.y=sign*-.3
    profile('Skull • nasal cavity',[(x,z-.13*s),(x-s*.13,z-.34*s),(x+s*.13,z-.34*s)],y-s*.3,s*.04,'eye',0)
    for i in [-1,0,1]:box('Skull • tooth cut',(x+i*s*.23,y-s*.208,z-.67*s),(s*.037,s*.04,s*.26),'eye',.001)

def lion(x,y,z,s=.15):
    # Raised heraldic lion: scalloped mane, brow, snout, jaw and swept crown.
    for i in range(9):
        a=2*pi*i/9;ico('Lion • mane',(x+cos(a)*s*.57,y,z+sin(a)*s*.65),(s*.38,s*.16,s*.4),'gold',1)
    ell('Lion • face',(x+s*.04,y-s*.10,z+s*.04),(s*.52,s*.15,s*.57),'goldlight',16,10)
    ell('Lion • muzzle',(x+s*.40,y-s*.17,z-s*.04),(s*.32,s*.13,s*.22),'gold',12,8)
    profile('Lion • swept brow',[(x-s*.4,z+s*.29),(x-s*.1,z+s*.5),(x+s*.57,z+s*.2),(x+s*.40,z+s*.13)],y-s*.24,s*.06,'goldlight',.005)
    profile('Lion • eye',[(x+s*.1,z+s*.23),(x+s*.34,z+s*.18),(x+s*.27,z+s*.09)],y-s*.274,s*.02,'eye',0)
    profile('Lion • jaw shadow',[(x+s*.4,z-.22*s),(x+s*.22,z-.24*s),(x+s*.36,z-.39*s),(x+s*.51,z-.30*s)],y-s*.24,s*.02,'stone3',0)

def badge(x,y,z,w=.46,h=.43,dark=False):
    pts=[(x-w/2,z+h/2),(x+w/2,z+h/2),(x+w/2,z-h*.25),(x,z-h*.6),(x-w/2,z-h*.25)]
    profile('Heraldry • gold escutcheon',pts,y,.065,'gold',.018)
    pts=[(x+(px-x)*.80,z+(pz-z)*.80) for px,pz in pts]
    profile('Heraldry • enamel field',pts,y-.043,.025,'purple' if dark else 'blue',.009)
    if dark:skull(x,y-.075,z,.145)
    else:lion(x,y-.078,z,.145)

def base(dark=False):
    start=len(PARTS)
    stone='darkstone' if dark else 'stone';light='darkstone2' if dark else 'stone2';shadow='darkstone3' if dark else 'stone3'
    cone('Plinth • underside',(0,0,.045),.81,.81,.09,shadow,48)
    for row,(r1,r2,z1,z2) in enumerate([(.62,.85,.045,.21),(.60,.77,.205,.37)]):
        for i in range(12):
            a=2*pi*(i+row*.5)/12
            ring_sector('Plinth • individual dressed block',r1,r2,z1,z2,a+.014,a+2*pi/12-.014,light if i%4==0 else stone,4)
    cone('Plinth • top rim',(0,0,.395),.79,.77,.10,light,48)
    cone('Plinth • inset top',(0,0,.444),.71,.71,.025,stone,48,.008)
    if dark:cone('Plinth • amethyst inlay',(0,0,.365),.78,.78,.035,'purplelight',48,.004)
    badge(0,-.80,.25,.47,.39,dark)
    for o in PARTS[start:]:o['plinth_component']=True

def boots(z=.57,dark=False,spread=.21):
    for s in [-1,1]:
        ell('Boot • toe',(s*spread,-.09,z),(.18,.23,.15),'iron' if dark else 'leather')
        cone('Boot • cuff',(s*spread,.015,z+.19),.125,.14,.31,'iron' if dark else 'silver',20)

def eyes(x,y,z,spacing=.12,glow=False,angry=True):
    for sign in [-1,1]:
        px=x+sign*spacing
        if glow:
            p=[(px-.065,z+.03),(px+.065,z+.03),(px+sign*.022,z-.055)]
            profile('Face • luminous eye',p,y,.02,'green',.003)
        else:
            ell('Face • eye',(px,y,z),(.037,.025,.071),'eye',12,8)
            ell('Face • catchlight',(px-.009,y-.024,z+.028),(.01,.007,.016),'fur',8,6)
        if angry:
            a=(px-sign*.08,y-.012,z+.11);b=(px+sign*.075,y-.019,z+.05)
            path('Face • determined brow',[a,b],.035,'iron' if glow else 'hair')

def face(z=2.16,dark=False,orc=False,queen=False):
    if dark and not queen:
        ell('Face • hood shadow',(0,-.075,z),(.265,.225,.30),'eye')
        eyes(0,-.291,z+.02,.115,True,False)
        return
    c='orc' if orc else 'skinlight'
    ell('Face • head',(0,-.065,z),(.28,.24,.31),c,24,16)
    for s in [-1,1]:ell('Face • ear',(s*.279,-.03,z-.025),(.065,.065,.10),c,16,10)
    ell('Face • nose',(0,-.293,z-.05),(.063,.063,.07),c,16,10)
    eyes(0,-.291,z+.04,.118,False,True)
    path('Face • mouth',[(-.07,-.287,z-.17),(0,-.31,z-.18),(.07,-.287,z-.17)],.012,'leather')
    if orc:
        for s in [-1,1]:cone('Orc • tusk',(s*.17,-.285,z-.12),.035,0,.13,'bone',12,.004)

def cape(z1=.64,z2=1.95,dark=False,width=.65,flow=.18):
    vs=[];fs=[];cols=16;rows=9
    for j in range(rows):
        t=j/(rows-1);w=width*(1-.44*t)
        for i in range(cols):
            u=i/(cols-1);a=pi*.08+u*pi*.84
            x=cos(a)*w*1.22;y=sin(a)*(width*.65+flow*(1-t))+.09
            y+=.045*sin(u*pi*8)*(1-t*.5)
            z=z1+(z2-z1)*t+.045*sin(u*pi*5)*(1-t)**3
            vs.append((x,y,z))
    for j in range(rows-1):
        for i in range(cols-1):a=j*cols+i;fs.append((a,a+1,a+1+cols,a+cols))
    o=mesh('Cape • sculpted fabric folds',vs,fs,'purple' if dark else 'blue',True)
    mod=o.modifiers.new('Cloth thickness','SOLIDIFY');mod.thickness=.034
    for i in [0,cols-1]:path('Cape • gold embroidered edge',[vs[j*cols+i] for j in range(rows)],.027,'gold')
    path('Cape • embroidered hem',vs[:cols],.026,'gold')
    return o

def armor(z=1.36,dark=False):
    ell('Armor • breastplate',(0,.015,z),(.38,.245,.43),'iron' if dark else 'silver',24,16)
    cone('Armor • skirt',(0,.015,z-.37),.38,.27,.32,'ironedge' if dark else 'silver',12,.025)
    box('Armor • belt',(0,-.01,z-.25),(.66,.46,.095),'leather',.018)
    box('Armor • buckle',(0,-.259,z-.25),(.15,.045,.12),'gold',.015)
    for s in [-1,1]:
        ell('Armor • shoulder',(s*.365,0,z+.23),(.22,.24,.16),'ironedge' if dark else 'silverlight')
        if dark:
            for dx in [-.08,.08]:cone('Armor • shoulder spike',(s*.39+dx,0,z+.4),.06,0,.19,'bone',8,.003)

def hand(p,dark=False,open=False):
    col='orc' if dark else 'skinlight';x,y,z=p
    ell('Hand • palm',p,(.11,.085,.12),col,16,10)
    for i in range(3):
        px=x+(i-1)*.055
        if open:path('Hand • finger',[(px,y-.01,z+.045),(px+(i-1)*.022,y-.02,z+.19)],.026,col)
        else:ell('Hand • knuckle',(px,y-.06,z+.02),(.031,.032,.074),col,12,8)
    ell('Hand • thumb',(x-.09,y-.04,z+.015),(.05,.047,.07),col,12,8)

def helmet(z,dark=False):
    col='ironedge' if dark else 'silver'
    ell('Helmet • dome',(0,0,z+.12),(.33,.265,.32),col,24,16)
    # open facial aperture, side cheek plates and swept brow, rather than a blank sphere.
    profile('Helmet • brow',[(-.32,z+.14),(-.21,z+.22),(0,z+.18),(.21,z+.22),(.32,z+.14),(.27,z+.08),(0,z+.10),(-.27,z+.08)],-.258,.07,col,.018)
    for s in [-1,1]:
        profile('Helmet • cheek plate',[(s*.23,z+.15),(s*.33,z+.15),(s*.31,z-.27),(s*.21,z-.30),(s*.21,z-.07)],-.10,.29,col,.022)
        ell('Helmet • hinge',(s*.319,-.20,z+.04),(.035,.024,.04),'gold' if not dark else 'bone',12,8)
    cone('Helmet • plume socket',(0,.015,z+.414),.082,.066,.13,'gold' if not dark else 'ironedge',16)
    for k in range(5):
        path('Helmet • swept plume',[(0,.015,z+.46),(0,.06+k*.045,z+.63),(0,.22+k*.045,z+.66-k*.026),(0,.40+k*.025,z+.53-k*.045)],.09,'purplelight' if dark else 'blue',[.9,1,1,.06])

def shield(x,y,z,dark=False):
    ell('Shield • metal rim',(x,y,z),(.35,.075,.42),'ironedge' if dark else 'gold',28,16)
    ell('Shield • enamel face',(x,y-.057,z),(.293,.045,.35),'purple' if dark else 'blue',28,16)
    for a in [0,.9,2.2,pi,4.1,5.4]:ell('Shield • rivet',(x+.32*cos(a),y-.066,z+.38*sin(a)),(.022,.018,.022),'bone' if dark else 'goldlight',10,8)
    if dark:skull(x,y-.105,z,.24,'green')
    else:lion(x,y-.113,z,.23)

def sword(x,y,z,length=.68,dark=False,down=False):
    # A true diamond-section blade with an explicit central ridge.
    direction=-1 if down else 1;w=.105
    v=[(x-w,y,z),(x,y-.055,z),(x+w,y,z),(x,y+.035,z),
       (x-w*.78,y,z+direction*(length-.17)),(x,y-.045,z+direction*(length-.17)),(x+w*.78,y,z+direction*(length-.17)),(x,y+.03,z+direction*(length-.17)),(x,y,z+direction*length)]
    mesh('Sword • forged diamond blade',v,[(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0),(4,8,5),(5,8,6),(6,8,7),(7,8,4),(0,1,2,3)],'greendark' if dark else 'silverlight')
    link('Sword • crossguard',(x-.24,y,z),(x+.24,y,z),.05,'gold' if not dark else 'ironedge')
    link('Sword • leather grip',(x,y,z-direction*.05),(x,y,z-direction*.28),.047,'leather')
    ico('Sword • pommel',(x,y,z-direction*.30),(.07,.06,.07),'gold' if not dark else 'purplelight',1)
    if dark:
        for s in [-1,1]:path('Sword • fel edge',[(x+s*w,y-.008,z+.02),(x+s*w*.78,y-.008,z+length-.17),(x,y-.008,z+length)],.012,'green',[.5,1,.05])

def crown(z,dark=False,r=.32):
    cone('Crown • circlet',(0,-.018,z),r,r,.13,'gold',32,.012)
    for i in range(7):
        a=2*pi*i/7-pi/2;x,y=r*cos(a),r*sin(a)-.018
        o=profile('Crown • pointed fleur',[(-.11,z-.01),(.11,z-.01),(.11,z+.17),(0,z+.30 if i==0 else z+.22),(-.11,z+.17)],0,.08,'gold',.022)
        o.rotation_euler.z=a+pi/2;o.location=(x,y,0)
    gem('Crown • central jewel',(0,-r-.098,z+.16),(.14,.035,.19),'pink' if dark else 'cyan')

def staff(x,y,z=1.95,dark=False,queen=False):
    link('Staff • carved shaft',(x,y,.69),(x,y,z),.041,'leather')
    for zz in [.74,z-.12]:cone('Staff • gold ferrule',(x,y,zz),.067,.067,.11,'gold',16)
    if dark:
        skull(x,y-.025,z+.12,.17,'green')
        for s in [-1,1]:path('Staff • crown talon',[(x+s*.1,y,z-.03),(x+s*.22,y,z+.23),(x+s*.16,y,z+.43)],.045,'gold',[1,1,.02])
        flame((x,y,z+.34),.37,'green')
    else:
        gem('Staff • azure crystal',(x,y,z+.20),(.27,.12,.39),'cyan')
        for s in [-1,1]:path('Staff • gold prong',[(x+s*.08,y,z-.04),(x+s*.24,y,z+.12),(x+s*.15,y,z+.37)],.046,'gold',[1,1,.2])
        if not queen:
            link('Staff • cross bar',(x-.25,y,z+.19),(x+.25,y,z+.19),.05,'gold')
            ico('Staff • crest',(x,y,z+.48),(.10,.065,.11),'gold',1)

def flame(p,h=.4,m='green'):
    x,y,z=p
    for i in range(3):
        a=2*pi*i/3
        path('Magic • sculpted flame',[(x+cos(a)*h*.13,y+sin(a)*h*.13,z-h*.2),(x+cos(a)*h*.20,y+sin(a)*h*.15,z+h*.10),(x+cos(a+.7)*h*.22,y+sin(a+.7)*h*.10,z+h*.42),(x+cos(a+1.2)*h*.05,y,z+h*.65)],h*.17,m,[1,1,.62,.01])

def pawn(dark):
    base(dark);boots(.59,dark)
    cape(.78,1.61,dark,.44,.10);armor(1.23,dark)
    face(1.94,orc=dark);helmet(1.99,dark)
    if dark:
        path('Orc • scarf',[(-.28,-.22,1.70),(0,-.31,1.65),(.28,-.22,1.72)],.095,'purplelight')
        skull(0,-.254,1.24,.11)
    link('Arm • shield arm',(-.36,0,1.43),(-.43,-.24,1.16),.13,'ironedge' if dark else 'silver')
    shield(-.40,-.34,1.23,dark)
    link('Arm • weapon arm',(.35,0,1.43),(.48,-.14,1.13),.13,'ironedge' if dark else 'silver')
    hand((.49,-.18,1.19),dark)
    if dark:
        link('Axe • haft',(.48,-.15,.99),(.52,-.15,2.17),.045,'leather')
        profile('Axe • crescent blade',[(.50,2.18),(.78,2.26),(.77,2.08),(.91,1.92),(.76,1.68),(.62,1.74),(.65,1.93),(.50,1.97)],-.15,.13,'ironedge',.025)
        path('Axe • fel edge',[(.80,-.235,2.19),(.84,-.235,1.96),(.74,-.235,1.72)],.022,'green')
        cone('Helmet • orc spike',(0,.01,2.62),.10,0,.25,'bone',8,.002)
    else:sword(.49,-.18,1.37,.76)

def rook(dark):
    base(dark)
    stone='darkstone' if dark else 'stone';light='darkstone2' if dark else 'stone2'
    cone('Tower • inner structure',(0,0,1.18),.59,.48,1.48,stone,32,.025)
    for row in range(5):
        for i in range(10):
            a=2*pi*(i+row*.5)/10
            ring_sector('Tower • ashlar block',.48,.62-.018*row,.48+row*.24,.706+row*.24,a+.018,a+2*pi/10-.018,light if (row+i)%5==0 else stone,4)
    # Recesses built on the surface with dressed-stone surrounds.
    if dark:
        gem('Tower • soul gate',(0,-.62,1.05),(.34,.045,.91),'greendark')
        gem('Tower • soul gate core',(0,-.66,1.08),(.16,.02,.68),'green')
        skull(0,-.585,1.75,.24,'green')
        for x in [-.62,.62]:
            link('Tower • banner rail',(x-.10,-.02,1.83),(x+.1,-.02,1.83),.035,'ironedge')
            profile('Tower • violet pennant',[(x-.12,1.78),(x+.12,1.78),(x+.12,1.18),(x,1.29),(x-.12,1.18)],-.08,.03,'purple',.004)
            skull(x,-.105,1.58,.075)
    else:
        profile('Tower • arrow slit',[(-.09,1.02),(.09,1.02),(.09,1.31),(0,1.39),(-.09,1.31)],-.586,.03,'eye',.008)
        for x in [-.13,.13]:box('Tower • window surround',(x,-.59,1.19),(.067,.07,.36),light,.018)
    cone('Tower • machicolation lip',(0,0,1.89),.65,.70,.18,light,32,.025)
    cone('Tower • parapet floor',(0,0,2.01),.71,.71,.10,stone,32,.02)
    for i in range(8):
        a=2*pi*i/8
        o=box('Tower • battlement',(cos(a)*.56,sin(a)*.56,2.17),(.28,.27,.36),light,.045);o.rotation_euler.z=a
        if dark:cone('Tower • spiked merlon',(cos(a)*.56,sin(a)*.56,2.44),.13,0,.25,'bone',6,.006)
    if not dark:
        link('Tower • flagstaff',(0,.09,2.02),(0,.09,2.91),.025,'gold')
        ico('Tower • flag finial',(0,.09,2.95),(.065,.065,.09),'gold',1)
        vs=[]
        for j in range(2):
            for i in range(9):
                t=i/8;vs.append((t*.65,.09+.08*sin(t*pi*2),2.81-j*.25+.04*sin(t*pi*1.4)))
        mesh('Tower • rippling royal pennant',vs,[(i,i+1,i+10,i+9) for i in range(8)],'blue',True)
    else:flame((0,0,2.18),.50,'green')

def knight(dark):
    base(dark)
    # A fully volumetric mount with four legs, barding, reins and rider.
    horse='darkstone2' if dark else 'horse'
    ell('Mount • barrel',(0,.04,1.07),(.32,.40,.28),horse,24,14)
    for side in [-1,1]:
        for rear in [-1,1]:
            y=.04+rear*.23;x=side*.21
            a=(x,y,1.03);b=(x,y+(.04 if rear>0 else -.08),.79);c=(x,y-.025,.56)
            link('Mount • upper leg',a,b,.105,horse,.073);link('Mount • lower leg',b,c,.067,horse,.05)
            ell('Mount • hoof',(x,y-.07,.53),(.09,.13,.075),'hoof')
            if dark:flame((x,y-.05,.60),.19,'green')
    path('Mount • arched neck',[(0,-.19,1.10),(0,-.29,1.36),(0,-.34,1.60)],.19,horse,[1.1,1,.82])
    ell('Mount • head',(0,-.42,1.59),(.19,.27,.20),horse)
    ell('Mount • muzzle',(0,-.64,1.46),(.17,.17,.13),horse)
    for s in [-1,1]:
        cone('Mount • ear',(s*.12,-.30,1.84),.062,.015,.24,horse,12,.01)
        ell('Mount • eye',(s*.167,-.51,1.64),(.028,.041,.044),'green' if dark else 'eye',12,8)
        ell('Mount • nostril',(s*.104,-.755,1.47),(.028,.02,.024),'eye',12,8)
        path('Mount • bridle',[(s*.17,-.28,1.77),(s*.19,-.53,1.60),(s*.15,-.70,1.43)],.018,'iron' if dark else 'leather')
        path('Mount • reins',[(s*.15,-.67,1.47),(s*.22,-.37,1.26),(s*.27,-.04,1.54)],.015,'leather')
    for j in range(5):ico('Mount • mane',(0,-.18+j*.045,1.60-j*.075),(.12,.09,.12),'purple' if dark else 'leather',1)
    path('Mount • tail',[(0,.37,1.13),(0,.51,1.15),(.13,.58,.93),(.17,.61,.73)],.085,'purple' if dark else 'leather',[1,1,.8,.03])
    # Barding is two side shields visible from either flank.
    for s in [-1,1]:
        o=profile('Mount • gold barding',[(-.28,.85),(.28,.85),(.24,1.25),(-.24,1.25)],0,.035,'gold',.01)
        o.rotation_euler.z=pi/2;o.location.x=s*.32;o.location.y=.13
        o=profile('Mount • enamel barding',[(-.23,.89),(.23,.89),(.20,1.21),(-.20,1.21)],0,.03,'purple' if dark else 'blue',.007)
        o.rotation_euler.z=pi/2;o.location.x=s*.345;o.location.y=.13
    ell('Rider • cuirass',(0,.08,1.66),(.26,.18,.29),'iron' if dark else 'silver')
    for s in [-1,1]:
        link('Rider • thigh',(s*.15,.12,1.47),(s*.34,.07,1.27),.10,'iron')
        link('Rider • greave',(s*.34,.07,1.27),(s*.34,-.02,1.09),.075,'ironedge' if dark else 'silver')
        ell('Rider • boot',(s*.34,-.09,1.07),(.095,.14,.075),'leather')
        ell('Rider • pauldron',(s*.25,.06,1.83),(.13,.17,.12),'ironedge' if dark else 'silverlight')
    cape(1.21,1.93,dark,.41,.25)
    face(2.15,dark=dark);helmet(2.20,dark)
    link('Rider • rein arm',(-.23,.01,1.80),(-.25,-.20,1.56),.09,'ironedge' if dark else 'silver')
    hand((-.23,-.20,1.55),dark)
    link('Rider • weapon arm',(.24,.02,1.81),(.39,-.01,1.64),.09,'ironedge' if dark else 'silver')
    hand((.39,-.01,1.68),dark)
    if dark:sword(.40,-.02,1.84,.71,True)
    else:
        link('Lance • shaft',(.41,-.02,1.10),(.41,-.02,2.57),.026,'leather')
        ico('Lance • leaf point',(.41,-.02,2.70),(.085,.055,.22),'silverlight',1)
        profile('Lance • blue pennon',[(.42,2.51),(.70,2.45),(.44,2.28)],.01,.016,'blue',.004)
    if dark:skull(0,-.71,1.76,.13,'green')

def robe(dark=False,queen=False):
    main='purple' if dark else ('blue' if queen else 'ivory')
    cone('Robes • folded skirt',(0,.02,.98),.49,.24,1.06,main,32,.025)
    for i in range(9):
        a=2*pi*i/9
        path('Robes • sculpted folds',[(cos(a)*.44,sin(a)*.44+.02,.52),(cos(a)*.37,sin(a)*.37+.02,.87),(cos(a)*.25,sin(a)*.25+.02,1.40)],.035,'purplelight' if dark else ('bluelight' if queen else 'fur'),[1,.6,.2])
    cone('Robes • bodice',(0,0,1.57),.26,.32,.43,main,24,.02)
    # Central tabard and gold piping are polygonal volumes.
    pts=[(-.105,1.80),(.105,1.80),(.22,.55),(-.22,.55)]
    profile('Robes • gold tabard border',pts,-.28,.035,'gold',.01)
    pts=[(-.069,1.76),(.069,1.76),(.16,.60),(-.16,.60)]
    profile('Robes • enamel tabard',pts,-.307,.025,'purple' if dark else 'blue',.007)
    box('Robes • leather belt',(0,-.015,1.30),(.58,.49,.075),'leather',.012)
    gem('Robes • jeweled clasp',(0,-.30,1.32),(.14,.035,.18),'pink' if dark else 'cyan')
    cape(.64,1.93,dark,.63,.18)

def hood(z=2.10,dark=False):
    m='purple' if dark else 'ivory'
    # Rear hood volume plus a thick pointed opening around a shadowed face.
    ell('Hood • back',(0,.065,z+.02),(.35,.29,.40),m,24,16)
    # remove visual overlap by putting the dark aperture well forward.
    profile('Hood • dark face opening',[(-.25,z-.21),(.25,z-.21),(.20,z+.16),(0,z+.39),(-.20,z+.16)],-.257,.055,'eye',.02)
    path('Hood • gold welt',[(-.27,-.29,z-.22),(-.26,-.29,z+.08),(-.18,-.27,z+.25),(0,-.23,z+.43),(.18,-.27,z+.25),(.26,-.29,z+.08),(.27,-.29,z-.22)],.037,'gold')
    if dark:eyes(0,-.30,z-.015,.105,True,False)
    else:
        ell('Hood • face',(0,-.28,z-.03),(.185,.075,.215),'skinlight',20,12)
        eyes(0,-.35,z+.015,.084,False,False)

def bishop(dark):
    base(dark);robe(dark);hood(2.12,dark)
    # Wide readable sleeves, a blessing hand and the staff.
    for s in [-1,1]:
        a=(s*.28,0,1.73);b=(s*.49,-.12,1.50 if s<0 else 1.79)
        link('Mage • flared sleeve',a,b,.16,'purple' if dark else 'ivory',.21)
        cone('Mage • cuff',(s*.48,-.12,b[2]),.17,.17,.08,'gold',20,.008).rotation_euler.y=pi/2
        hand((s*.53,-.16,b[2]+.07),dark,open=s>0)
    path('Mage • shoulder stole',[(-.35,-.12,1.90),(0,-.33,1.78),(.35,-.12,1.90)],.095,'purplelight' if dark else 'blue')
    if dark:
        skull(0,-.37,1.79,.115)
        profile('Mage • mitre',[(-.27,2.34),(.27,2.34),(.20,2.73),(0,2.95),(-.20,2.73)],-.03,.26,'purple',.025)
        path('Mage • mitre gold edge',[(-.27,-.17,2.34),(-.20,-.17,2.73),(0,-.17,2.95),(.20,-.17,2.73),(.27,-.17,2.34)],.027,'gold')
        gem('Mage • mitre jewel',(0,-.18,2.65),(.15,.04,.28),'pink')
    else:
        box('Cleric • cross vertical',(0,-.37,1.77),(.07,.05,.27),'gold',.015)
        box('Cleric • cross horizontal',(0,-.37,1.79),(.22,.05,.065),'gold',.015)
    staff(-.56,-.15,2.10,dark)

def queen(dark):
    base(dark);robe(dark,True)
    # Hair masses are swept around the head; face remains fully readable.
    ell('Queen • hair back',(0,.08,2.19),(.35,.25,.39),'darkhair' if dark else 'hair')
    for s in [-1,1]:
        for j in range(3):path('Queen • flowing hair',[(s*.23,.02,2.40-j*.10),(s*.36,.01,2.25-j*.14),(s*(.39+.04*j),.12,1.98-j*.15),(s*.31,.15,1.83-j*.10)],.095,'darkhair' if dark else 'hair',[1,1,1,.05])
    face(2.19,dark=True,queen=True)
    path('Queen • swept fringe',[(-.27,-.20,2.29),(-.18,-.27,2.42),(0,-.28,2.40),(.20,-.22,2.48)],.075,'darkhair' if dark else 'hair',[.5,1,1,.15])
    crown(2.50,dark,.33)
    for s in [-1,1]:
        ell('Queen • fur shoulder',(s*.29,0,1.90),(.20,.23,.115),'ironedge' if dark else 'fur')
        link('Queen • upper sleeve',(s*.31,0,1.78),(s*.43,-.07,1.55),.12,'purple' if dark else 'blue')
    link('Queen • staff forearm',(.43,-.07,1.55),(.52,-.17,1.80),.095,'purple' if dark else 'ivory')
    hand((.52,-.17,1.83),False);staff(.55,-.16,2.20,dark,True)
    link('Queen • open forearm',(-.43,-.07,1.55),(-.61,-.17,1.65),.092,'purple' if dark else 'ivory')
    hand((-.62,-.17,1.70),False,True)
    for zz in [1.48,1.85]:gem('Queen • tabard jewel',(0,-.355,zz),(.15,.05,.20),'pink' if dark else 'cyan')
    if dark:
        skull(0,-.34,1.69,.105)
        for s in [-1,1]:path('Queen • crown horn',[(s*.25,0,2.50),(s*.39,0,2.70),(s*.29,-.01,2.91)],.075,'gold',[1,1,.01])
        flame((-.62,-.17,1.95),.38,'pink')

def king(dark):
    base(dark)
    if dark:
        robe(True);hood(2.20,True)
        crown(2.62,True,.31)
        for s in [-1,1]:
            path('VezNan • horn',[(s*.26,.04,2.55),(s*.49,.07,2.69),(s*.51,.07,2.91),(s*.37,.035,3.10)],.10,'bone',[1,1,.65,.015])
            ell('VezNan • sharp pauldron',(s*.32,.02,1.95),(.21,.23,.10),'gold')
        link('VezNan • casting sleeve',(-.32,0,1.85),(-.53,-.14,1.99),.15,'purple',.22)
        hand((-.63,-.19,2.07),True,True)
        link('VezNan • staff sleeve',(.32,0,1.84),(.48,-.10,1.64),.16,'purple',.19)
        hand((.51,-.14,1.81),True)
        staff(.56,-.12,2.49,True)
        skull(0,-.35,1.94,.14)
        for zz in [.80,1.1,1.52]:gem('VezNan • runic tabard',(0,-.34,zz),(.17,.035,.21),'pink')
        path('VezNan • collar left',[(-.35,-.07,2.10),(-.28,-.20,1.94),(0,-.36,1.87)],.035,'gold')
        path('VezNan • collar right',[(.35,-.07,2.10),(.28,-.20,1.94),(0,-.36,1.87)],.035,'gold')
    else:
        boots(.59,False,.22);cape(.61,1.96,False,.65,.12);armor(1.32,False)
        face(2.20)
        # Broad golden beard, split moustache and brows distinguish King Denas.
        ell('Denas • beard',(0,-.17,1.98),(.31,.24,.30),'hair')
        for s in [-1,1]:
            path('Denas • curled moustache',[(s*.015,-.33,2.14),(s*.16,-.40,2.12),(s*.29,-.37,2.01)],.09,'hair',[.6,1,.1])
            path('Denas • sideburn',[(s*.25,-.05,2.36),(s*.29,-.12,2.18),(s*.25,-.23,2.02)],.075,'hair')
        crown(2.56,False,.35)
        # Sculpted ermine collar, not a flat torus.
        for i in range(11):
            a=pi*.04+pi*.92*i/10
            ell('Denas • ermine collar',(cos(a)*.48,sin(a)*.24-.02,1.88+.055*sin(a)),(.18,.15,.13),'fur',16,10)
        for s in [-1,1]:
            path('Denas • folded arm',[(s*.34,-.02,1.69),(s*.37,-.27,1.52),(s*.11,-.39,1.58)],.14,'silver')
            link('Denas • wrist cuff',(s*.20,-.36,1.56),(s*.09,-.40,1.59),.15,'gold')
            hand((s*.075,-.43,1.62))
        sword(0,-.46,1.39,.91,False,True)
        gem('Denas • sword guard jewel',(0,-.52,1.39),(.16,.05,.20),'goldlight')

BUILDERS=[('01_Quan_tot','Pawn',pawn),('02_Quan_xe','Rook',rook),('03_Quan_ngua','Knight',knight),('04_Quan_tuong','Bishop',bishop),('05_Quan_hau','Queen',queen),('06_Quan_vua','King',king)]

def apply_meshes(obs):
    for o in obs:
        bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
        for mod in list(o.modifiers):
            try:bpy.ops.object.modifier_apply(modifier=mod.name)
            except RuntimeError:pass
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)

def bounds(obs):
    bpy.context.view_layer.update()
    vs=[o.matrix_world@Vector(v) for o in obs for v in o.bound_box]
    lo=Vector([min(v[i] for v in vs) for i in range(3)]);hi=Vector([max(v[i] for v in vs) for i in range(3)])
    return lo,hi

def uv(obs):
    # Palette materials make color independent of UV, but every mesh still has usable UVs.
    for o in obs:
        if o.get('preserve_uv'):continue
        if not o.data.uv_layers:o.data.uv_layers.new(name='UVMap')
        layer=o.data.uv_layers.active.data
        for poly in o.data.polygons:
            axis=max(range(3),key=lambda i:abs(poly.normal[i]));axes=[i for i in range(3) if i!=axis]
            for li in poly.loop_indices:
                co=o.data.vertices[o.data.loops[li].vertex_index].co
                layer[li].uv=(co[axes[0]]*.5+.5,co[axes[1]]*.5+.5)

def root_objects(name,obs):
    root=bpy.data.objects.new(name,None);bpy.context.scene.collection.objects.link(root)
    for o in obs:o.parent=root
    root['reference_image']=str(REF/(name+'.png'))
    root['faction']='Denas / White' if name.endswith('Denas') else 'VezNan / Black'
    root['front']='-Y in Blender; -Z in Unity';root['pivot']='ground center'
    return root

def export_fbx(name,obs):
    # Keep component meshes in .blend; consolidate export into one renderer per asset.
    copies=[]
    for src in obs:
        if src.type!='MESH':continue
        o=src.copy();o.data=src.data.copy();bpy.context.scene.collection.objects.link(o)
        world=src.matrix_world.copy();o.parent=None;o.matrix_world=world;copies.append(o)
    bpy.ops.object.select_all(action='DESELECT')
    for o in copies:o.select_set(True)
    bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();joined=bpy.context.object
    joined.name=name
    bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.export_scene.fbx(filepath=str(OUT/'Models'/(name+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},
        global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
        use_mesh_modifiers=True,mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False,path_mode='COPY',embed_textures=True,use_custom_props=True)
    bpy.data.objects.remove(joined,do_unlink=True)

def camera(s,p,target,ortho,name='Camera'):
    d=bpy.data.cameras.new(name);o=bpy.data.objects.new(name,d);s.collection.objects.link(o);o.location=p;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
    d.type='ORTHO';d.ortho_scale=ortho;d.lens=50;d.clip_end=500;s.camera=o;return o

def light(s,name,p,power,color,size,target=(0,0,0)):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.color=color;d.shape='DISK';d.size=size
    o=bpy.data.objects.new(name,d);s.collection.objects.link(o);o.location=p;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();return o

def render_settings(s,w,h,transparent=False):
    try:s.render.engine='CYCLES'
    except TypeError:pass
    s.cycles.samples=32;s.cycles.use_denoising=True
    # CPU is portable. Prefer a local CUDA/OptiX GPU when Blender reports one.
    try:
        prefs=bpy.context.preferences.addons['cycles'].preferences
        prefs.compute_device_type='OPTIX';prefs.get_devices()
        devices=[d for d in prefs.devices if d.type=='OPTIX']
        if devices:
            for d in prefs.devices:d.use=d in devices
            s.cycles.device='GPU'
    except Exception:pass
    s.render.resolution_x=w;s.render.resolution_y=h;s.render.resolution_percentage=100
    s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGBA' if transparent else 'RGB';s.render.film_transparent=transparent
    s.world=bpy.data.worlds.new(s.name+' • sky');s.world.use_nodes=True
    n=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND');n.inputs['Color'].default_value=(.35,.43,.60,1);n.inputs['Strength'].default_value=.35
    s.view_settings.view_transform='AgX'
    try:s.view_settings.look='AgX - Medium High Contrast'
    except TypeError:pass
    s.render.image_settings.color_depth='8'

def save_blend(path):
    bpy.data.libraries.write(str(path),{bpy.context.scene},fake_user=True,compress=True)

def reference_collection(s,name):
    c=bpy.data.collections.new('REFERENCE • source image (non-rendering)');s.collection.children.link(c)
    img=bpy.data.images.load(str(REF/(name+'.png')),check_existing=True);img.pack()
    o=bpy.data.objects.new('REFERENCE • '+name,None);o.empty_display_type='IMAGE';o.data=img;o.empty_display_size=2.5;o.location=(2,1,1.2);o.rotation_euler=(pi/2,0,0);c.objects.link(o)
    c.hide_render=True;c.hide_viewport=True

def piece_art():
    global PARTS
    for prefix,kind,fn in BUILDERS:
        for dark in [False,True]:
            name=prefix+('_Veznan' if dark else '_Denas');s=fresh(name);PARTS=[];fn(dark)
            # Broader heads and torsos keep the supplied heroic chibi silhouettes readable.
            for o in PARTS:
                if o.get('plinth_component') or kind=='Rook':continue
                factor=1.0
                if o.name.startswith(('Face','Hood','Helmet','Crown','Denas • beard','Denas • curled','Denas • sideburn','Queen • hair','Queen • flowing','Queen • swept')):factor=1.25
                elif o.name.startswith(('Armor','Denas • folded','Denas • wrist','Robes')):factor=1.13
                if factor!=1:
                    for v in o.data.vertices:v.co.x*=factor
                    o.location.x*=factor
            obs=list(PARTS);apply_meshes(obs);lo,hi=bounds(obs)
            target=MEAS[kind]['dimensions'];scale_z=target[2]/(hi.z-lo.z)
            # Exact reference height and the same maximum footprint, with XY aspect preserved.
            scale_x=target[0]/(hi-lo).x;scale_y=target[1]/(hi-lo).y
            center=(lo+hi)/2
            for o in obs:
                for v in o.data.vertices:
                    w=o.matrix_world@v.co;v.co=((w.x-center.x)*scale_x,(w.y-center.y)*scale_y,(w.z-lo.z)*scale_z)
                o.location=(0,0,0);o.rotation_euler=(0,0,0);o.scale=(1,1,1)
            uv(obs);root=root_objects(name,obs)
            lo2,hi2=bounds(obs);dims=list(hi2-lo2)
            root['reference_height']=target[2];root['reference_max_footprint']=max(target[:2])
            root['height_m']=dims[2];root['max_footprint_m']=max(dims[:2])
            export_fbx(name,[root]+obs)
            reference_collection(s,name)
            render_settings(s,800,1000,True);h=dims[2]
            camera(s,(h*2.2,-h*4.3,h*2.35),(0,0,h*.49),h*1.25)
            light(s,'Key • warm softbox',(-2,-3,4),400,(1,.85,.68),3,(0,0,.7))
            light(s,'Fill • blue sky',(3,-1,2),220,(.60,.76,1),2,(0,0,.7))
            light(s,'Rim • silhouette',(1,2,3),450,(.8,.87,1),2,(0,0,.7))
            s.render.filepath=str(OUT/'Previews'/(name+'.png'))
            # Open source files in a useful material preview, centered on the sculpt.
            for screen in bpy.data.screens:
                for area in screen.areas:
                    if area.type=='VIEW_3D':
                        area.spaces.active.region_3d.view_distance=h*2.8;area.spaces.active.region_3d.view_location=(0,0,h*.5)
                        area.spaces.active.shading.type='MATERIAL'
            save_blend(SRC/'Pieces'/(name+'.blend'))
            bpy.ops.render.render(write_still=True)
            tris=sum(len(p.vertices)-2 for o in obs for p in o.data.polygons)
            REPORT['pieces'].append({'name':name,'kind':kind,'faction':'Black' if dark else 'White','reference_height':target[2],
                'dimensions_blender_xyz':dims,'max_footprint':max(dims[:2]),'reference_max_footprint':max(target[:2]),'triangles':tris,'mesh_parts':len(obs),
                'fbx':str((OUT/'Models'/(name+'.fbx')).relative_to(ROOT)).replace('\\','/'),'blend':str((SRC/'Pieces'/(name+'.blend')).relative_to(ROOT)).replace('\\','/')})
            print('KR_PIECE_DONE',name,json.dumps(REPORT['pieces'][-1]),flush=True)
    (SRC/'asset_manifest.json').write_text(json.dumps(REPORT,indent=2),encoding='utf-8')

if __name__=='__main__':
    piece_art()
