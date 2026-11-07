"""Convex polygon operations used to remove coincident painted surfaces."""
import math

def signed_area(points):
    return sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(points,points[1:]+points[:1]))*.5

def clip_halfspace(vertices, a, b, inside=True, epsilon=1e-9):
    # Vertices may carry interpolated position, UV and colour after their XY values.
    def distance(p): return (b[0]-a[0])*(p[1]-a[1])-(b[1]-a[1])*(p[0]-a[0])
    result=[]
    for p,q in zip(vertices,vertices[1:]+vertices[:1]):
        dp,dq=distance(p),distance(q)
        pin=dp>=-epsilon if inside else dp<=epsilon
        qin=dq>=-epsilon if inside else dq<=epsilon
        if pin: result.append(p)
        if pin != qin:
            t=dp/(dp-dq)
            result.append(tuple(x+(y-x)*t for x,y in zip(p,q)))
    return result

def intersection(a,b):
    result=list(a)
    if signed_area(b)<0:b=list(reversed(b))
    for p,q in zip(b,b[1:]+b[:1]):
        result=clip_halfspace(result,p,q)
        if len(result)<3:return []
    return result

def subtract(a,b):
    """Partition convex A minus convex B into nonoverlapping convex pieces."""
    if len(intersection(a,b))<3:return [a]
    if signed_area(b)<0:b=list(reversed(b))
    result=[];remaining=list(a)
    for p,q in zip(b,b[1:]+b[:1]):
        outside=clip_halfspace(remaining,p,q,False)
        if len(outside)>=3 and abs(signed_area(outside))>1e-10:result.append(outside)
        remaining=clip_halfspace(remaining,p,q,True)
        if len(remaining)<3:break
    return result

def projected_axes(normal):
    dominant=max(range(3),key=lambda a:abs(normal[a]))
    return tuple(a for a in range(3) if a!=dominant)

def bounds_overlap(a,b,epsilon=1e-6):
    return all(min(p[k] for p in a)<max(p[k] for p in b)-epsilon and
               min(p[k] for p in b)<max(p[k] for p in a)-epsilon for k in range(2))
