
import bpy, math, bmesh, json
from mathutils import Vector
from pathlib import Path
from math import sin, cos, pi
OUT=Path('D:/Stuurdy/Blender/Halloween_Chess_Set')
scene=bpy.data.scenes.new('Halloween • Twelve Originals')
bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1.0
pieces=bpy.data.collections.new('01 • Halloween Pieces • 12 Masters')
scene.collection.children.link(pieces)
stage=bpy.data.collections.new('02 • Presentation')
scene.collection.children.link(stage)
def mat(name,color,metal=0,rough=.4,em=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
 if em: bs.inputs['Emission Color'].default_value=(*color,1);bs.inputs['Emission Strength'].default_value=em
 return m
obsidian=mat('HW • Obsidian',(.026,.018,.05),.35)
bone=mat('HW • Old Ivory',(.76,.63,.40))
black=mat('HW • Carved Shadows',(.007,.003,.012))
gold=mat('HW • Antique Brass',(.46,.23,.055),.7)
stem=mat('HW • Dry Stems',(.11,.09,.026))
palettes=[
 ('Ember',mat('HW • Ember Pumpkin',(.72,.16,.022)),mat('HW • Ember Cloak',(.15,.028,.10)),mat('HW • Amber Fire',(1,.39,.025),em=2),mat('HW • Ember Trim',(.85,.36,.065),.55)),
 ('Specter',mat('HW • Specter Pumpkin',(.10,.48,.37)),mat('HW • Specter Cloak',(.055,.095,.22)),mat('HW • Ghost Fire',(.16,1,.66),em=2),mat('HW • Specter Trim',(.20,.67,.54),.55))]
PARTS=[]
def register(o,m):
 o.data.materials.append(m);PARTS.append(o);return o
def mesh(name,vs,fs,m):
 me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.update()
 o=bpy.data.objects.new(name,me);scene.collection.objects.link(o);return register(o,m)
def lathe(name,profile,m,n=24):
 vs=[(r*cos(2*pi*i/n),r*sin(2*pi*i/n),z) for r,z in profile for i in range(n)]
 fs=[]
 for j in range(len(profile)-1):
  for i in range(n): a=j*n+i;b=j*n+(i+1)%n;fs.append((a,b,b+n,a+n))
 fs += [tuple(reversed(range(n))),tuple((len(profile)-1)*n+i for i in range(n))]
 return mesh(name,vs,fs,m)
def orb(name,loc,scale,m,seg=16,rings=10):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=seg,ring_count=rings,location=loc)
 o=bpy.context.object;o.name=name;o.scale=scale;return register(o,m)
def box(name,loc,scale,m,bevel=.02):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name;o.scale=scale
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  mod=o.modifiers.new('Soft edges','BEVEL');mod.width=bevel;mod.segments=1
  bpy.ops.object.modifier_apply(modifier=mod.name)
 return register(o,m)
def rod(name,a,b,r,m,r2=None,n=10):
 d=Vector(b)-Vector(a)
 bpy.ops.mesh.primitive_cone_add(vertices=n,radius1=r,radius2=r if r2 is None else r2,depth=d.length,location=(Vector(a)+Vector(b))/2)
 o=bpy.context.object;o.name=name;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();return register(o,m)
def shape(name,coords,depth,m,y=0):
 # Extruded silhouette in XZ.
 vs=[(x,y+s*depth/2,z) for s in [-1,1] for x,z in coords];n=len(coords)
 fs=[tuple(reversed(range(n))),tuple(n+i for i in range(n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 return mesh(name,vs,fs,m)
def base(trim,cloak):
 lathe('Octagonal plinth',[(.30,0),(.36,.055),(.36,.11),(.32,.16),(.30,.20)],obsidian,16)
 lathe('Faction inlay',[(.362,.075),(.362,.11)],trim,16)
 lathe('Upper socket',[(.29,.20),(.28,.235),(.21,.29)],cloak,16)
def pumpkin(z,r,skin,fire):
 n=32;nr=12
 vs=[(0,0,z-r*.79)]
 for j in range(1,nr):
  ph=-pi/2+pi*j/nr
  for i in range(n):
   t=2*pi*i/n
   rr=r*cos(ph)*(1+.065*cos(8*t))
   vs.append((rr*cos(t),rr*sin(t),z+r*.80*sin(ph)))
 vs.append((0,0,z+r*.8));top=len(vs)-1
 fs=[(0,1+(i+1)%n,1+i) for i in range(n)]
 for j in range(nr-2):
  for i in range(n):
   a=1+j*n+i;b=1+j*n+(i+1)%n;fs.append((a,b,b+n,a+n))
 fs.extend((1+(nr-2)*n+i,1+(nr-2)*n+(i+1)%n,top) for i in range(n))
 mesh('Eight lobed pumpkin',vs,fs,skin)
 def face(coords,m,offset):
  # Raised inset face follows the pumpkin curvature.
  v=[]
  for depth in [offset,offset+.009]:
   for x,h in coords:
    ph=math.asin(max(-.98,min(.98,h/.80)))
    rr=cos(ph)
    yy=-math.sqrt(max(.01,rr*rr-x*x))
    t=math.atan2(yy,x)
    v.append((x*r,yy*r*(1+.065*cos(8*t))-depth,z+h*r))
  count=len(coords)
  f=[tuple(reversed(range(count))),tuple(count+i for i in range(count))]+[(i,(i+1)%count,(i+1)%count+count,i+count) for i in range(count)]
  mesh('Jack o lantern carving',v,f,m)
 eyes=[[(-.64,.02),(-.17,.04),(-.41,.40)],[(.17,.04),(.64,.02),(.41,.40)]]
 mouth=[(-.56,-.19),(-.31,-.23),(-.20,-.37),(-.08,-.24),(.09,-.25),(.21,-.37),(.32,-.23),(.56,-.19),(.34,-.51),(-.34,-.51)]
 for poly in eyes+[mouth]:
  face(poly,black,.009)
  cx=sum(x for x,h in poly)/len(poly);cz=sum(h for x,h in poly)/len(poly)
  face([(cx+(x-cx)*.73,cz+(h-cz)*.73) for x,h in poly],fire,.023)
 rod('Crooked pumpkin stem',(0,0,z+r*.73),(.065,.02,z+r*1.08),r*.105,stem,r*.065)
def crown(z,r,trim,fire,king=False):
 lathe('Crown band',[(r,z),(r*1.05,z+.07),(r*.96,z+.13)],trim,20)
 for i in range(6 if king else 5):
  t=2*pi*i/(6 if king else 5)
  x,y=r*cos(t),r*sin(t)
  rod('Crown point',(x,y,z+.06),(x*1.12,y*1.12,z+.30),.062,trim,.006,6)
  orb('Crown ember',(x*1.1,y*1.1,z+.27),(.037,.037,.046),fire,8,6)
 if king:
  rod('Royal cross stem',(0,0,z+.13),(0,0,z+.51),.035,trim,n=8)
  rod('Royal cross arms',(-.11,0,z+.40),(.11,0,z+.40),.032,trim,n=8)
def build(kind,pal):
 global PARTS
 PARTS=[]
 faction,skin,cloak,fire,trim=pal
 base(trim,cloak)
 if kind=='Pawn':
  lathe('Pawn pedestal',[(.20,.26),(.15,.33),(.125,.48),(.18,.53)],cloak)
  pumpkin(.72,.235,skin,fire)
 elif kind=='Rook':
  lathe('Haunted tower',[(.25,.26),(.23,.37),(.205,.77),(.28,.84),(.29,.94)],cloak,16)
  lathe('Tower belt',[(.239,.38),(.237,.42)],trim,16)
  for i in range(6):
   a=2*pi*i/6
   o=box('Battlement',(.235*cos(a),.235*sin(a),1.015),(.145,.14,.20),skin,.015);o.rotation_euler.z=a
  shape('Gothic window',[(-.075,.49),(.075,.49),(.075,.69),(0,.78),(-.075,.69)],.018,black,y=-.216)
  shape('Haunted window light',[(-.044,.53),(.044,.53),(.044,.67),(0,.725),(-.044,.67)],.012,fire,y=-.229)
  rod('Window mullion',(0,-.242,.52),(0,-.242,.72),.014,trim,n=6)
  lathe('Tower upper ledge',[(.29,.84),(.30,.88)],trim,16)
 elif kind=='Knight':
  lathe('Horse socket',[(.22,.27),(.20,.36),(.16,.42)],cloak,16)
  # Horse skull faces left in profile, toward viewer at a three-quarter angle.
  coords=[(-.19,.39),(.17,.39),(.19,.65),(.27,.93),(.18,1.10),(.10,1.14),(.065,1.32),(-.005,1.34),(-.045,1.16),(-.13,1.17),(-.34,1.04),(-.41,.88),(-.31,.82),(-.10,.87),(-.04,.77)]
  shape('Skeletal horse skull and neck',coords,.23,bone)
  for y in [-.125,.125]:
   orb('Hollow skull eye',(-.11,y,1.055),(.075,.018,.063),black,12,8)
   orb('Horse spirit eye',(-.13,y*1.10,1.06),(.033,.016,.029),fire,10,6)
   orb('Nostril',(-.34,y,.92),(.028,.016,.025),black,8,6)
  for i in range(4):
   box('Skull tooth',(-.29+i*.052,-.005,.842),(.032,.248,.07),bone,.005)
  for i in range(5):
   z=.47+i*.09
   rod('Neck rib',(-.09,0,z),(.17,0,z+.075),.028,trim,n=8)
  for i in range(5):
   z=.68+i*.08
   shape('Jagged mane',[(.15,z),(.32,z+.035),(.22,z+.14)],.17,cloak)
 elif kind=='Bishop':
  lathe('Witch robe',[(.25,.27),(.225,.34),(.15,.70),(.115,.85)],cloak,16)
  pumpkin(.97,.19,skin,fire)
  lathe('Witch hat brim',[(.34,1.10),(.345,1.145),(.235,1.17)],obsidian,20)
  # Bent hat with asymmetric rings.
  rings=[(0,1.15,.215),(.015,1.27,.17),(.035,1.40,.12),(.08,1.52,.075),(.18,1.56,.018)]
  vs=[(cx+r*cos(2*pi*i/16),r*sin(2*pi*i/16),z) for cx,z,r in rings for i in range(16)]
  fs=[tuple(reversed(range(16))),tuple(64+i for i in range(16))]+[(j*16+i,j*16+(i+1)%16,(j+1)*16+(i+1)%16,(j+1)*16+i) for j in range(4) for i in range(16)]
  mesh('Crooked witch hat',vs,fs,cloak)
  lathe('Hat ribbon',[(.213,1.17),(.192,1.235)],trim,16)
  box('Hat buckle',(.008,-.20,1.205),(.085,.035,.065),gold,.008)
  rod('Witch staff',(.26,0,.28),(.26,0,1.02),.023,stem,n=8)
  orb('Staff flame',(.26,0,1.075),(.06,.06,.115),fire,10,8)
 elif kind in ['Queen','King']:
  isking=kind=='King'
  lathe('Royal flared cloak',[(.27,.27),(.26,.34),(.205,.44),(.145,.91),(.22,1.04)],cloak,20)
  # Front lapels and clasp.
  shape('Royal lapel left',[(-.23,1.03),(-.05,.58),(0,.94)],.035,skin,y=-.16)
  shape('Royal lapel right',[(.23,1.03),(.05,.58),(0,.94)],.035,skin,y=-.16)
  orb('Royal brooch',(0,-.20,.91),(.064,.033,.078),fire,10,8)
  if isking:
   pumpkin(1.23,.265,skin,fire)
   crown(1.40,.21,trim,fire,True)
   # High vampire collar.
   for s in [-1,1]:
    shape('High royal collar',[(s*.10,.99),(s*.34,1.21),(s*.30,.92)],.11,obsidian,y=.08)
  else:
   pumpkin(1.16,.22,skin,fire)
   crown(1.32,.175,trim,fire)
   # Distinct scalloped bat wings rise behind the queen.
   for s in [-1,1]:
    shape('Bat wing',[(s*.12,.88),(s*.20,1.16),(s*.44,1.36),(s*.40,1.10),(s*.30,1.08),(s*.28,.96),(s*.20,.98)],.065,cloak,y=.095)
    rod('Wing leading bone',(s*.14,.045,.92),(s*.43,.045,1.34),.016,trim,n=6)
 # Bake geometry and join into one reusable mesh, pivot at base center.
 bpy.ops.object.select_all(action='DESELECT')
 for o in PARTS: o.select_set(True)
 bpy.context.view_layer.objects.active=PARTS[0]
 bpy.ops.object.convert(target='MESH')
 bpy.ops.object.join()
 o=bpy.context.object;o.name='HW_'+faction+'_'+kind
 scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
 bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
 for coll in list(o.users_collection): coll.objects.unlink(o)
 pieces.objects.link(o)
 o['faction']=faction;o['chess_role']=kind;o['forward']='-Y';o['pivot']='base center';o['style']='Halloween low-poly'
 return o
masters=[]
for p in palettes:
 for kind in ['Pawn','Rook','Knight','Bishop','Queen','King']:
  masters.append(build(kind,p))
for i,o in enumerate(masters):
 o.location=((i%6-2.5)*1.28, (i//6)*2.0,0)
print('CREATED',[(o.name,len(o.data.polygons)) for o in masters])
