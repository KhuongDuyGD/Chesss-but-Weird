"""Offline geometry/frustum checks and Blender previews. Never launches Unity or play mode."""
import bpy,json,sys,math,hashlib
from pathlib import Path
from mathutils import Vector
SRC=Path(__file__).resolve().parents[1];ROOT=SRC.parents[1]
sys.path.insert(0,str(SRC));import build_kingdom_rush as K
sys.path.insert(0,str(Path(__file__).resolve().parent));import cleanup_faction_release as C
output=next((a.split('=',1)[1] for a in sys.argv if a.startswith('--output=')),None)
OUT=Path(output) if output else ROOT/'Logs/KingdomRush/ReleaseChecks'
OUT.mkdir(parents=True,exist_ok=True);checks=[]
def check(label,ok,detail=None):
    checks.append({'check':label,'pass':bool(ok),'detail':detail})
    if not ok:raise AssertionError((label,detail))
def scene_from(path):
    with bpy.data.libraries.load(str(path)) as (a,b):b.scenes=a.scenes
    return max(b.scenes,key=lambda s:sum(o.type=='MESH' for o in s.objects))
def bounds(o):
    pts=[o.matrix_world@Vector(p) for p in o.bound_box]
    return Vector([(min(p[i] for p in pts)+max(p[i] for p in pts))/2 for i in range(3)])

baseline=json.loads((SRC/'asset_protection_hashes.json').read_text(encoding='utf-8-sig'))
for item in baseline['files']:
    path=ROOT/item['path'];digest=hashlib.sha256(path.read_bytes()).hexdigest().upper()
    check('Protected model/paint unchanged: '+path.name,digest==item['sha256'])
for faction in ['Denas','Veznan']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=scene_from(SRC/'Factions'/('KR_'+faction+'_Environment.blend'));bpy.context.window.scene=scene
    bpy.context.view_layer.update();obs=[o for o in scene.objects if o.type=='MESH']
    columns=[o for o in obs if o.name.startswith(('Smooth carved tower','Fractured standing pillar remnant'))]
    check(faction+' has exactly four courtyard columns',len(columns)==4)
    check(faction+' retains correct inner-column sites',all(abs(abs(bounds(o).x)-7.65)<.001 and abs(abs(bounds(o).y)-8)<.001 for o in columns))
    check(faction+' has no arches or outer fortresses',not any(o.name.startswith(C.ARCH_PARTS+C.FORT_PARTS) for o in obs))
    if faction=='Denas':
        terrain=next(o for o in obs if o.name.startswith('Detailed continuous sculpted terrain'))
        rivers=[]
        for o in obs:
            if not o.name.startswith('Clear rippled stream'):continue
            rows={}
            for v in o.data.vertices:
                p=o.matrix_world@v.co;rows.setdefault(round(p.y,4),[]).append(p)
            rivers.append([(y,min(p.x for p in pts),max(p.x for p in pts),sum(p.z for p in pts)/len(pts)) for y,pts in sorted(rows.items())])
        def water(row,y):
            i=min(len(row)-2,max(0,int((y-row[0][0])/.6)));a,b=row[i],row[i+1];t=(y-a[0])/(b[0]-a[0])
            return [a[k]*(1-t)+b[k]*t for k in range(1,4)]
        intrusions=0;bank_count=0;bad_heights=0;bad_uv=0
        for poly in terrain.data.polygons:
            points=[terrain.matrix_world@terrain.data.vertices[i].co for i in poly.vertices]
            center=sum(points,Vector())/len(points)
            if rivers[0][0][0]<center.y<rivers[0][-1][0]:
                for row in rivers:
                    lo,hi,z=water(row,center.y)
                    if lo+1e-5<center.x<hi-1e-5:intrusions+=1
        for v in terrain.data.vertices:
            p=terrain.matrix_world@v.co
            if rivers[0][0][0]<=p.y<=rivers[0][-1][0]:
                for row in rivers:
                    lo,hi,z=water(row,p.y)
                    if min(abs(p.x-lo),abs(p.x-hi))<2e-5:
                        bank_count+=1;bad_heights+=abs(p.z-(z-.025))>2e-5
        for poly in terrain.data.polygons:
            for index in poly.loop_indices:
                p=terrain.data.vertices[terrain.data.loops[index].vertex_index].co
                uv=terrain.data.uv_layers.active.data[index].uv
                bad_uv+=(uv-Vector((p.x/128+.5,p.y/128+.5))).length>1e-5
        check('No Linirea terrain polygons remain inside either stream',intrusions==0,intrusions)
        check('Every river-bank vertex is below the matching water edge',bank_count>500 and bad_heights==0,{'bankVertices':bank_count,'badHeights':bad_heights})
        check('Terrain uses continuous world-aligned macro UVs',bad_uv==0,bad_uv)
    else:
        trees=[(-10,-6),(10,6),(-10,6),(10,-6)]
        check('Marked Veznan trench trees completely removed',not any(o.name.startswith(C.TREE_PARTS) and any(math.hypot(bounds(o).x-x,bounds(o).y-y)<1.65 for x,y in trees) for o in obs))
        check('All twelve ruin skulls preserved',sum(o.name.startswith('Fallen sculpted skull') for o in obs)==12)
    board=scene_from(SRC/'Factions'/('KR_'+faction+'_Board.blend'))
    for o in list(board.objects):
        if o.type=='MESH':scene.collection.objects.link(o)
    bpy.context.window.scene=scene
    for o in list(scene.objects):
        if o.type in ['LIGHT','CAMERA']:bpy.data.objects.remove(o,do_unlink=True)
    K.render_settings(scene,1280,960);scene.cycles.samples=24
    K.light(scene,'Soft review sun',(-10,-14,27),3200,(1,.93,.82) if faction=='Denas' else (.78,.82,1),9)
    K.light(scene,'Broad review fill',(13,9,20),1800,(.86,.92,1) if faction=='Denas' else (.69,.79,1),18)
    for angle,p in ([] if '--no-render' in sys.argv else [('Top',(0,-.01,40)),('Orbit',(14,-25,27))]):
        K.camera(scene,p,(0,0,0),34,'Release review '+angle)
        scene.render.filepath=str(OUT/(faction+'_'+angle+'_Blender.png'));bpy.ops.render.render(write_still=True)

# A mathematical sweep of the complete near rectangle, not a Unity gameplay test.
# The old fixed 10-tile distance must reproduce clipping; the corrected distance
# must keep all corners above a 2.75-tile terrain envelope without changing X/Y.
frustum_cases=0;old_clips=0;max_dolly=0
for aspect in [16/9,4/3,21/9,9/16,32/9]:
    for pitch in [25,30,40,50,65,85]:
        a=math.radians(pitch);s,c=math.sin(a),math.cos(a)
        for yaw in range(0,360,15):
            q=math.radians(yaw);right=Vector((math.cos(q),-math.sin(q),0))
            forward=Vector((math.sin(q)*c,math.cos(q)*c,-s));up=right.cross(forward).normalized()
            # Board fit includes decorative extents and HUD fraction used by runtime.
            corners=[Vector((x,y,z)) for x in [-4.8,4.8] for y in [-5.9,5.9] for z in [-.9,.9]]
            h=max(abs(p.dot(up)) for p in corners);w=max(abs(p.dot(right)) for p in corners)
            for zoom in [.55,1,1.65]:
                size=max(h,w/aspect)*1.12*zoom/.76;distance=max(10,.3+(size*abs(up.z)+2)/s)
                for sx in [-1,1]:
                    for sy in [-1,1]:
                        offset=right*sx*size*aspect+up*sy*size
                        oldz=.75+(10-.3)*s+offset.z
                        newz=.75+(.3-distance)*forward.z+offset.z
                        old_clips+=oldz<0
                        check_ok=newz>=2.75-1e-5
                        if not check_ok:raise AssertionError(('Near rectangle clips ground',aspect,pitch,yaw,zoom,newz))
                frustum_cases+=1;max_dolly=max(max_dolly,distance)
check('Old camera distance reproduces near-ground clipping',old_clips>0,old_clips)
check('Full camera near rectangle clears terrain across orbit/zoom/aspects',True,{'frustumCases':frustum_cases,'maxDistanceInTiles':max_dolly})
(OUT/'offline_checks.json').write_text(json.dumps({'scope':'Offline authoring and frustum checks; no Unity E2E','pass':len(checks),'fail':0,'checks':checks},indent=2))
print('RELEASE_CLEANUP_OFFLINE_PASS',len(checks),flush=True)
