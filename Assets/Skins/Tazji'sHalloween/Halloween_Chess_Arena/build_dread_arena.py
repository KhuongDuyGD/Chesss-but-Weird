
import bpy,ast,math,bmesh,random,json
from pathlib import Path
from mathutils import Vector
from math import sin,cos,pi
scene=bpy.context.scene
OUT=Path('D:/Stuurdy/Blender/Halloween_Chess_Arena')
tree=ast.parse(Path('D:/Stuurdy/Blender/Halloween_Chess_Set/build_halloween.py').read_text(encoding='utf-8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef)],type_ignores=[]),'helpers','exec'))
for name in ['Arena_Stone_Architecture','Arena_Banners','Arena_Decor_Props','Arena_Gardens']:
 o=scene.objects.get(name)
 if o:bpy.data.objects.remove(o,do_unlink=True)
PARTS=[]
stone=mat('DREAD • Weathered tombstone',(.10,.135,.135),rough=.9)
stone2=mat('DREAD • Broken stone edges',(.18,.21,.19),rough=.85)
obsidian=mat('DREAD • Black iron',(.015,.020,.025),metal=.55,rough=.62)
bone=mat('DREAD • Old skull bone',(.42,.46,.34),rough=.78)
black=mat('DREAD • Empty sockets',(.002,.003,.003),rough=1)
skin=mat('DREAD • Rotten pumpkin',(.43,.085,.009),rough=.73)
stem=mat('DREAD • Dead wood',(.046,.028,.021),rough=.98)
fire=mat('DREAD • Hellfire',(1,.18,.006),em=4)
mint=mat('DREAD • Soul fire',(.02,.78,.35),em=3)
webmat=mat('DREAD • Cobweb',(.21,.31,.25),rough=1)
def setcolor(name,c,em=0):
 m=bpy.data.materials.get(name)
 if not m:return
 m.diffuse_color=(*c,1)
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*c,1);bs.inputs['Roughness'].default_value=.86
 bs.inputs['Emission Strength'].default_value=em
 if em:bs.inputs['Emission Color'].default_value=(*c,1)
for n,c in {
 'HW_MAT_Board_Light':(.20,.265,.23),'HW_MAT_Board_Dark':(.018,.012,.025),
 'HW_MAT_Arena_Stone':(.069,.094,.092),'HW_MAT_Arena_Stone_Light':(.13,.155,.14),
 'HW_MAT_Arena_Stone_Shadow':(.025,.035,.038),'HW_MAT_Arena_Cliff':(.021,.024,.029),
 'HW_MAT_Arena_Cliff_Light':(.055,.068,.064),'HW_MAT_Arena_Antique_Gold':(.17,.20,.14),
 'HW_Arena_Ember_Slot':(.28,.075,.014),'HW_Arena_Specter_Slot':(.04,.23,.12),
}.items():setcolor(n,c)
def finish(name):
 global PARTS
 bpy.ops.object.select_all(action='DESELECT')
 for o in PARTS:o.select_set(True)
 bpy.context.view_layer.objects.active=PARTS[0];bpy.ops.object.join();o=bpy.context.object;o.name=name
 scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
 PARTS=[];return o
def skull(pos,r,eyes=mint):
 start=len(PARTS)
 orb('Skull cranium',(0,0,.12*r),(r*.73,r*.52,r*.82),bone,12,8)
 orb('Skull cheek left',(-.47*r,-.18*r,-.18*r),(.31*r,.39*r,.34*r),bone,10,6)
 orb('Skull cheek right',(.47*r,-.18*r,-.18*r),(.31*r,.39*r,.34*r),bone,10,6)
 for x in [-.31,.31]:
  orb('Deep eye socket',(x*r,-.49*r,.12*r),(.235*r,.12*r,.27*r),black,10,8)
  orb('Possessed eye',(x*r,-.60*r,.10*r),(.045*r,.028*r,.068*r),eyes,8,6)
 shape('Nasal void',[(-.13*r,-.28*r),(.13*r,-.28*r),(0,-.03*r)],.018*r,black,y=-.57*r)
 for x in [-.39,-.195,0,.195,.39]:
  box('Skull teeth',(x*r,-.35*r,-.58*r),(.14*r,.33*r,.26*r),bone,.025*r)
 for o in PARTS[start:]:o.location+=Vector(pos)
# Perimeter foundation and battered corner columns.
for y in [-5.65,5.65]:
 box('Graveyard retaining wall',(0,y,.22),(12.5,.34,.44),stone,.05)
for side in [-1,1]:
 box('Side grave wall',(side*6.46,0,.17),(.27,10.85,.34),stone,.04)
 for y in [-5.2,5.2]:
  for z,w in [(.15,.73),(.39,.56),(.69,.47),(.98,.57)]:
   box('Crypt column course',(side*6.0,y,z),(w,w,.24),stone,.035)
  skull((side*6.0,y,1.30),.35)
  for dx in [-.22,.22]:
   rod('Column black spike',(side*6.0+dx,y,.96),(side*6.0+dx,y,1.80),.065,obsidian,.002,8)
# Spear-tipped iron railings, irregular heights.
random.seed(31)
for y in [-5.65,5.65]:
 for x in [-5.4,-4.8,-4.2,-3.6,-3,-2.4,-1.8,1.8,2.4,3,3.6,4.2,4.8,5.4]:
  h=random.uniform(.70,1.10)
  rod('Iron fence picket',(x,y,.35),(x,y,h),.025,obsidian,n=6)
  rod('Iron spearhead',(x,y,h),(x,y,h+.17),.07,obsidian,.0,6)
 rod('Railing horizontal',(-5.6,y,.67),(5.6,y,.67),.023,obsidian,n=6)
for sx in [-1,1]:
 for y in [-4.4,-3.7,-3,-2.3,-1.6,-.9,0,.9,1.6,2.3,3,3.7,4.4]:
  x=sx*6.46;h=random.uniform(.6,.88)
  rod('Side fence',(x,y,.3),(x,y,h),.023,obsidian,n=6)
  rod('Side fence spear',(x,y,h),(x,y,h+.15),.06,obsidian,0,6)
 rod('Side rail',(sx*6.46,-4.6,.53),(sx*6.46,4.6,.53),.025,obsidian,n=6)
finish('Arena_Stone_Architecture')
# Monumental pointed gateway with skull keystone and demonic horns.
outline=[(-1.63,0),(-1.63,2.45),(-1.25,3.31),(0,4.27),(1.25,3.31),(1.63,2.45),(1.63,0),(1.17,0),(1.17,2.37),(.87,2.99),(0,3.66),(-.87,2.99),(-1.17,2.37),(-1.17,0)]
start=len(PARTS);shape('Ruined Gothic arch',outline,.43,stone,y=5.10)
for sx in [-1,1]:
 box('Gateway pedestal',(sx*1.40,5.10,.15),(.80,.78,.30),stone2,.05)
 for z in [.55,1.1,1.65,2.20]:
  box('Arch masonry ledge',(sx*1.40,5.10,z),(.57,.52,.095),stone2,.018)
 # narrow recessed soul rune
 rod('Gateway soul seam',(sx*1.398,4.873,.40),(sx*1.398,4.873,2.35),.012,mint,n=6)
 # broken claw finials
 rod('Crooked arch horn',(sx*.53,5.10,3.91),(sx*.86,5.11,4.63),.12,obsidian,.052,8)
 rod('Horn tip',(sx*.86,5.11,4.63),(sx*.70,5.11,4.87),.052,obsidian,0,8)
skull((0,4.80,3.86),.55)
# Suspended rusted bars form a half-open cemetery gate.
for sx in [-1,1]:
 for dx in [.16,.38,.60,.82,1.04]:
  x=sx*dx
  rod('Gate bars',(x,5.22,.15),(x,5.22,2.45-abs(x)*.24),.018,obsidian,n=6)
 for z in [.48,1.23,2.0]:
  rod('Gate brace',(sx*.12,5.22,z),(sx*1.08,5.22,z),.026,obsidian,n=6)
# Bat silhouette at gate apex.
bat=[(-.55,.14),(-.31,.04),(-.09,.15),(-.06,.26),(0,.20),(.06,.26),(.09,.15),(.31,.04),(.55,.14),(.36,-.15),(.25,-.04),(.12,-.16),(0,-.20),(-.12,-.16),(-.25,-.04),(-.36,-.15)]
shape('Bat on gate',[(x,z+2.83) for x,z in bat],.045,obsidian,y=4.85)
finish('Arena_Gothic_Gateway')
# Twisted bare trees at rear corners, all trunks outside the slot lanes.
for side in [-1,1]:
 x=side*5.65;y=4.32
 root=(x,y,0);a=(x-side*.13,y,1.0);b=(x+side*.12,y+.10,2.0);c=(x-side*.15,y+.02,2.8)
 rod('Dead tree trunk',root,a,.21,stem,.15,9);rod('Crooked trunk',a,b,.15,stem,.11,8);rod('Upper trunk',b,c,.11,stem,.055,7)
 branches=[
 (a,(x+side*.57,y-.22,1.63),(x+side*.81,y-.44,2.19)),
 (b,(x-side*.48,y-.24,2.60),(x-side*.35,y-.50,3.20)),
 (c,(x+side*.38,y+.12,3.38),(x+side*.16,y+.08,3.88)),
 (b,(x+side*.55,y+.56,2.70),(x+side*.64,y+.75,3.18)),
 (a,(x-side*.20,y-.62,1.64),(x+side*.10,y-1.00,2.13))]
 for aa,bb,cc in branches:
  rod('Dead branch',aa,bb,.083,stem,.044,7);rod('Dead branch tip',bb,cc,.044,stem,.002,6)
  tip=Vector(bb)+Vector((side*.23,-.19,.37))
  rod('Forked twig',bb,tip,.033,stem,0,6)
 for dy in [-.3,.3]:
  rod('Gnarled root',root,(x+side*.42,y+dy,.035),.12,stem,.025,7)
 # Hanging lantern on bare branch
 lx=x+side*.74;ly=y-.43
 rod('Lantern chain',(lx,ly,2.0),(lx,ly,1.30),.012,obsidian,n=5)
 orb('Caged soul',(lx,ly,1.15),(.09,.09,.19),mint,10,8)
 for dx in [-.12,.12]:
  for dy in [-.12,.12]:rod('Lantern iron cage',(lx+dx,ly+dy,.94),(lx+dx,ly+dy,1.37),.012,obsidian,n=5)
 box('Lantern lid',(lx,ly,1.39),(.30,.30,.065),obsidian,.01)
 box('Lantern foot',(lx,ly,.93),(.30,.30,.07),obsidian,.01)
finish('Arena_Dead_Trees')
# Leaning graves, half-buried skulls and jack-o-lanterns along the outer margins.
for side in [-1,1]:
 for idx,y in enumerate([-3.20,-1.28,.75,2.55]):
  x=side*5.96
  before=len(PARTS)
  outline=[(-.28,0),(.28,0),(.28,.65),(.20,.80),(0,.90),(-.20,.80),(-.28,.65)]
  shape('Crooked tombstone',outline,.15,stone2,y=0)
  rod('Grave cross vertical',(0,-.09,.24),(0,-.09,.64),.022,black,n=6)
  rod('Grave cross bar',(-.12,-.09,.50),(.12,-.09,.50),.023,black,n=6)
  # coherent tilt of the entire monument
  from mathutils import Matrix
  rot=Matrix.Rotation(math.radians((-1)**idx*9),4,'Y')
  for o in PARTS[before:]:
   o.matrix_world=Matrix.Translation((x,y,.05))@rot@o.matrix_world
  if idx%2==0:skull((x-.05,y-.30,.19),.20,fire)
 for yy,r in [(-4.40,.43),(-.20,.29),(3.50,.32)]:
  before=len(PARTS);pumpkin(r*.80,r,skin,fire)
  for o in PARTS[before:]:o.location+=Vector((side*5.92,yy,0))
  if r>.4:
   before=len(PARTS);pumpkin(.17,.20,skin,fire)
   for o in PARTS[before:]:o.location+=Vector((side*6.40,yy+.05,0))
# Skull pile on front center crypt; front walk still leaves board clear.
box('Ossuary base',(0,-5.12,.10),(1.20,.50,.20),stone,.04)
skull((-.32,-5.12,.43),.29,fire);skull((.27,-5.10,.39),.24,fire)
for sx in [-1,1]:
 rod('Crossed bone',(sx*.43,-5.32,.19),(-sx*.45,-5.24,.26),.035,bone,n=8)
# Cobwebs on portal shoulders, rear wall and front corner ironwork.
for cx,cy,cz,radius in [(-2.6,5.5,.4,.8),(2.6,5.5,.4,.8),(-5.85,-5.45,.35,.6),(5.85,-5.45,.35,.6)]:
 origin=Vector((cx,cy,cz));dirs=[Vector((cos(a),0,sin(a))) for a in [0,pi/6,pi/3,pi/2,2*pi/3,5*pi/6,pi]]
 for d in dirs:rod('Cobweb spoke',origin,origin+d*radius,.008,webmat,n=5)
 for r in [.30*radius,.63*radius,.95*radius]:
  for a,b in zip(dirs,dirs[1:]):
   aa=origin+a*r;bb=origin+b*r;mid=(aa+bb)/2-Vector((0,0,.06))
   rod('Sagging spider silk',aa,mid,.006,webmat,n=5);rod('Sagging spider silk',mid,bb,.006,webmat,n=5)
# Narrow floor cracks only on outer walk, never on chess grid or slots.
for side in [-1,1]:
 for y in [-2.7,-.7,1.5]:
  pts=[(side*5.50,y,.043),(side*5.66,y+.18,.043),(side*5.55,y+.38,.043),(side*5.78,y+.59,.043)]
  for a,b in zip(pts,pts[1:]):rod('Soul fissure',a,b,.009,mint,n=5)
finish('Arena_Decor_Props')
scene['style']='Dread cemetery: ruined Gothic gate, skulls, bare trees, iron spears, graves, rotten pumpkins'
scene['layout_policy']='Original board, floor, island and 12 side slots preserved; perimeter architecture redesigned'
print('REDESIGNED',[(o.name,len(o.data.polygons)) for o in scene.objects if o.type=='MESH'])
