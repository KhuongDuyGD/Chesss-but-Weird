"""Author the original low-poly combat and landscape props. No Unity primitives or external assets.
Models face +Z, one authored unit = one board tile. Run with Python 3 from any directory.
"""
from pathlib import Path
import math

ROOT = Path(__file__).resolve().parents[1] / 'Assets/Resources/BuffVfx/Models'

class Model:
    def __init__(self): self.lines = ['# Chess But Weird - original stylized combat prop']; self.index = 0
    def part(self, name, material, rings):
        self.lines += ['o ' + material + '_' + name, 'usemtl ' + material]
        start = self.index + 1
        for ring in rings:
            for vertex in ring:
                self.lines.append('v %.6f %.6f %.6f' % tuple(vertex)); self.index += 1
        n = len(rings[0])
        def face(ids): self.lines.append('f ' + ' '.join(str(i) for i in ids))
        face([start+i for i in reversed(range(n))])
        for r in range(len(rings)-1):
            for i in range(n):
                a=start+r*n+i; b=start+r*n+(i+1)%n
                face([a,b,b+n,a+n])
        face([start+(len(rings)-1)*n+i for i in range(n)])
    def box(self, name, material, center, scale, bevel=.012):
        x,y,z=center; w,h,d=[s/2 for s in scale]; b=min(bevel,w*.4,h*.4,d*.4)
        rings=[]
        for zz,shrink in [(-d,b),(-d+b,0),(d-b,0),(d,b)]:
            a=w-shrink; c=h-shrink
            outline=[(-a+b,-c),(a-b,-c),(a,-c+b),(a,c-b),(a-b,c),(-a+b,c),(-a,c-b),(-a,-c+b)]
            rings.append([(x+xx,y+yy,z+zz) for xx,yy in outline])
        self.part(name,material,rings)
    def tube(self,name,material,center,radius,length,axis='z',segments=10,end_radius=None):
        x,y,z=center; end_radius=radius if end_radius is None else end_radius
        rings=[]
        for along,r in [(-length/2,radius),(length/2,end_radius)]:
            ring=[]
            for i in range(segments):
                a=2*math.pi*i/segments; u=math.cos(a)*r; v=math.sin(a)*r
                ring.append((x+u,y+v,z+along) if axis=='z' else (x+along,y+u,z+v) if axis=='x' else (x+u,y+along,z+v))
            rings.append(ring)
        self.part(name,material,rings)
    def save(self,name):
        ROOT.mkdir(parents=True,exist_ok=True)
        # Preserve authored bevels while merging parts by paint: at most four renderers per prop.
        vertices=[]; faces={}; material='metal'
        for line in self.lines:
            if line.startswith('v '): vertices.append(line)
            elif line.startswith('usemtl '): material=line.split()[1]
            elif line.startswith('f '): faces.setdefault(material,[]).append(line)
        lines=['# Original Chess But Weird combat prop','mtllib CombatPalette.mtl']+vertices+['s off']
        for material,polygons in faces.items(): lines+=['o '+material+'_'+name,'usemtl '+material]+polygons
        (ROOT/(name+'.obj')).write_text('\n'.join(lines)+'\n',encoding='ascii')

def saddle(m):
    m.tube('piece_saddle','metal',(0,-.2,-.05),.19,.06,'y',12)
    m.box('left_support','brass',(-.15,-.08,-.05),(.045,.23,.08))
    m.box('right_support','brass',(.15,-.08,-.05),(.045,.23,.08))

def rifle(name,short=False):
    m=Model(); saddle(m)
    m.box('sculpted_stock','wood',(0,.03,-.38),(.13,.17,.34),.025)
    m.box('shoulder_pad','metal',(0,.035,-.55),(.16,.2,.055))
    m.box('receiver','metal',(0,.09,-.08),(.13,.15,.34))
    m.box('receiver_inlay','brass',(.069,.09,-.08),(.012,.055,.2),.003)
    m.box('grip','wood',(0,-.06,-.17),(.085,.2,.12))
    m.box('trigger_floor','brass',(0,-.105,-.02),(.035,.025,.14),.003)
    m.box('trigger_front','brass',(0,-.055,.045),(.035,.1,.025),.003)
    m.box('trigger','metal',(0,-.04,-.01),(.017,.065,.02),.003)
    m.box('magazine','metal',(0,-.055,.09),(.085,.18,.12))
    m.box('fore_end','wood',(0,.075,.25),(.12,.115,.3))
    length=.38 if short else .62; end=.61 if short else .84
    m.tube('barrel','metal',(0,.13,end-length/2),.032 if not short else .05,length)
    m.tube('muzzle_band','brass',(0,.13,end-.025),.055 if not short else .09,.055)
    m.tube('muzzle_bore','metal',(0,.13,end+.004),.026 if not short else .06,.008)
    m.box('bolt','brass',(.09,.12,-.08),(.12,.035,.035),.005)
    m.tube('bolt_knob','metal',(.155,.12,-.08),.03,.04,'y',8)
    if not short:
        for z in [-.19,.03]: m.box('scope_mount','brass',(0,.205,z),(.065,.09,.04))
        m.tube('scope','metal',(0,.275,-.045),.055,.4)
        m.tube('scope_front','brass',(0,.275,.165),.073,.07)
        m.tube('scope_lens','glass',(0,.275,.203),.058,.008)
        m.tube('scope_rear','brass',(0,.275,-.26),.065,.04)
        for x in [-.08,.08]: m.box('bipod_folded','metal',(x,.025,.37),(.025,.03,.25),.004)
    else:
        m.box('crown_sight','brass',(0,.235,.08),(.1,.045,.09))
        for x in [-.04,0,.04]: m.box('crown_tip','brass',(x,.28,.08),(.02,.065 if x==0 else .04,.035),.004)
    m.save(name)

def cannon():
    m=Model()
    m.tube('turret_collar','metal',(0,-.13,0),.25,.13,'y',12)
    m.tube('turret_band','brass',(0,-.06,0),.255,.035,'y',12)
    for x in [-.19,.19]:
        m.box('trunnion_support','metal',(x,.045,.03),(.08,.22,.29))
        m.tube('pivot','brass',(x,.075,.04),.055,.08,'y',8)
    m.tube('breech','metal',(0,.13,.02),.145,.29,segments=12)
    m.tube('tapered_barrel','metal',(0,.13,.37),.105,.44,end_radius=.075,segments=12)
    for z,r in [(.2,.115),(.4,.1),(.65,.105)]: m.tube('reinforcing_band','brass',(0,.13,z),r,.05,segments=12)
    m.tube('muzzle','metal',(0,.13,.72),.11,.11,segments=12)
    m.tube('dark_bore','metal',(0,.13,.78),.071,.008,segments=12)
    m.box('loading_hatch','wood',(0,.17,-.17),(.16,.1,.08))
    for x in [-.24,.24]:
        m.box('stabilizer','metal',(x,-.18,.1),(.11,.07,.38))
        m.box('stabilizer_inlay','brass',(x,-.14,.1),(.08,.015,.28),.004)
    m.save('Cannon')

def landscape():
    m=Model()
    m.box('crate_floor','wood',(0,.025,0),(.62,.05,.54))
    for y in [.08,.20,.32,.44]:
        for z in [-.25,.25]:m.box('front_rear_planks','wood',(0,y,z),(.62,.11,.045))
        for x in [-.29,.29]:m.box('side_planks','wood',(x,y,0),(.045,.11,.46))
    for x in [-.21,.21]:
        m.box('front_strap','metal',(x,.27,.277),(.055,.49,.025))
        m.box('rear_strap','metal',(x,.27,-.277),(.055,.49,.025))
    m.box('latch','brass',(0,.4,.29),(.1,.12,.035));m.save('SupplyCrate')
    m=Model();m.box('lid_plank','wood',(0,.02,.27),(.65,.075,.57))
    for x in [-.21,.21]:m.box('lid_strap','metal',(x,.062,.27),(.055,.025,.57))
    m.tube('hinge','brass',(0,0,0),.035,.25,'x',segments=8)
    m.save('CrateLid')
    m=Model();m.tube('low_profile_body','metal',(0,.065,0),.3,.12,'y',12)
    m.tube('rim','brass',(0,.125,0),.285,.035,'y',12)
    m.tube('pressure_plate','metal',(0,.155,0),.2,.035,'y',10)
    m.tube('indicator','glass',(0,.18,0),.05,.025,'y',8)
    for x in [-.14,.14]:m.box('hazard_mark','brass',(x,.182,.03),(.035,.012,.12),.002)
    m.save('Landmine')
    m=Model()
    for x in [-.27,.27]:
        for z in [-.19,.19]:m.box('sawhorse_foot','metal',(x,.14,z),(.065,.28,.075))
        m.box('cross_foot','wood',(x,.035,0),(.11,.07,.48))
    m.box('caution_beam','wood',(0,.32,0),(.78,.19,.09))
    for side in [-1,1]:
        m.box('dark_stripe_backing','metal',(0,.325,side*.055),(.75,.15,.018))
        for x in [-.3,-.15,0,.15,.3]:
            z=side*.071
            m.part('caution_slash','brass', [[(x-.05,.25,z-.004),(x+.01,.25,z-.004),(x+.09,.4,z-.004),(x+.03,.4,z-.004)],[(x-.05,.25,z+.004),(x+.01,.25,z+.004),(x+.09,.4,z+.004),(x+.03,.4,z+.004)]])
    m.save('ConstructionBarrier')
    m=Model();m.tube('substitution_log','wood',(0,.16,0),.16,.62,segments=10)
    for z in [-.23,.23]:m.tube('ninja_binding','metal',(0,.16,z),.166,.05,segments=10)
    for z in [-.317,.317]:m.tube('growth_ring','brass',(0,.16,z),.105,.012,segments=10)
    m.box('training_crest','brass',(0,.33,0),(.13,.09,.1));m.save('DecoyLog')

def strategy():
    m=Model();m.tube('crown_band','brass',(0,0,0),.22,.065,'y',16)
    for i in range(8):
        a=i*math.pi/4;x=math.cos(a)*.2;z=math.sin(a)*.2
        m.box('crown_point','brass',(x,.07,z),(.045,.15,.045),.008)
        if i%2==0:m.tube('crown_gem','glass',(x,.15,z),.034,.035,'y',8)
    m.save('CrownHalo')
    m=Model()
    for i in range(12):
        a=i*math.pi/6;b=(i+1)*math.pi/6
        m.part('canopy_panel','glass' if i%2==0 else 'wood', [[(0,.25,0),(math.cos(a)*.25,.18,math.sin(a)*.25),(math.cos(b)*.25,.18,math.sin(b)*.25)],[(0,.24,0),(math.cos(a)*.43,0,math.sin(a)*.43),(math.cos(b)*.43,0,math.sin(b)*.43)]])
    for x in [-.29,.29]:
        for z in [-.29,.29]:m.box('parachute_rope','brass',(x,-.34,z),(.012,.68,.012),.002)
    m.box('parachute_harness','metal',(0,-.67,0),(.25,.035,.25));m.save('Parachute')
    m=Model();m.box('embossed_card','metal',(0,0,0),(.4,.54,.04),.035)
    m.box('card_inlay','brass',(0,0,-.027),(.32,.46,.016),.02)
    m.box('card_chip','metal',(-.07,.07,-.04),(.09,.09,.015),.01)
    m.box('card_signature','glass',(.025,-.15,-.04),(.22,.028,.015),.004)
    m.tube('currency_coin','brass',(.21,-.17,-.075),.115,.03,segments=12)
    m.box('currency_mark','metal',(.21,-.17,-.096),(.025,.14,.012),.003);m.save('CreditCard')

if __name__=='__main__':
    rifle('Sniper'); rifle('KingRifle',True); cannon()
    landscape()
    strategy()
    (ROOT/'CombatPalette.mtl').write_text('newmtl metal\nKd 0.16 0.19 0.24\nnewmtl wood\nKd 0.47 0.28 0.19\nnewmtl brass\nKd 0.91 0.66 0.27\nnewmtl glass\nKd 0.37 0.71 0.73\n',encoding='ascii')
    print('Authored 3 combat, 5 landscape and 3 strategy OBJ models in',ROOT)
