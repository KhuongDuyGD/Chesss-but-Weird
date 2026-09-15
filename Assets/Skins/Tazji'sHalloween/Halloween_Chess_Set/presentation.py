
import bpy, math
from mathutils import Vector
from pathlib import Path
scene=bpy.context.scene
stage=bpy.data.collections['02 • Presentation']
def stage_link(o):
 for c in list(o.users_collection):c.objects.unlink(o)
 stage.objects.link(o)
def smat(n,c,metal=0):
 m=bpy.data.materials.new(n);m.diffuse_color=(*c,1);m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*c,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=.5
 return m
stone=smat('Stage • Midnight slate',(.024,.028,.045))
edge=smat('Stage • Muted brass',(.24,.14,.05),.55)
letter=smat('Stage • Warm ivory',(.60,.55,.43))
def cube(n,loc,scale,m,b=.04):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=n;o.scale=scale
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 o.data.materials.append(m)
 mod=o.modifiers.new('Edge bevel','BEVEL');mod.width=b;mod.segments=3
 stage_link(o);return o
for row in range(2):
 z=row*.55
 cube('Display tier '+str(row),(0,row*2,z-.15),(8.15,1.65,.30),stone)
 cube('Tier trim '+str(row),(0,row*2-.816,z-.085),(7.98,.012,.027),edge,.004)
 for i,kind in enumerate(['Pawn','Rook','Knight','Bishop','Queen','King']):
  x=(i-2.5)*1.28
  o=bpy.data.objects['HW_'+['Ember','Specter'][row]+'_'+kind];o.location.z=z
  if kind=='Knight':o.rotation_euler.z=math.radians(-18)
  bpy.ops.object.text_add(location=(x,row*2-.61,z+.012),rotation=(0,0,0))
  t=bpy.context.object;t.name='Label '+str(row)+' '+kind;t.data.body=kind.upper();t.data.align_x='CENTER';t.data.size=.115;t.data.extrude=.0005;t.data.materials.append(letter);stage_link(t)
cube('Foundation',(0,1,-.35),(8.6,4.3,.20),stone)
cube('Ground',(0,0,-.51),(200,200,.10),smat('Stage • Background',(.009,.014,.024)),.01)
def txt(n,body,loc,size,m):
 bpy.ops.object.text_add(location=loc)
 o=bpy.context.object;o.name=n;o.data.body=body;o.data.align_x='CENTER';o.data.size=size;o.data.extrude=.001;o.data.materials.append(m);stage_link(o)
txt('Collection title','H A L L O W E E N',(0,-1.13,-.237),.28,letter)
txt('Collection subtitle','E M B E R   /   S P E C T E R',(0,3.00,-.237),.14,letter)
world=bpy.data.worlds.new('Halloween studio');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.06,.075,.12,1);world.node_tree.nodes['Background'].inputs[1].default_value=.35;scene.world=world
def light(n,loc,energy,color,size):
 data=bpy.data.lights.new(n,'AREA');data.energy=energy;data.color=color;data.shape='DISK';data.size=size
 o=bpy.data.objects.new(n,data);stage.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,1,.5))-o.location).to_track_quat('-Z','Y').to_euler()
light('Key • soft warm',(-4,-5,8),1400,(1,.80,.63),7)
light('Fill • moonlight',(4,-1,6),1100,(.53,.71,1),6)
light('Rim • spirit',(-1,6,7),1800,(.45,1,.81),5)
data=bpy.data.cameras.new('Presentation Camera');cam=bpy.data.objects.new('Presentation Camera',data);stage.objects.link(cam)
cam.location=(3,-11.5,9);cam.rotation_euler=(Vector((0,.85,.45))-cam.location).to_track_quat('-Z','Y').to_euler();data.type='ORTHO';data.ortho_scale=10.6;scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True
scene.render.resolution_x=1800;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG';scene.render.filepath='D:/Stuurdy/Blender/Halloween_Chess_Set/Halloween_Chess_Set_Preview.png'
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   area.spaces.active.region_3d.view_perspective='CAMERA'
   area.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath='D:/Stuurdy/Blender/Halloween_Chess_Set/Halloween_Chess_Set.blend')
print('STAGE READY')
