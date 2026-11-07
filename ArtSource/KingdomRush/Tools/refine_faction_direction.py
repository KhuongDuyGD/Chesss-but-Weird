"""Blender: targeted, repeatable scene-direction pass from the preserved HQ sources.
Keeps all board/sculpt files and environment anchor positions. No full-scene regeneration.
"""
import bpy,bmesh,sys,math,random,json
from pathlib import Path
from mathutils import Vector
from math import sin,cos,pi
sys.path.insert(0,str(Path(__file__).resolve().parent))
import cleanup_faction_release
SRC=Path(__file__).resolve().parents[1]; ROOT=SRC.parents[1]
sys.path.insert(0,str(SRC));import build_kingdom_rush as K
REVIEW=ROOT/'Logs/KingdomRush/Authoring';REVIEW.mkdir(parents=True,exist_ok=True)
BEFORE=SRC/'AuthoringBase'
palette=json.loads((K.OUT/'faction_material_palette.json').read_text())
REPORT=[];spheres={}

def apply_meshes(obs):
    bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get()
    baked=[(o,bpy.data.meshes.new_from_object(o.evaluated_get(dg),preserve_all_data_layers=True,depsgraph=dg)) for o in obs if o.modifiers]
    for o,data in baked:o.modifiers.clear();o.data=data
K.apply_meshes=apply_meshes

def ground(x,y):
    ramp=min(1,max(0,(math.hypot(x,y)-8)/15))
    return -1.10+ramp*(.65+.6*sin(x*.19)*cos(y*.21)+.30*sin(y*.36+x*.17))
def bounds(o):
    pts=[o.matrix_world@Vector(p) for p in o.bound_box]
    lo=Vector([min(v[i] for v in pts) for i in range(3)])
    hi=Vector([max(v[i] for v in pts) for i in range(3)])
    return (lo+hi)/2,(hi-lo)/2
def remove(o):bpy.data.objects.remove(o,do_unlink=True)
def ell(name,p,s,m,canopy=False):
    key=(24,16) if canopy else (32,20)
    if key not in spheres:
        bm=bmesh.new();bmesh.ops.create_uvsphere(bm,u_segments=key[0],v_segments=key[1],radius=1)
        spheres[key]=bpy.data.meshes.new('Smooth sculpting template');bm.to_mesh(spheres[key]);bm.free()
    data=spheres[key].copy()
    for v in data.vertices:
        u=v.co.copy();organic=1+.035*sin(u.x*4.5+u.y*2.3)+.028*cos(u.y*6-u.z*2) if canopy else 1
        v.co=Vector((u.x*s[0]*organic,u.y*s[1]*organic,u.z*s[2]*organic))
        if canopy:v.co.z+=.035*s[2]*sin(u.x*5)*cos(u.y*4)
    o=bpy.data.objects.new(name,data);bpy.context.scene.collection.objects.link(o);o.location=p
    return K.finish(o,name,m,True)
def tube(name,pts,r,m,radii=None):return K.path(name,pts,r,m,radii)
def box(name,p,s,m,angle=0):
    o=K.box(name,p,s,m,.035);o.rotation_euler.z=angle;return o
def paint_materials(scene):
    mats={m.name:m for o in scene.objects if o.type=='MESH' for m in o.data.materials if m}
    K.M={}
    for spec in palette['materials']:
        m=mats.get(spec['name']) or bpy.data.materials.new(spec['name']);m.use_nodes=True
        nodes=m.node_tree.nodes;nodes.clear();out=nodes.new('ShaderNodeOutputMaterial');p=nodes.new('ShaderNodeBsdfPrincipled')
        m.node_tree.links.new(p.outputs['BSDF'],out.inputs['Surface'])
        p.inputs['Roughness'].default_value=spec['roughness'];p.inputs['Metallic'].default_value=spec['metallic']
        uv=nodes.new('ShaderNodeTexCoord');a=nodes.new('ShaderNodeTexImage')
        a.image=bpy.data.images.load(str(ROOT/spec['albedo']),check_existing=True)
        tint=nodes.new('ShaderNodeMixRGB');tint.blend_type='MULTIPLY';tint.inputs[0].default_value=1;tint.inputs[2].default_value=spec['color']
        m.node_tree.links.new(a.outputs['Color'],tint.inputs[1]);m.node_tree.links.new(tint.outputs[0],p.inputs['Base Color'])
        norm=nodes.new('ShaderNodeTexImage');norm.image=bpy.data.images.load(str(ROOT/spec['normal']),check_existing=True);norm.image.colorspace_settings.name='Non-Color'
        bump=nodes.new('ShaderNodeNormalMap');bump.inputs['Strength'].default_value=.16
        m.node_tree.links.new(norm.outputs['Color'],bump.inputs['Color']);m.node_tree.links.new(bump.outputs['Normal'],p.inputs['Normal'])
        if spec['emissionStrength']>0:
            p.inputs['Emission Color'].default_value=spec['emission'];p.inputs['Emission Strength'].default_value=spec['emissionStrength']
        K.M[spec['name'].removeprefix('KR HQ ')]=m

def stylize_living_canopies(scene):
    clouds=[o for o in scene.objects if o.type=='MESH' and o.name.startswith('Sculpted layered broadleaf canopy')]
    for i,o in enumerate(clouds):
        center,extent=bounds(o);remove(o)
        radius=max(extent.x,extent.y)
        # Smooth grouped masses, with readable lobe silhouettes rather than thousands of noisy leaves.
        ell('Fantasy canopy central mass',center,(radius*.63,radius*.55,extent.z*.70),'Deep leaf',True)
        for j in range(4):
            a=j*2*pi/4+.23*(i%4);p=center+Vector((cos(a)*radius*.46,sin(a)*radius*.38,(.15 if j%2 else -.12)*extent.z))
            ell('Fantasy canopy rounded cluster',p,(radius*.48,radius*.42,extent.z*.67),'New leaf' if j%3 else 'Leaf highlights',True)
        ell('Fantasy canopy sunward crown',center+Vector((-.1*radius,-.1*radius,extent.z*.40)),(radius*.48,radius*.45,extent.z*.49),'Leaf highlights',True)
    for o in list(scene.objects):
        if o.name.startswith('Canopy shaded inner growth'):remove(o)
    return len(clouds)

def fractured_column(p,r,height,seed):
    rng=random.Random(seed);n=64;phase=rng.random()*pi;v=[]
    for ring in range(3):
        for j in range(n):
            a=j*2*pi/n;h=height*(.83+.12*sin(3*a+phase)+.05*sin(7*a)) if ring==2 else ring*.16
            v.append((p[0]+cos(a)*r*(1-.04*ring),p[1]+sin(a)*r*(1-.04*ring),p[2]+h))
    faces=[]
    for ring in range(2):
        for j in range(n):q=ring*n+j;k=ring*n+(j+1)%n;faces.append((q,k,k+n,q+n))
    faces.append(tuple(reversed(range(n))));faces.append(tuple(range(2*n,3*n)))
    o=K.mesh('Fractured standing pillar remnant',v,faces,'Obsidian',True,.018)
    o.data.polygons[-1].use_smooth=False;return o

def skull_template():
    # Real concave eye sockets, softly bevelled jaw and separate teeth; no flat skull card.
    o=ell('Sculpted skull cranium',(0,0,0),(.34,.25,.31),'Bone ivory')
    for side in [-1,1]:
        cutter=ell('Temporary socket cutter',(side*.135,-.215,.045),(.105,.12,.125),'Bone recess')
        mod=o.modifiers.new('Carved eye socket','BOOLEAN');mod.operation='DIFFERENCE';mod.object=cutter
        bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name);remove(cutter)
    parts=[o,ell('Sculpted skull jaw',(0,-.012,-.235),(.25,.19,.095),'Bone ivory')]
    for side in [-1,1]:parts.append(ell('Recessed dark socket',(side*.135,-.145,.045),(.077,.055,.093),'Bone recess'))
    parts.append(ell('Skull nasal recess',(0,-.241,-.075),(.04,.024,.059),'Bone recess'))
    for i in range(6):parts.append(K.box('Rounded skull tooth',((i-2.5)*.06,-.188,-.19),(.054,.08,.09),'Bone ivory',.012))
    K.apply_meshes(parts);bpy.ops.object.select_all(action='DESELECT')
    for part in parts:part.select_set(True)
    bpy.context.view_layer.objects.active=o;bpy.ops.object.join();o.name='Sculpted ruin skull'
    return o
def place_skull(template,p,scale,angle):
    o=template.copy();o.data=template.data.copy();bpy.context.scene.collection.objects.link(o)
    o.name='Fallen sculpted skull';o.location=p;o.scale=(scale,)*3;o.rotation_euler=(-.82,.10,angle)
    K.PARTS.append(o);return o
def bone(a,b,r=.055):
    tube('Weathered long bone',[a,b],r,'Bone ivory')
    a,b=Vector(a),Vector(b);d=(b-a).normalized();across=d.cross(Vector((0,0,1)))
    if across.length<.1:across=Vector((1,0,0))
    across.normalize()
    for p in [a,b]:
        for side in [-1,1]:ell('Rounded bone epiphysis',p+across*r*.55,(r*.95,r*.95,r*1.1),'Bone ivory')
def rib_cage(x,y,z,angle,scale=1):
    def p(a,b,c):return (x+scale*(a*cos(angle)-b*sin(angle)),y+scale*(a*sin(angle)+b*cos(angle)),z+scale*c)
    tube('Exposed curved vertebral spine',[p(0,-.73,.68),p(0,0,.71),p(0,.75,.68)],.075*scale,'Bone ivory')
    for j in range(6):
        along=-.58+j*.23;width=.54*(1-.07*abs(j-2))
        for side in [-1,1]:
            pts=[p(side*width*sin(a),along,.08+.62*cos(a)) for a in [k*pi*.60/12 for k in range(13)]]
            tube('Curved exposed rib',pts,.041*scale,'Bone ivory',[1-k*.45/12 for k in range(13)])
    for j in range(7):ell('Rounded exposed vertebra',p(0,-.65+j*.21,.70),(.10*scale,.08*scale,.075*scale),'Bone ivory')

def ruin_story(scene):
    rng=random.Random(71107);removed=0;columns=[]
    for o in list(scene.objects):
        if o.type!='MESH':continue
        if o.name.startswith('Smooth carved tower'):
            center,extent=bounds(o);columns.append((center.x,center.y,center.z-extent.z,extent.x,extent.z*2));remove(o);removed+=1
    for o in list(scene.objects):
        if o.type!='MESH':continue
        if o.name.startswith(('Curved crown talon','Sculpted roof collar','Soul brazier rim','Masonry string course','Fine mortar joint','Keep masonry band','Portal-bound ember')):
            remove(o);removed+=1
        elif o.name.startswith('Dressed arch voussoir'):
            center,extent=bounds(o)
            if center.z>ground(center.x,center.y)+2.15 or rng.random()<.22:
                # Transfer the missing arch block to the same ruin's base instead of deleting its story.
                o.location.z-=center.z-ground(center.x,center.y)-.16;o.rotation_euler=(.28,rng.uniform(-.6,.6),rng.uniform(-1,1));o.name='Fallen arch voussoir';removed+=1
        elif o.name.startswith(('Citadel dressed keep','Broken sanctuary wall')):
            center,extent=bounds(o);base=center.z-extent.z;w=extent.x
            remove(o);removed+=1
            verts=[]
            heights=[.90,.76,.98,.50,.66,.29,.42,.23,.18]
            for depth in [-extent.y,extent.y]:
                verts.extend([(center.x-w,center.y+depth,base),(center.x+w,center.y+depth,base)])
                for i,h in enumerate(heights):verts.append((center.x+w-i*2*w/8,center.y+depth,base+extent.z*2*h))
            n=11;faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
            faces.extend([(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)])
            K.mesh('Jagged shattered sanctuary wall',verts,faces,'Obsidian',False,.032)
            for i in range(4):box('Fallen sanctuary block',(center.x+(i-1.5)*w*.65,center.y-extent.y-.35-rng.random()*.5,base+.13),(.38,.34,.26),'Ash rock',rng.uniform(-1,1))
    for i,(x,y,z,r,h) in enumerate(columns):
        fractured_column((x,y,z),r,h*(.42+.17*(i%3)),700+i)
        if h>3:
            o=fractured_column((x+.85,y+.4,z+.12),r*.86,1.2,800+i);o.rotation_euler.y=1.35
    skull=skull_template();clusters=[(-9,-3),(9,3),(-9,3),(9,-3),(-5,-10),(5,10)]
    for i,(x,y) in enumerate(clusters):
        z=ground(x,y);a=i*.83
        place_skull(skull,(x-.44,y-.70,z+.35),1.1,a)
        place_skull(skull,(x+.43,y-.60,z+.24),.72,a+.7)
        if i%2==0:rib_cage(x,y-.15,z+.08,a,.85)
        for j in range(3):
            px=x+(j-1)*.44;py=y-.9
            bone((px,py,z+.14),(px+.40*cos(a+j),py+.48*sin(a+j),z+.17),.045)
        # A fallen standard, bent pole and ragged physical fabric belong to each ruined sanctuary.
        tube('Bent fallen standard pole',[(x-.8,y+.65,z+.10),(x,y+.72,z+.18),(x+.9,y+.60,z+.13)],.035,'ironedge')
        v=[(x-.15,y+.70,z+.22),(x+.60,y+.70,z+.24),(x+.60,y+.15,z+.18),(x+.42,y+.27,z+.19),(x+.20,y+.07,z+.15),(x+.02,y+.27,z+.18),(x-.15,y+.16,z+.17)]
        cloth=K.mesh('Ragged fallen Dominion standard',v,[tuple(range(7))],'Tyrant violet',True,.008)
        solid=cloth.modifiers.new('Fabric thickness','SOLIDIFY');solid.thickness=.025
    remove(skull)
    return {'alteredOrRemovedIntactParts':removed,'fracturedTowerSites':len(columns),'boneStoryClusters':len(clusters),'ribCages':3,'skulls':12}

for faction in ([] if '--boards-only' in sys.argv else ['Denas','Veznan']):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    spheres={}
    source=BEFORE/('KR_'+faction+'_Environment.blend')
    with bpy.data.libraries.load(str(source),link=False) as (a,b):b.scenes=a.scenes
    scene=max(b.scenes,key=lambda s:sum(o.type=='MESH' for o in s.objects));bpy.context.window.scene=scene
    bpy.context.preferences.filepaths.save_version=0;K.PARTS=[];paint_materials(scene)
    result=stylize_living_canopies(scene) if faction=='Denas' else ruin_story(scene)
    cleanup=cleanup_faction_release.apply(scene,K,bounds,remove,faction=='Veznan')
    obs=[o for o in scene.objects if o.type=='MESH'];K.apply_meshes(obs)
    # Retain approved UVs on existing terrain/architecture. Generate UVs only on new objects.
    for o in obs:
        if not o.data.uv_layers:K.uv([o])
        if o.name.startswith('Detailed continuous sculpted terrain'):
            for loop in o.data.uv_layers.active.data:
                # A unique broad painted terrain map; no repeated garden-noise tile pattern.
                loop.uv=(loop.uv-Vector((.5,.5)))/64+Vector((.5,.5))
    bpy.context.view_layer.update();name='KR_'+faction+'_Environment'
    K.export_fbx(name,obs)
    for o in obs:
        for mat in o.data.materials:
            if mat and mat.use_nodes:
                for node in mat.node_tree.nodes:
                    if node.type=='TEX_IMAGE' and node.image and not node.image.packed_file:node.image.pack()
    K.save_blend(SRC/'Factions'/(name+'.blend'))
    stat={'asset':name,'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in obs),'meshParts':len(obs),'directionChanges':result,'releaseCleanup':cleanup}
    REPORT.append(stat);print('STYLE_ENVIRONMENT_DONE',json.dumps(stat),flush=True)
if REPORT:(REVIEW/'geometry_report.json').write_text(json.dumps(REPORT,indent=2))

# Keep the editable board surfaces consistent with Unity, without re-exporting board geometry.
for faction in ['Denas','Veznan']:
    bpy.ops.wm.read_factory_settings(use_empty=True);spheres={}
    source=BEFORE/('KR_'+faction+'_Board.blend')
    with bpy.data.libraries.load(str(source),link=False) as (a,b):b.scenes=a.scenes
    scene=max(b.scenes,key=lambda s:sum(o.type=='MESH' for o in s.objects));bpy.context.window.scene=scene
    paint_materials(scene)
    for o in scene.objects:
        if o.type!='MESH':continue
        for mat in o.data.materials:
            if mat and mat.use_nodes:
                for node in mat.node_tree.nodes:
                    if node.type=='TEX_IMAGE' and node.image and not node.image.packed_file:node.image.pack()
    K.save_blend(SRC/'Factions'/('KR_'+faction+'_Board.blend'))
    print('STYLE_BOARD_SURFACES_PACKED',faction,flush=True)
