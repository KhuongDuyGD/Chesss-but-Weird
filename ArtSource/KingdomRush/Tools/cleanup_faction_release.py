"""Targeted release corrections, called by refine_faction_direction.py before export.
All coordinates here are authoring-space Blender XY, Z up. Does not touch a board.
"""
import math
import bpy
from mathutils import Vector

TOWER_PARTS=('Dressed round foundation','Smooth carved tower','Masonry string course',
    'Fine mortar joint','Sculpted roof collar','Azure slate spire','Roof shingle course',
    'Gilded finial','Fractured standing pillar remnant','Curved crown talon','Soul brazier rim')
ARCH_PARTS=('Dressed arch voussoir','Fallen arch voussoir','Portal-bound ember')
FORT_PARTS=('Citadel terrace','Citadel dressed keep','Keep masonry band','High royal slate roof')
TREE_PARTS=('Organic bark trunk','Tapered twisting branch','Tree root flare')

def cleanup_structures(scene,K,bounds,remove,evil):
    removed={};kept=[]
    # Four inner courtyard columns are the only standing architectural sites retained.
    for o in list(scene.objects):
        if o.type!='MESH':continue
        center,_=bounds(o);x,y=center.x,center.y
        tower=o.name.startswith(TOWER_PARTS)
        distant=max(abs(x),abs(y))>18
        delete=o.name.startswith(ARCH_PARTS+FORT_PARTS)
        delete|=tower and (abs(x)>9 or abs(y)>10)
        delete|=distant and o.name.startswith(('Jagged shattered sanctuary wall','Fallen sanctuary block'))
        # Fallen duplicate tower sections outside the four courtyard sites are also discarded.
        if evil and o.name.startswith('Fractured standing pillar remnant'):
            delete|=not (abs(abs(x)-7.65)<.25 and abs(abs(y)-8)<.25)
        if delete:
            prefix=o.name.split('.')[0];removed[prefix]=removed.get(prefix,0)+1;remove(o)
        elif o.name.startswith(('Smooth carved tower','Fractured standing pillar remnant')):
            kept.append([round(x,3),round(y,3)])
    if evil:
        # These complete tree groups intrude on the luminous trench in the marked views.
        trees=[(-10,-6),(10,6),(-10,6),(10,-6)]
        for o in list(scene.objects):
            if o.type!='MESH' or not o.name.startswith(TREE_PARTS):continue
            center,_=bounds(o)
            if any(math.hypot(center.x-x,center.y-y)<1.65 for x,y in trees):
                prefix=o.name.split('.')[0];removed[prefix]=removed.get(prefix,0)+1;remove(o)
    assert len(kept)==4,('Expected precisely four inner columns',kept)
    return {'removedParts':removed,'retainedInnerColumnCenters':kept}

def carve_linirea_waterways(scene,K,remove):
    """Cut terrain polygons to the existing river edges instead of hiding land under water.
    Row boundaries include every river sample, making the shore match its piecewise geometry.
    The original terrain elevation is bilinearly sampled and the bank meets the water cleanly.
    """
    terrain=next(o for o in scene.objects if o.name.startswith('Detailed continuous sculpted terrain'))
    old=terrain.data;world=terrain.matrix_world.copy()
    samples={(round((world@v.co).x,4),round((world@v.co).y,4)):(world@v.co).z for v in old.vertices}
    step=.8
    def old_height(x,y):
        i=min(159,max(0,int((x+64)/step)));j=min(159,max(0,int((y+64)/step)))
        ax=-64+i*step;ay=-64+j*step;u=(x-ax)/step;v=(y-ay)/step
        z=lambda a,b:samples[(round(a,4),round(b,4))]
        return (z(ax,ay)*(1-u)+z(ax+step,ay)*u)*(1-v)+(z(ax,ay+step)*(1-u)+z(ax+step,ay+step)*u)*v
    rivers=[]
    for o in scene.objects:
        if not o.name.startswith('Clear rippled stream'):continue
        rows={}
        for v in o.data.vertices:
            p=o.matrix_world@v.co;rows.setdefault(round(p.y,4),[]).append(p)
        data=[]
        for y,pts in sorted(rows.items()):
            data.append((y,min(p.x for p in pts),max(p.x for p in pts),sum(p.z for p in pts)/len(pts)))
        rivers.append(data)
    rivers.sort(key=lambda r:r[0][1]);start=rivers[0][0][0];end=rivers[0][-1][0]
    def water(row,y):
        index=min(len(row)-2,max(0,int((y-start)/.6)))
        a,b=row[index],row[index+1];t=min(1,max(0,(y-a[0])/(b[0]-a[0])))
        return tuple(a[k]*(1-t)+b[k]*t for k in range(1,4))
    ys=sorted(set([round(-64+i*.8,4) for i in range(161)]+[r[0] for r in rivers[0]]))
    verts=[];faces=[];lookup={}
    def vertex(p):
        x,y=p;z=old_height(x,y)
        # A short, softly graded bank ends just below the existing opaque river surface.
        for row in rivers:
            left,right,wz=water(row,y);edge=min(abs(x-left),abs(x-right))
            if start-1e-5<=y<=end+1e-5 and edge<.65:
                t=edge/.65;t=t*t*(3-2*t);z=(wz-.025)*(1-t)+z*t
        key=(round(x,5),round(y,5))
        if key not in lookup:lookup[key]=len(verts);verts.append((x,y,z))
        return lookup[key]
    def clip(poly,bound0,bound1,y0,y1,keep_right):
        def d(p):
            edge=bound0+(bound1-bound0)*(p[1]-y0)/(y1-y0)
            return (p[0]-edge)*(1 if keep_right else -1)
        result=[]
        for a,b in zip(poly,poly[1:]+poly[:1]):
            da,db=d(a),d(b)
            if da>=-1e-7:result.append(a)
            if (da<0<db) or (db<0<da):
                t=da/(da-db);result.append((a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t))
        return result
    for y0,y1 in zip(ys,ys[1:]):
        wet=start<=(y0+y1)/2<=end
        if wet:
            a0,b0=water(rivers[0],y0),water(rivers[1],y0)
            a1,b1=water(rivers[0],y1),water(rivers[1],y1)
            regions=[((-64,-64),(a0[0],a1[0])),((a0[1],a1[1]),(b0[0],b1[0])),((b0[1],b1[1]),(64,64))]
        else:regions=[((-64,-64),(64,64))]
        for i in range(160):
            x0=-64+i*.8;x1=x0+.8
            for left,right in regions:
                if x1<min(left)-1e-6 or x0>max(right)+1e-6:continue
                poly=[(x0,y0),(x1,y0),(x1,y1),(x0,y1)]
                poly=clip(poly,*left,y0,y1,True)
                if len(poly)<3:continue
                poly=clip(poly,*right,y0,y1,False)
                ids=list(dict.fromkeys(vertex(p) for p in poly))
                if len(ids)<3:continue
                area=sum(verts[ids[k]][0]*verts[ids[(k+1)%len(ids)]][1]-verts[ids[(k+1)%len(ids)]][0]*verts[ids[k]][1] for k in range(len(ids)))
                if area>1e-8:faces.append(ids)
    remove(terrain)
    terrain=K.mesh('Detailed continuous sculpted terrain',verts,faces,'Meadow',True)
    # The caller's existing whole-terrain UV conversion expects the original half-unit UV scale.
    K.uv([terrain])
    for poly in terrain.data.polygons:
        for index in poly.loop_indices:
            p=terrain.data.vertices[terrain.data.loops[index].vertex_index].co
            terrain.data.uv_layers.active.data[index].uv=(p.x*.5+.5,p.y*.5+.5)
    return {'cutWaterways':len(rivers),'terrainVertices':len(verts),'terrainPolygons':len(faces),
        'bankWaterClearance':.025,'waterRange':[start,end]}

def apply(scene,K,bounds,remove,evil):
    bpy.context.view_layer.update()
    result=cleanup_structures(scene,K,bounds,remove,evil)
    if not evil:result['shoreline']=carve_linirea_waterways(scene,K,remove)
    return result
