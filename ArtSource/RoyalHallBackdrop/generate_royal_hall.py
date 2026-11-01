"""Original Royal Ascension hall. Blender-only geometry/materials; no external assets.
Run Blender --background --factory-startup --python this_file -- --blend <source.blend> --image <render.png>.
"""
import bpy,math,random,sys,argparse
from mathutils import Vector
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--blend',required=True);p.add_argument('--image',required=True);a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);random.seed(74)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
S=bpy.context.scene
try:S.render.engine='BLENDER_EEVEE_NEXT'
except TypeError:S.render.engine='BLENDER_EEVEE'
S.eevee.shadow_pool_size='1024'
S.render.resolution_x=1920;S.render.resolution_y=1080;S.render.resolution_percentage=100
S.render.image_settings.file_format='PNG';S.render.image_settings.color_mode='RGB';S.render.filepath=str(Path(a.image).resolve())
S.world.color=(.015,.018,.04);S.world.use_nodes=True;S.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.015,.022,.055,1);S.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.25
S.view_settings.view_transform='AgX';S.view_settings.exposure=.45
try:S.view_settings.look='AgX - Medium High Contrast'
except TypeError:pass

def mat(name,color,metal=0,rough=.5,emit=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;n=m.node_tree.nodes.get('Principled BSDF');n.inputs['Base Color'].default_value=(*color,1);n.inputs['Metallic'].default_value=metal;n.inputs['Roughness'].default_value=rough
 if emit:n.inputs['Emission Color'].default_value=(*color,1);n.inputs['Emission Strength'].default_value=emit
 return m
navy=mat('Midnight ink stone',(.025,.038,.09),.18,.64);violet=mat('Royal violet velvet',(.09,.025,.17),0,.91);gold=mat('Aged restrained gold',(.48,.28,.085),.74,.34);shadow=mat('Obsidian blue',(.012,.02,.05),.2,.34);ivory=mat('Blue moonlit alabaster',(.18,.23,.33),.12,.46);glow=mat('Quiet candle gold',(.9,.48,.12),.1,.4,3);glass=mat('Distant violet window',(.16,.12,.38),.3,.45,.3)
# Fine original procedural stone grain is baked by the image render, never expected to transfer into Unity shaders.
for m in [navy,ivory]:
 n=m.node_tree.nodes;links=m.node_tree.links;noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=18;noise.inputs['Detail'].default_value=2;bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.15;bump.inputs['Distance'].default_value=.055;links.new(noise.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs['Normal'],n['Principled BSDF'].inputs['Normal'])

def cube(name,loc,scale,m,bevel=0):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name;o.scale=scale;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(m)
 if bevel:b=o.modifiers.new('Soft carved edges','BEVEL');b.width=bevel;b.segments=2;o.modifiers.new('Weighted corners','WEIGHTED_NORMAL')
 return o

def cyl(name,loc,radius,depth,m,vertices=16,radius2=None):
 if radius2 is None:bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=loc)
 else:bpy.ops.mesh.primitive_cone_add(vertices=vertices,radius1=radius,radius2=radius2,depth=depth,location=loc)
 o=bpy.context.object;o.name=name;o.data.materials.append(m);b=o.modifiers.new('Cut edge','BEVEL');b.width=.035;b.segments=2;o.modifiers.new('Weighted facets','WEIGHTED_NORMAL');return o

def line(name,points,r,m):
 d=bpy.data.curves.new(name,'CURVE');d.dimensions='3D';d.resolution_u=2;d.bevel_depth=r;d.bevel_resolution=3;sp=d.splines.new('POLY');sp.points.add(len(points)-1)
 for v,co in zip(sp.points,points):v.co=(*co,1)
 o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.data.materials.append(m);return o

def area(name,loc,energy,color,size,target):
 d=bpy.data.lights.new(name,'AREA');d.energy=energy;d.use_shadow=not name.startswith('Warm wall glint');d.color=color;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
# One chess floor mesh rather than hundreds of loose props.
verts=[];faces=[];inds=[]
for i in range(14):
 for j in range(31):
  x=-9.1+i*1.3;y=-10+j*1.3;k=len(verts);verts += [(x,y,0),(x+1.29,y,0),(x+1.29,y+1.29,0),(x,y+1.29,0)];faces.append((k,k+1,k+2,k+3));inds.append((i+j)%2)
d=bpy.data.meshes.new('Original chess tessellation');d.from_pydata(verts,[],faces);o=bpy.data.objects.new('Quiet reflective chess aisle',d);bpy.context.collection.objects.link(o);d.materials.append(navy);d.materials.append(shadow)
for f,i in zip(d.polygons,inds):f.material_index=i
cube('Floor foundation',(0,10,-.25),(19,43,.5),shadow)
for side in [-1,1]:
 cube('Side wall',(side*9.5,10,6),(1,43,12),navy)
 for z in [.6,4.2,10.8]:cube('Architectural gold line',(side*8.96,10,z),(.05,43,.055),gold)
 for y in [-5,2,9,16,23,30]:
  x=side*8
  cube('Square pedestal',(x,y,.38),(1.5,1.5,.72),shadow,.08)
  cyl('Gold plinth',(x,y,.88),.74,.2,gold)
  cyl('Royal fluted column',(x,y,3.1),.48,4.2,navy,12)
  for angle in range(0,360,60):
   t=math.radians(angle);cube('Fine capital flute',(x+.49*math.cos(t),y+.49*math.sin(t),3.1),(.045,.045,4.1),gold)
  cyl('Royal capital',(x,y,5.26),.75,.35,gold)
  cube('Velvet banner',(side*8.9,y+2.8,5.1),(.07,2.05,4.5),violet,.04)
  for yy in [y+1.82,y+3.78]:cube('Banner gilded edge',(side*8.84,yy,5.1),(.07,.025,4.45),gold)
  # Small original diamond/crown sigil in the banner plane.
  for dz in [-.32,.32]:
   q=cube('Royal banner diamond',(side*8.8,y+2.8,5.0+dz),(.08,.55,.55),gold,.02);q.rotation_euler.x=math.pi/4
  cube('Sconce bracket',(side*7.52,y,3.1),(.65,.3,.2),gold,.03)
  cyl('Candle',(side*7.27,y,3.45),.07,.5,ivory,8);cyl('Gentle flame',(side*7.27,y,3.75),.08,.25,glow,8,radius2=0)
  area('Warm wall glint',(side*7.0,y,4),55,(1,.57,.21),1,(side*8.8,y,5))
for y in [-5,2,9,16,23,30]:
 points=[(8*math.cos(t),y,5.1+7.2*math.sin(t)) for t in [math.pi*i/64 for i in range(65)]]
 line('Carved royal vault',points,.30,navy);line('Fine gold arch',[(x,y-.27,z) for x,yy,z in points],.045,gold)
 for side in [-1,1]:line('Vault rib',[(side*6.2,y,9.6),(side*4.1,y+3.5,11.8),(0,y+7,12.2)],.07,gold)
# Rear wall and restrained circular light. No reward/item artwork is invented.
cube('Distant sanctum',(0,31,6),(19,.6,13),shadow)
for r in [3.5,3.68]:line('Sanctum window ring',[(r*math.cos(t),30.63,7+r*math.sin(t)) for t in [2*math.pi*i/96 for i in range(97)]],.055,gold)
for k in range(12):
 t=k*math.pi/6;line('Rose window original spokes',[(.8*math.cos(t),30.63,7+.8*math.sin(t)),(3.5*math.cos(t),30.63,7+3.5*math.sin(t))],.03,gold)
q=cyl('Violet glass disk',(0,30.7,7),3.45,.08,glass,64);q.rotation_euler.x=math.pi/2
for y in [0,10,22]:
 area('Moon blue nave',(0,y,10.4),1200,(.24,.34,1),7,(0,y+2,0))
 area('Violet opposing bounce',(7,y,8),700,(.5,.22,.95),5,(-4,y,4))
 area('Alabaster edge',(-7,y,7),650,(.38,.52,1),4,(-8,y,3))
area('Distant moon',(0,30,8),1800,(.24,.28,1),5,(0,8,4))
# Two low, peripheral ceremonial chess sentinels; purely environmental forms.
for side in [-1,1]:
 x=side*6.7;y=-1.8
 cube('Ceremonial side podium',(x,y,.65),(1.8,1.8,1.3),navy,.08)
 cyl('Sentinel foot',(x,y,1.4),.67,.3,gold);cyl('Sentinel taper',(x,y,2),.5,.95,ivory,12,radius2=.28);cyl('Sentinel neck',(x,y,2.58),.48,.24,gold)
 cyl('Rook parapet',(x,y,2.86),.57,.3,ivory,12)
 for n in range(6):
  t=n*math.pi/3;cube('Rook battlement',(x+.45*math.cos(t),y+.45*math.sin(t),3.12),(.24,.24,.3),gold,.025)
# Atmosphere, intentionally very thin so the reveal remains the subject.
vol=bpy.data.materials.new('Thin midnight air');vol.use_nodes=True;nodes=vol.node_tree.nodes;nodes.clear();out=nodes.new('ShaderNodeOutputMaterial');v=nodes.new('ShaderNodeVolumePrincipled');v.inputs['Density'].default_value=.005;v.inputs['Color'].default_value=(.18,.22,.4,1);v.inputs['Anisotropy'].default_value=.15;vol.node_tree.links.new(v.outputs['Volume'],out.inputs['Volume']);cube('Atmospheric depth',(0,10,6),(18,42,12),vol)
bpy.ops.object.camera_add(location=(0,-12.8,5.7));cam=bpy.context.object;cam.name='Original backdrop camera';cam.rotation_euler=(Vector((0,12,5.3))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=20;S.camera=cam
for dest in [a.blend,a.image]:Path(dest).resolve().parent.mkdir(parents=True,exist_ok=True)
S['asset_provenance']='Original geometry/materials authored for Chess But Weird. No external imagery, models, textures, fonts or generators.'
S['purpose']='Static external development-demo backdrop; not reward artwork, not a RenderTexture replacement.'
bpy.ops.wm.save_as_mainfile(filepath=str(Path(a.blend).resolve()));bpy.ops.render.render(write_still=True)
S.render.filepath='//../../Assets/CinematicDemo/FinalSprint/Art/RoyalHall.png';bpy.ops.wm.save_as_mainfile(filepath=str(Path(a.blend).resolve()))
print('CBW_ORIGINAL_BACKDROP_RENDER_COMPLETE',bpy.app.version_string,S.render.filepath)
