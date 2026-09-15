
import bpy,ast,math,bmesh,json
from mathutils import Vector
from pathlib import Path
from math import sin,cos,pi
OUT=Path('D:/Stuurdy/Blender/Halloween_Chess_Arena')
scene=bpy.context.scene
# Reuse only modeling helper definitions, never scene creation from the chess set.
tree=ast.parse(Path('D:/Stuurdy/Blender/Halloween_Chess_Set/build_halloween.py').read_text(encoding='utf-8'))
defs=[n for n in tree.body if isinstance(n,ast.FunctionDef)]
exec(compile(ast.Module(body=defs,type_ignores=[]),'halloween_helpers','exec'))
PARTS=[]
colors={
'MAT_Board_Light':(.58,.42,.25),'MAT_Board_Dark':(.043,.020,.067),
'MAT_Arena_Stone':(.13,.115,.19),'MAT_Arena_Stone_Light':(.22,.19,.29),
'MAT_Arena_Stone_Shadow':(.047,.032,.077),'MAT_Arena_Cliff':(.034,.025,.055),
'MAT_Arena_Cliff_Light':(.084,.043,.11),'MAT_Arena_Antique_Gold':(.61,.25,.043),
'MAT_Arena_Indigo':(.068,.21,.19),'MAT_Arena_Wine':(.26,.032,.125),
'MAT_Arena_Walnut':(.085,.037,.017),'MAT_Arena_Wood_Light':(.23,.087,.032),
'MAT_Arena_Foliage':(.08,.20,.17),'MAT_Arena_Foliage_Light':(.17,.33,.24),
'MAT_Arena_Crystal_Cyan':(.14,.68,.41),'MAT_Arena_Crystal_Violet':(.43,.075,.55),
'MAT_Arena_Flame':(.22,.80,.48),'MAT_Arena_Flame_Core':(.62,1,.68),
'MAT_Arena_Soil':(.035,.018,.032)}
for m in list(bpy.data.materials):
 if m.name in colors:
  name=m.name;c=colors[name];m.name='HW_'+name
  m.diffuse_color=(*c,1);m.use_nodes=True
  bs=m.node_tree.nodes.get('Principled BSDF')
  bs.inputs['Base Color'].default_value=(*c,1);bs.inputs['Roughness'].default_value=.52
  if 'Flame' in name or 'Crystal' in name:
   bs.inputs['Emission Color'].default_value=(*c,1);bs.inputs['Emission Strength'].default_value=1.2 if 'Flame' in name else .25
skin=mat('HW_Arena_Pumpkin',(.78,.155,.018),rough=.49)
fire=mat('HW_Arena_Lantern_Fire',(1,.38,.028),em=2)
mint=mat('HW_Arena_Spirit',(.20,.88,.56),em=1.5)
black=mat('HW_Arena_Carving',(.005,.002,.012))
stem=mat('HW_Arena_Stem',(.12,.065,.016))
obsidian=mat('HW_Arena_Iron',(.02,.012,.04),metal=.6)
grave=mat('HW_Arena_Gravestone',(.25,.23,.32))
webmat=mat('HW_Arena_Spider_Silk',(.36,.56,.50),rough=.7)
def join_into(name):
 global PARTS
 target=scene.objects[name]
 bpy.ops.object.select_all(action='DESELECT')
 target.select_set(True)
 for o in PARTS:o.select_set(True)
 bpy.context.view_layer.objects.active=target
 bpy.ops.object.join()
 bm=bmesh.new();bm.from_mesh(target.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(target.data);bm.free()
 PARTS=[]
 return target
# Ground pumpkins occupy outer margins, clear of all 12 side slots.
for side in [-1,1]:
 for yy in [-4.62,4.65]:
  for dx,dy,r in [(0,0,.39),(.47,-.18,.21)]:
   before=len(PARTS);pumpkin(r*.80,r,skin,fire)
   for o in PARTS[before:]:o.location+=Vector((side*(5.85+dx),yy+dy,0))
# Gravestone stations on the rear walkway, outside the board rim.
for x in [-2.6,2.6]:
 box('Grave footing',(x,4.98,.075),(.85,.42,.15),obsidian,.035)
 before=len(PARTS)
 shape('Gothic gravestone',[(-.34,.13),(.34,.13),(.34,.84),(.22,1.04),(0,1.13),(-.22,1.04),(-.34,.84)],.20,grave)
 for o in PARTS[before:]:o.location+=Vector((x,5.03,0))
 rod('Engraved cross vertical',(x,4.917,.43),(x,4.917,.89),.025,obsidian,n=6)
 rod('Engraved cross horizontal',(x-.14,4.917,.73),(x+.14,4.917,.73),.022,obsidian,n=6)
 orb('Grave green seal',(x,4.91,.34),(.09,.017,.047),mint,10,6)
# Bats attached to rear-facing facade; solid extruded silhouettes.
bat=[(-.49,.13),(-.28,.08),(-.09,.17),(-.065,.27),(0,.21),(.065,.27),(.09,.17),(.28,.08),(.49,.13),(.32,-.10),(.23,-.015),(.13,-.12),(0,-.18),(-.13,-.12),(-.23,-.015),(-.32,-.10)]
for x,y,z,k in [(0,5.30,2.15,1.0),(-6.32,-3.10,2.20,.65),(6.30,2.85,2.35,.65)]:
 before=len(PARTS)
 shape('Bat crest',[(a*k,b*k) for a,b in bat],.035,obsidian)
 for o in PARTS[before:]:o.location+=Vector((x,y,z))
 for dx in [-.04,.04]:orb('Bat crest eye',(x+dx*k,y-.025,z+.13*k),(.016,.011,.016),fire,8,6)
# Webs strung on rear railing. No web enters playing or side-slot surfaces.
for cx in [-4.8,4.8]:
 origin=Vector((cx,5.34,.20))
 dirs=[Vector((cos(a),0,sin(a))) for a in [0,pi/6,pi/3,pi/2,2*pi/3,5*pi/6,pi]]
 for d in dirs:rod('Web radial',origin,origin+d*.73,.009,webmat,n=5)
 for radius in [.25,.48,.70]:
  for i in range(len(dirs)-1):
   a=origin+dirs[i]*radius;b=origin+dirs[i+1]*radius
   mid=(a+b)/2+Vector((0,0,-.045))
   rod('Web scallop',a,mid,.007,webmat,n=5)
   rod('Web scallop',mid,b,.007,webmat,n=5)
 orb('Spider',origin+Vector((0,-.025,.35)),(.055,.035,.08),obsidian,10,6)
# Small iron spikes on the two rear corner turrets.
for sx in [-1,1]:
 for dx in [-.28,.28]:
  rod('Gothic corner spike',(sx*6.1+dx,5.35,.90),(sx*6.1+dx,5.35,1.48),.06,obsidian,.003,8)
join_into('Arena_Decor_Props')
# Faction-colored slot frames, preserve original vertices and all face connectivity.
o=scene.objects['Arena_Floor_Inlays']
orange=mat('HW_Arena_Ember_Slot',(.85,.30,.043),metal=.5)
green=mat('HW_Arena_Specter_Slot',(.12,.60,.37),metal=.4)
o.data.materials.append(orange);oi=len(o.data.materials)-1
o.data.materials.append(green);gi=len(o.data.materials)-1
for f in o.data.polygons:
 c=f.center
 if 4.57<abs(c.x)<5.29 and -3.61<c.y<3.61 and c.z<.0261:
  f.material_index=oi if c.x<0 else gi
# Metadata makes compatibility intent explicit.
scene['reference_arena']='Chess_Arena/Chess_Arena_Source.blend'
scene['layout_policy']='Preserve Chess_Board, floor, inlays, architecture, island geometry exactly'
scene['side_slots_each']=6
scene['side_slot_outer_size']=[.70,.90]
scene['side_slot_inner_size']=[.59,.79]
scene['side_slot_center_x']=[-4.93,4.93]
scene['side_slot_centers_y']=[-3.15,-1.89,-.63,.63,1.89,3.15]
print('HALLOWEEN ARENA',[(o.name,len(o.data.polygons)) for o in scene.objects if o.type=='MESH'])
