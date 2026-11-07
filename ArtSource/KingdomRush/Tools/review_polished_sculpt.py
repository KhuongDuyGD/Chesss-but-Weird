import bpy,numpy as np,math,sys,json
from pathlib import Path
from mathutils import Vector
SRC=Path(__file__).resolve().parents[1];OUT=SRC/'Polish3D';name=sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else '06_Quan_vua_Denas'
D=np.load(OUT/'Geometry'/(name+'_UV.npz'));V=D['vertices'].copy();V[:,2]-=V[:,2].min();V/=np.ptp(V[:,2]);V[:,:2]-=(V[:,:2].min(0)+V[:,:2].max(0))/2
bpy.ops.wm.read_factory_settings(use_empty=True);s=bpy.context.scene
mesh=bpy.data.meshes.new(name);mesh.from_pydata(V.tolist(),[],D['faces'].tolist());mesh.update();uv=mesh.uv_layers.new(name='UniqueSurfaceUV');uv.data.foreach_set('uv',D['uv'].reshape(-1).tolist())
o=bpy.data.objects.new(name,mesh);s.collection.objects.link(o)
for p in mesh.polygons:p.use_smooth=True
m=bpy.data.materials.new('KR_Art_'+name);m.use_nodes=True;bs=m.node_tree.nodes.get('Principled BSDF')
for suffix,socket in [('Albedo','Base Color'),('Metallic','Metallic'),('Roughness','Roughness')]:
    im=bpy.data.images.load(str(OUT/'Textures'/(name+'_'+suffix+'.png')));im.pack()
    if suffix!='Albedo':im.colorspace_settings.name='Non-Color'
    t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=im;t.extension='EXTEND';m.node_tree.links.new(t.outputs['Color'],bs.inputs[socket])
mesh.materials.append(m)
s.render.engine='CYCLES';s.cycles.device='CPU';s.cycles.samples=24;s.cycles.use_denoising=True;s.render.resolution_x=640;s.render.resolution_y=640;s.render.resolution_percentage=100
if '--gpu' in sys.argv:
    prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='OPTIX';prefs.get_devices()
    for device in prefs.devices:device.use=device.type=='OPTIX'
    s.cycles.device='GPU'
s.render.film_transparent=False;s.world=bpy.data.worlds.new('Neutral studio');s.world.use_nodes=True;s.world.node_tree.nodes.get('Background').inputs[0].default_value=(.28,.28,.28,1);s.world.node_tree.nodes.get('Background').inputs[1].default_value=.65
s.view_settings.view_transform='Standard';s.view_settings.look='None';s.view_settings.exposure=0
for label,loc,power,size in [('Key',(-2,-3,4),240,3),('Fill',(3,-1,2),120,3),('Rim',(0,3,3),200,2)]:
    d=bpy.data.lights.new(label,'AREA');d.energy=power;d.shape='DISK';d.size=size;l=bpy.data.objects.new(label,d);s.collection.objects.link(l);l.location=loc;l.rotation_euler=(Vector((0,0,.5))-l.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.cameras.new('Review camera');c=bpy.data.objects.new('Review camera',d);s.collection.objects.link(c);s.camera=c;d.type='ORTHO';d.ortho_scale=1.32
folder=OUT/'Reviews'/name;folder.mkdir(parents=True,exist_ok=True)
for i,(angle,elev) in enumerate([(0,8),(45,18),(90,8),(135,18),(180,8),(225,18),(270,8),(315,18),(0,65)]):
    a=math.radians(angle);e=math.radians(elev);c.location=(3*math.sin(a)*math.cos(e),-3*math.cos(a)*math.cos(e),.5+3*math.sin(e));c.rotation_euler=(Vector((0,0,.5))-c.location).to_track_quat('-Z','Y').to_euler();s.render.filepath=str(folder/f'{i:02d}.png');bpy.ops.render.render(write_still=True)
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Work'/(name+'_PBR_Review.blend')),compress=True)
print('MULTIANGLE_REVIEW_RENDERED',name,flush=True)
