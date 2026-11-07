"""Audit the real assembled Blender mesh for coincident faces and unsupported modules."""
import bpy
import sys
import json
from pathlib import Path
from collections import defaultdict
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools'))
from fuel04_geometry import intersection,signed_area,projected_axes,bounds_overlap

args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
source=Path(args[0]) if args else ROOT/'ArtSource/FuelStop04/FuelStop04_Assembly.blend'
output=Path(args[1]) if len(args)>1 else ROOT/'Builds/FuelStop04/geometry-audit.json'
bpy.ops.wm.open_mainfile(filepath=str(source),load_ui=False,use_scripts=False)
bpy.context.view_layer.update()
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
planes=defaultdict(list);vertices=[];triangles=[];owners=[];module_data=[];component_data=[]
for obj in objects:
    points=[obj.matrix_world@v.co for v in obj.data.vertices]
    obj.data.calc_loop_triangles();base=len(vertices);vertices.extend(points)
    bvh=BVHTree.FromPolygons(points,[tuple(t.vertices) for t in obj.data.loop_triangles],all_triangles=True,epsilon=.002)
    low=Vector([min(p[k] for p in points) for k in range(3)])
    high=Vector([max(p[k] for p in points) for k in range(3)])
    module_data.append((obj.name,low,high,bvh,points))
    # Recover surface islands after UV seams, then test their actual contacts too.
    # This catches a floating bulb/knob inside an otherwise grounded module.
    parents=list(range(len(obj.data.loop_triangles)));first={}
    def find(i):
        while parents[i]!=i:parents[i]=parents[parents[i]];i=parents[i]
        return i
    for i,t in enumerate(obj.data.loop_triangles):
        for v in t.vertices:
            key=tuple(round(c,5) for c in points[v])
            if key in first:parents[find(i)]=find(first[key])
            else:first[key]=i
    islands=defaultdict(list)
    for i,t in enumerate(obj.data.loop_triangles):islands[find(i)].append(tuple(t.vertices))
    for index,faces in enumerate(islands.values()):
        verts=sorted({v for face in faces for v in face});mapping={v:i for i,v in enumerate(verts)}
        cpoints=[points[i] for i in verts];cfaces=[tuple(mapping[v] for v in face) for face in faces]
        clo=Vector([min(p[k] for p in cpoints) for k in range(3)])
        chi=Vector([max(p[k] for p in cpoints) for k in range(3)])
        component_data.append((f'{obj.name} [surface {index}]',clo,chi,BVHTree.FromPolygons(cpoints,cfaces,all_triangles=True,epsilon=.002),cpoints))
    for t in obj.data.loop_triangles:
        triangles.append(tuple(base+i for i in t.vertices));owners.append(obj.name)
        p=[points[i] for i in t.vertices];normal=(p[1]-p[0]).cross(p[2]-p[0])
        if normal.length<1e-8:continue
        normal.normalize();d=normal.dot(p[0]);axes=projected_axes(normal)
        key=tuple(round(v,4) for v in normal)+(round(d,4),)
        planes[key].append((obj.name,t.polygon_index,[(v[axes[0]],v[axes[1]]) for v in p]))
overlaps=defaultdict(lambda:{'pairs':0,'area':0.,'faces':[]})
for group in planes.values():
    for i,(name,face,a) in enumerate(group):
        for other,otherface,b in group[i+1:]:
            if name==other and face==otherface:continue
            if not bounds_overlap(a,b):continue
            polygon=intersection(a,b)
            area=abs(signed_area(polygon)) if len(polygon)>=3 else 0
            if area<1e-7:continue
            key=' / '.join(sorted([name,other]));record=overlaps[key]
            record['pairs']+=1;record['area']+=area
            if len(record['faces'])<6:record['faces'].append([face,otherface])
# A module is supported when it belongs to the geometric contact graph of the deck.
# A real mesh intersection or <= 3 mm surface contact is required, not just AABB overlap.
def unsupported_parts(data):
    links=defaultdict(set)
    for i,(name,lo,hi,bvh,points) in enumerate(data):
        for j,(other,olo,ohi,obvh,opoints) in enumerate(data[i+1:],i+1):
            if any(hi[k]<olo[k]-.003 or ohi[k]<lo[k]-.003 for k in range(3)):continue
            contact=bool(bvh.overlap(obvh))
            if not contact:
                for p in points:
                    if all(olo[k]-.003<=p[k]<=ohi[k]+.003 for k in range(3)):
                        nearest=obvh.find_nearest(p,.003)
                        if nearest[0] is not None:contact=True;break
            if contact:links[i].add(j);links[j].add(i)
    supported={i for i,(name,*_) in enumerate(data) if name.startswith('Deck ')}
    pending=list(supported)
    while pending:
        for neighbor in links[pending.pop()]-supported:supported.add(neighbor);pending.append(neighbor)
    return [{'name':d[0],'min':list(d[1]),'max':list(d[2])} for i,d in enumerate(data)
            if i not in supported and not d[0].startswith('ParkingDecal')]
unsupported=unsupported_parts(module_data)
unsupported_components=unsupported_parts(component_data)
report={'source':str(source),'modules':len(objects),'triangles':len(triangles),
        'coplanarOverlapPairs':sum(v['pairs'] for v in overlaps.values()),
        'coplanarOverlaps':dict(sorted(overlaps.items(),key=lambda v:-v[1]['area'])),
        'unsupportedModules':unsupported,'surfaceIslands':len(component_data),
        'unsupportedSurfaceIslands':unsupported_components,'supportToleranceMetres':.003,
        'success':not overlaps and not unsupported and not unsupported_components}
output.parent.mkdir(parents=True,exist_ok=True);output.write_text(json.dumps(report,indent=2))
print(json.dumps({k:v for k,v in report.items() if k!='coplanarOverlaps'},indent=2),flush=True)
print('OVERLAPS',json.dumps(report['coplanarOverlaps'],indent=2)[:18000],flush=True)
