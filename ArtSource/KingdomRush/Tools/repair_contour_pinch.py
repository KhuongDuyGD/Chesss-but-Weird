"""Separate touching contour fans by a subpixel gap; preserve all painted UVs."""
import numpy as np,json,sys
from pathlib import Path
SRC=Path(__file__).resolve().parents[1];name=sys.argv[1];path=SRC/'Reconstructions'/(name+'_textured_surface.npz');D=np.load(path)
vertices=D['vertices'].tolist();faces=D['faces'].copy();uv=D['uv'].copy();fit=json.loads((SRC/'Reconstructions'/(name+'_artwork_camera.json')).read_text());direction=np.array(fit['right'])
edges=np.sort(np.concatenate([faces[:,[0,1]],faces[:,[1,2]],faces[:,[2,0]]]),1);unique,counts=np.unique(edges,axis=0,return_counts=True);bad=np.unique(unique[counts>2])
for vertex in bad:
    incident=np.where((faces==vertex).any(1))[0];links={int(i):set() for i in incident};neighbors={}
    for fi in incident:
        for neighbor in faces[fi]:
            if neighbor!=vertex:neighbors.setdefault(int(neighbor),[]).append(int(fi))
    for adjacent in neighbors.values():
        if len(adjacent)==2:
            a,b=adjacent;links[a].add(b);links[b].add(a)
    groups=[];remaining=set(links)
    while remaining:
        seed=remaining.pop();group={seed};queue=[seed]
        while queue:
            fi=queue.pop()
            for other in links[fi]&remaining:remaining.remove(other);group.add(other);queue.append(other)
        groups.append(group)
    origin=np.array(vertices[vertex])
    for group_index,group in enumerate(groups):
        co=origin+direction*(group_index-(len(groups)-1)/2)*.00004
        if group_index==0:vertices[vertex]=co.tolist();new=int(vertex)
        else:new=len(vertices);vertices.append(co.tolist())
        ids=np.array(sorted(group));sub=faces[ids];sub[sub==vertex]=new;faces[ids]=sub
np.savez_compressed(path,vertices=np.array(vertices,np.float32),faces=faces,uv=uv)
print('CONTOUR_FANS_SEPARATED',name,'touching_vertices',len(bad),flush=True)
