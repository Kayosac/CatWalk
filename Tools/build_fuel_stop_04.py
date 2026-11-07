"""Build the approved Fuel 04 modular kit in Blender (metres, one RGBA atlas).

Run: Blender --background --python Tools/build_fuel_stop_04.py
Each model is an independently editable .blend and FBX with a floor/grid pivot.
No vehicle, character, existing model, gameplay prefab or scene is overwritten.
"""
import bpy
import argparse
import json
import math
import random
import shutil
import struct
import sys
import time
import zlib
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
from fuel04_geometry import subtract, signed_area, projected_axes, bounds_overlap

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / 'ArtSource/FuelStop04'
MODELS = ROOT / 'CatWalk/Assets/SpaceCourier/Resources/Models/FuelStop04'
CONTENT = ROOT / 'CatWalk/Assets/SpaceCourier/Content/FuelStop04'
OUT = ROOT / 'Builds/FuelStop04'
for directory in [SRC / 'Models', MODELS, CONTENT, OUT]:
    directory.mkdir(parents=True, exist_ok=True)
parser = argparse.ArgumentParser()
parser.add_argument('--only', nargs='*')
parser.add_argument('--no-render', action='store_true')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])

# Pixel coordinates in the single atlas; UVs inset by two pixels to prevent bleed.
REGIONS = {
    'cream': (0, 0, 128, 128), 'steel': (128, 0, 256, 128),
    'oxide': (256, 0, 384, 128), 'deck': (384, 0, 512, 128),
    'roof': (0, 128, 128, 256), 'rubber': (128, 128, 256, 256),
    'wood': (256, 128, 384, 256), 'window': (384, 128, 512, 256),
    'hazard': (0, 256, 128, 384), 'sign04': (128, 256, 256, 320),
    'post': (128, 320, 192, 384), 'repair': (192, 320, 256, 384),
    'fuel': (256, 256, 320, 320), 'cyan': (320, 256, 384, 320),
    'warm': (256, 320, 320, 384), 'label': (320, 320, 384, 384),
    'glass': (384, 256, 512, 384), 'parking': (0, 384, 128, 512),
    'vent': (128, 384, 256, 512), 'service': (256, 384, 384, 512),
    'trim': (384, 384, 512, 512),
}
PALETTE = {
    'cream': (178, 176, 157), 'steel': (66, 76, 75),
    'oxide': (155, 92, 56), 'deck': (83, 88, 84),
    'roof': (70, 78, 78), 'rubber': (32, 39, 39),
    'wood': (121, 101, 74), 'window': (181, 130, 65),
    'hazard': (162, 125, 64), 'sign04': (52, 48, 37),
    'post': (39, 53, 53), 'repair': (39, 53, 53), 'fuel': (39, 53, 53),
    'cyan': (118, 175, 165), 'warm': (239, 194, 119),
    'label': (180, 172, 145), 'glass': (38, 50, 51),
    'parking': (181, 175, 147), 'vent': (45, 54, 53),
    'service': (160, 143, 106), 'trim': (109, 116, 108),
}

def atlas():
    """Original, deterministic pixel paint. No external images or extra maps."""
    rng = random.Random(40426)
    pixels = bytearray(512 * 512 * 4)
    def put(x, y, color):
        if 0 <= x < 512 and 0 <= y < 512:
            p = (y * 512 + x) * 4
            pixels[p:p+4] = bytes(tuple(color[:3]) + (color[3] if len(color) > 3 else 255,))
    def rect(x0, y0, x1, y1, color):
        for y in range(y0, y1):
            for x in range(x0, x1): put(x, y, color)
    def stroke(points, color, width=2):
        for a, b in zip(points, points[1:]):
            count = max(abs(b[0]-a[0]), abs(b[1]-a[1]), 1)
            for i in range(count+1):
                x = round(a[0] + (b[0]-a[0])*i/count)
                y = round(a[1] + (b[1]-a[1])*i/count)
                rect(x-width//2, y-width//2, x+(width+1)//2, y+(width+1)//2, color)
    for name, (x0,y0,x1,y1) in REGIONS.items():
        color = PALETTE[name]
        for y in range(y0, y1, 2):
            for x in range(x0, x1, 2):
                noise = rng.choice([-4, -2, 0, 0, 0, 2, 3])
                rect(x,y,x+2,y+2,tuple(max(0,min(255,c+noise)) for c in color))
        if name in ['cream','steel','oxide','deck','roof','wood','trim']:
            # Broad old paint patches and sparse stepped edge chips, not photo noise.
            for _ in range(28):
                x=rng.randrange(x0+4,x1-7); y=rng.choice([rng.randrange(y0+4,y0+15),rng.randrange(y1-17,y1-5),rng.randrange(y0+8,y1-8)])
                w=rng.randrange(2,8); h=rng.randrange(1,4)
                c=(107,76,52) if name=='cream' else tuple(max(0,v-13) for v in color)
                rect(x,y,x+w,y+h,c)
                rect(x+1,y+h,x+w//2+1,y+h+2,c)
        if name=='deck':
            rect(x0+3,y0+3,x1-3,y0+5,(51,57,55));rect(x0+3,y1-5,x1-3,y1-3,(48,54,53))
            rect(x0+3,y0+3,x0+5,y1-3,(51,57,55));rect(x1-5,y0+3,x1-3,y1-3,(48,54,53))
            for x in [x0+9,x1-11]:
                for y in [y0+9,y1-11]: rect(x,y,x+2,y+2,(112,116,105))
        if name=='roof':
            for x in range(x0+8,x1-4,16):
                rect(x,y0+3,x+3,y1-3,(83,91,87));rect(x+3,y0+3,x+5,y1-3,(51,60,59))
        if name=='wood':
            for y in range(y0+7,y1,24):rect(x0+3,y,x1-3,y+2,(76,68,53))
        if name=='hazard':
            for y in range(y0+2,y1-2):
                for x in range(x0+2,x1-2):
                    if ((x-x0+y-y0)//19)%2:put(x,y,(64,70,64))
        if name=='window':
            for x in [x0+5,x0+61,x1-8]:rect(x,y0+4,x+3,y1-4,(65,62,45))
            rect(x0+4,y1-10,x1-4,y1-5,(94,79,46))
        if name=='vent':
            for y in range(y0+12,y1-7,12):
                rect(x0+10,y,x1-10,y+5,(22,29,29));rect(x0+10,y+5,x1-10,y+7,(97,105,98))
    # Big readable 04. Same bitmap is shared by the sign and small equipment labels.
    glyphs={'0':['111','101','101','101','111'],'4':['101','101','111','001','001']}
    for k,ch in enumerate('04'):
        for y,row in enumerate(glyphs[ch]):
            for x,b in enumerate(row):
                if b=='1':rect(155+k*42+x*9,268+y*8,155+k*42+x*9+9,276+y*8,(230,180,98))
    # Purpose-built pixel pictograms, all in the atlas.
    stroke([(139,341),(181,341),(181,368),(139,368),(139,341),(160,356),(181,341)],(153,180,164),3)
    stroke([(207,367),(225,349),(236,348),(241,338),(233,341),(228,335),(225,345),(207,362)],(153,180,164),4)
    rect(265,266,304,278,(122,167,146))
    for y in range(284,309,7):rect(265,y,294,y+3,(115,158,142))
    rect(299,284,305,309,(104,139,127))
    # A mesh decal: completely transparent surroundings, chipped painted lines.
    x0,y0,x1,y1=REGIONS['parking'];rect(x0,y0,x1,y1,(0,0,0,0))
    paint=(182,177,151,255)
    for sx in [-1,1]:
        for sy in [-1,1]:
            x=64+sx*56; y=448+sy*56
            stroke([(x-sx*20,y),(x,y),(x,y-sy*20)],paint,3)
    for x in [8,120]:
        for y in [420,443,466]:rect(x-1,y,x+2,y+12,paint)
    for _ in range(65):
        x=rng.randrange(4,124);y=rng.randrange(388,508)
        put(x,y,(0,0,0,0))
    raw=b''.join(b'\x00'+bytes(pixels[y*2048:(y+1)*2048]) for y in range(512))
    def chunk(tag,data):return struct.pack('!I',len(data))+tag+data+struct.pack('!I',zlib.crc32(tag+data)&0xffffffff)
    png=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('!2I5B',512,512,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw,9))+chunk(b'IEND',b'')
    path=SRC/'FuelStop04_Atlas.png';path.write_bytes(png)
    shutil.copy2(path,CONTENT/path.name)
    (SRC/'AtlasRegions.json').write_text(json.dumps({'size':512,'regions':REGIONS,'emission':'vertex color alpha, not a second texture','opacity':'atlas alpha, used by parking decal'},indent=2))
    return path

bpy.ops.wm.read_factory_settings(use_empty=True)
image=bpy.data.images.load(str(atlas()));image.colorspace_settings.name='sRGB'
mat=bpy.data.materials.new('FuelStop04_Atlas');mat.use_nodes=True
nodes=mat.node_tree.nodes;links=mat.node_tree.links;nodes.clear()
out=nodes.new('ShaderNodeOutputMaterial');pbr=nodes.new('ShaderNodeBsdfPrincipled');tex=nodes.new('ShaderNodeTexImage')
tex.image=image;tex.interpolation='Closest';tex.extension='EXTEND'
attr=nodes.new('ShaderNodeVertexColor');attr.layer_name='Color'
multiply=nodes.new('ShaderNodeMath');multiply.operation='MULTIPLY';multiply.inputs[1].default_value=2.0
links.new(tex.outputs['Color'],pbr.inputs['Base Color']);links.new(tex.outputs['Alpha'],pbr.inputs['Alpha'])
links.new(tex.outputs['Color'],pbr.inputs['Emission Color']);links.new(attr.outputs['Alpha'],multiply.inputs[0]);links.new(multiply.outputs[0],pbr.inputs['Emission Strength'])
pbr.inputs['Roughness'].default_value=.92
links.new(pbr.outputs[0],out.inputs['Surface'])
try: mat.surface_render_method='DITHERED'
except Exception: pass

class Mesh:
    def __init__(self):self.v=[];self.faces=[];self.uvs=[];self.colors=[]
    def face(self,pts,region='steel',emission=0,uv=None):
        pts=[Vector(p) for p in pts];start=len(self.v);self.v.extend(pts);self.faces.append(tuple(range(start,start+len(pts))))
        if uv is None:
            normal=(pts[1]-pts[0]).cross(pts[2]-pts[0]);axis=max(range(3),key=lambda a:abs(normal[a]));axes=((1,2),(0,2),(0,1))[axis]
            low=[min(p[a] for p in pts) for a in axes];span=[max(p[a] for p in pts)-low[j] for j,a in enumerate(axes)]
            uv=[((p[axes[0]]-low[0])/max(span[0],.00001),(p[axes[1]]-low[1])/max(span[1],.00001)) for p in pts]
        x0,y0,x1,y1=REGIONS[region]
        self.uvs.extend([((x0+2+u*(x1-x0-4))/512,1-(y1-2-v*(y1-y0-4))/512) for u,v in uv])
        self.colors.extend([(1,1,1,emission)]*len(pts))
    def box(self,p,s,region='steel',emission=0):
        x,y,z=p;a,b,c=[v/2 for v in s]
        v=[(x+dx*a,y+dy*b,z+dz*c) for dx,dy,dz in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
        for f in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]:self.face([v[i] for i in f],region,emission)
    def prism(self,points,z0,z1,side='steel',top=None):
        self.face([(x,y,z0) for x,y in reversed(points)],side)
        self.face([(x,y,z1) for x,y in points],top or side)
        for a,b in zip(points,points[1:]+points[:1]):self.face([(*a,z0),(*b,z0),(*b,z1),(*a,z1)],side)
    def cylinder(self,a,b,r,region='steel',n=12,r2=None,emission=0):
        a,b=Vector(a),Vector(b);d=(b-a).normalized();u=d.cross(Vector((1,0,0)) if abs(d.x)<.9 else Vector((0,1,0))).normalized();v=d.cross(u)
        rings=[[p+(u*math.cos(i*math.tau/n)+v*math.sin(i*math.tau/n))*radius for i in range(n)] for p,radius in [(a,r),(b,r if r2 is None else r2)]]
        self.face(list(reversed(rings[0])),region,emission);self.face(rings[1],region,emission)
        for i in range(n):
            j=(i+1)%n
            self.face([rings[0][i],rings[0][j],rings[1][j],rings[1][i]],region,emission,[(i/n,0),((i+1)/n,0),((i+1)/n,1),(i/n,1)])
    def beam(self,a,b,width,depth,region='steel'):
        a,b=Vector(a),Vector(b);d=(b-a).normalized();u=d.cross(Vector((0,1,0)) if abs(d.y)<.9 else Vector((1,0,0))).normalized()*width/2;v=d.cross(u).normalized()*depth/2
        rings=[[p-u-v,p+u-v,p+u+v,p-u+v] for p in [a,b]]
        self.face(list(reversed(rings[0])),region);self.face(rings[1],region)
        for i in range(4):j=(i+1)%4;self.face([rings[0][i],rings[0][j],rings[1][j],rings[1][i]],region)
    def tube(self,centres,r,region='rubber',n=8):
        pts=[Vector(p) for p in centres];rings=[]
        for i,p in enumerate(pts):
            d=(pts[min(i+1,len(pts)-1)]-pts[max(0,i-1)]).normalized();u=d.cross(Vector((0,0,1))).normalized();v=d.cross(u)
            rings.append([p+r*(u*math.cos(j*math.tau/n)+v*math.sin(j*math.tau/n)) for j in range(n)])
        for i in range(len(rings)-1):
            for j in range(n):k=(j+1)%n;self.face([rings[i][j],rings[i][k],rings[i+1][k],rings[i+1][j]],region)
        self.face(list(reversed(rings[0])),region);self.face(rings[-1],region)
    def panel(self,p,w,h,region,emission=0):
        x,y,z=p;self.face([(x-w/2,y,z-h/2),(x+w/2,y,z-h/2),(x+w/2,y,z+h/2),(x-w/2,y,z+h/2)],region,emission,[(0,0),(1,0),(1,1),(0,1)])
    def remove_coincident_surfaces(self):
        # Detail patches replace the underlying surface instead of hovering above it.
        # Subtract later coplanar faces, preserving each cut's original UVs and colour.
        planes={};clean=[];removed=0
        for face in reversed(self.faces):
            pts=[self.v[i] for i in face]
            normal=(pts[1]-pts[0]).cross(pts[2]-pts[0]).normalized()
            axes=projected_axes(normal)
            projected=[(p[axes[0]],p[axes[1]],*p,*self.uvs[i],*self.colors[i]) for i,p in zip(face,pts)]
            key=tuple(round(v,5) for v in normal)+(round(normal.dot(pts[0]),5),)
            pieces=[projected]
            for cover in planes.get(key,[]):
                next_pieces=[]
                for piece in pieces:
                    next_pieces.extend(subtract(piece,cover) if bounds_overlap(piece,cover) else [piece])
                pieces=next_pieces
                if not pieces:break
            if sum(abs(signed_area(p)) for p in pieces)<abs(signed_area(projected))-1e-8:removed+=1
            clean.extend(p for p in pieces if len(p)>=3 and abs(signed_area(p))>1e-10)
            planes.setdefault(key,[]).append(projected)
        self.v=[];self.faces=[];self.uvs=[];self.colors=[]
        for polygon in reversed(clean):
            start=len(self.v)
            self.v.extend(Vector(p[2:5]) for p in polygon)
            self.uvs.extend(p[5:7] for p in polygon);self.colors.extend(p[7:11] for p in polygon)
            self.faces.append(tuple(range(start,len(self.v))))
        return removed
    def object(self,name):
        data=bpy.data.meshes.new(name);data.from_pydata(self.v,[],self.faces);data.update()
        uv=data.uv_layers.new(name='AtlasUV');color=data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
        for i,value in enumerate(self.uvs):uv.data[i].uv=value;color.data[i].color=self.colors[i]
        obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj);obj.data.materials.append(mat)
        obj['atlas']='FuelStop04_Atlas.png';obj['units']='metres';obj['emission']='vertex color alpha';return obj

def deck(m,side=0):
    pts=[(-2,-2.5),(2,-2.5),(2,2.5),(-2,2.5)]
    if side==-1:pts=[(-.7,-2.5),(2,-2.5),(2,2.5),(-2,2.5),(-2,-1.2)]
    if side==1:pts=[(-2,-2.5),(.7,-2.5),(2,-1.2),(2,2.5),(-2,2.5)]
    m.prism(pts,-.32,0,'steel','deck')
def rim(m):
    m.box((0,0,-.65),(4,.48,1.1),'steel');m.box((0,0,-.16),(4,.6,.16),'trim')
    m.panel((0,-.24,-.55),2.5,.55,'hazard');m.box((0,0,-1.18),(4,.64,.14),'rubber')
def rim_joint(m):
    # Cast corner sleeve covers the shortened beam ends without coplanar caps.
    m.cylinder((0,0,-1.53),(0,0,-.36),.40,'steel',8)
    m.cylinder((0,0,-1.55),(0,0,-1.39),.42,'rubber',8)
def brace(m):
    m.box((0,0,-1.3),(3,.4,.25),'steel');m.beam((-1.4,0,-1.25),(0,0,-3.1),.22,.32,'steel');m.beam((1.4,0,-1.25),(0,0,-3.1),.22,.32,'steel');m.box((0,0,-3),( .48,.6,.35),'oxide')
    for x in [-1.35,1.35]:m.box((x,0,-.81),(.2,.32,.98),'steel')
def cabin(m):
    # A usable room: floor, nonoverlapping wall courses, open hatch and side entrance.
    m.box((0,0,.04),(7.44,5.44,.08),'wood')
    def wall_x(x,y,width,depth):
        m.box((x,y,.42),(width,depth,.84),'steel')
        m.box((x,y,2.42),(width,depth,3.16),'cream')
    wall_x(0,2.86,8,.28);wall_x(-3.86,0,.28,5.44)
    # Door opening on the right: Y .65..2.05, Z .08..2.65.
    wall_x(3.86,-1.035,.28,3.37);wall_x(3.86,2.385,.28,.67)
    m.box((3.86,1.35,3.325),(.28,1.4,1.35),'cream')
    for x in [-2.95,2.95]:wall_x(x,-2.86,2.1,.28)
    m.box((0,-2.86,.5),(3.8,.28,1),'steel')
    m.box((0,-2.86,3.45),(3.8,.28,1.1),'cream')
    # Window frame is solid timber; there is no false wall behind the opening.
    for x in [-1.87,1.87]:m.box((x,-2.87,1.98),(.10,.36,1.96),'trim')
    m.box((0,-2.87,2.91),(3.84,.36,.12),'trim')
    for y in [.65,2.05]:m.box((3.86,y,1.37),(.36,.10,2.58),'trim')
    m.box((3.86,1.35,2.65),(.36,1.5,.12),'trim')
    # Threshold joins the raised interior floor to the deck.
    m.box((3.88,1.35,.04),(.72,1.4,.08),'steel')
def cabin_door(m):
    # Hinge pivot at the lower left; the assembly parks this door open against the wall.
    m.box((.65,0,1.25),(1.3,.10,2.5),'steel')
    m.panel((.65,-.05,1.35),1.08,2.12,'cream')
    m.box((1.11,-.10,1.18),(.06,.12,.24),'rubber')
    for z in [.35,2.15]:m.cylinder((0,0,z-.12),(0,0,z+.12),.07,'trim',8)
def cabin_shelf(m):
    for x in [-1.35,1.35]:m.box((x,0,1.12),(.10,.62,2.24),'steel')
    for z in [.18,.86,1.54,2.24]:m.box((0,0,z),(2.8,.68,.08),'wood')
    m.box((0,.30,1.15),(2.7,.06,2.1),'steel')
    for x in [-.48,.48]:m.box((x,0,1.58),(.055,.60,1.28),'trim')
    m.box((0,0,2.16),(1.12,.32,.10),'steel')
    m.box((0,-.02,2.10),(.98,.24,.04),'warm',.65)
def cabin_cabinet(m):
    m.box((0,0,.08),(1.48,.64,.16),'rubber')
    m.box((0,0,.52),(1.6,.72,.80),'steel')
    m.box((0,0,.97),(1.7,.82,.10),'wood')
    for x in [-.4,.4]:
        m.panel((x,-.36,.52),.72,.66,'cream')
        m.box((x+.25,-.4,.62),(.055,.08,.19),'rubber')
def roof(m):
    m.box((0,0,.13),(8.5,6.5,.26),'roof')
    for x in [-4,-3,-2,-1,0,1,2,3,4]:m.box((x,0,.28),(.055,6.5,.06),'trim')
    m.box((0,-3.25,.14),(8.6,.12,.3),'steel');m.box((0,3.25,.14),(8.6,.12,.3),'steel')
def awning(m):
    for x in [-4.3,4.3]:
        m.box((x,-1.25,1.58),(.13,.13,3.16),'steel');m.box((x,-1.25,.12),(.34,.34,.24),'oxide')
        m.beam((x,-1.25,2.65),(x,-.35,3.22),.09,.09,'steel')
    # Sloped, single-thickness canopy, high edge toward cabin (+Y).
    for i in range(9):
        x=-4+i
        top=[(x-.5,-1.45,3.2),(x+.5,-1.45,3.2),(x+.5,1.45,3.46),(x-.5,1.45,3.46)]
        bottom=[(a,b,c-.08) for a,b,c in top]
        m.face(top,'oxide');m.face(list(reversed(bottom)),'roof')
        for j in range(4):k=(j+1)%4;m.face([bottom[j],bottom[k],top[k],top[j]],'oxide')
        m.beam((x-.49,-1.45,3.23),(x-.49,1.45,3.49),.045,.035,'trim')
    m.box((0,-1.45,3.14),(9,.12,.2),'oxide');m.box((0,1.45,3.4),(8.9,.16,.2),'steel')
    for x in [-.45,.45]:m.box((x,.45,3.20),(.06,.06,.20),'steel')
    m.box((0,.45,3.12),(1.3,.36,.12),'rubber');m.box((0,.45,3.04),(1.05,.27,.04),'warm',.7)
def counter(m):
    m.box((0,0,.5),(3.35,.88,1),'steel')
    m.box((0,.35,1.04),(3.5,1.9,.08),'wood')
    for x in [-1.55,1.55]:m.box((x,1.15,.54),(.12,.12,.92),'steel')
    m.panel((0,-.44,.5),3.05,.6,'cream')
    for x in [-1.58,1.58]:m.box((x,-.46,.5),(.07,.06,.86),'oxide')
def tank(m):
    for x,y in [(-1.5,-1.5),(1.5,-1.5),(-1.5,1.5),(1.5,1.5)]:
        m.box((x,y,.45),(.34,.34,.9),'steel');m.box((x,y,.08),(.65,.65,.16),'oxide')
    m.cylinder((0,0,.7),(0,0,3.5),2.5,'steel',16)
    m.cylinder((0,0,3.5),(0,0,4.2),2.5,'oxide',16)
    m.cylinder((0,0,4.2),(0,0,6.65),2.5,'cream',16)
    m.cylinder((0,0,6.65),(0,0,7.15),2.5,'cream',16,r2=1.75)
    m.cylinder((0,0,7.15),(0,0,7.5),.65,'steel',8)
    for z in [.75,6.65]:m.cylinder((0,0,z-.06),(0,0,z+.06),2.54,'trim',16)
    m.cylinder((0,-2,.77),(0,-2.72,.77),.22,'steel',8)
    m.cylinder((0,-2.52,.77),(0,-2.6,.77),.31,'oxide',8)
    # A sparse side service riser, not sci-fi greebles.
    m.cylinder((2.48,0,1),(2.48,0,5.8),.12,'steel',8)
    m.cylinder((2.48,0,5.8),(2.15,0,5.8),.12,'steel',8)
def pump(m):
    m.box((0,0,.12),(1.7,2.2,.24),'steel');m.box((0,0,.8),(1.25,1.15,1.35),'cream')
    m.box((0,0,1.56),(1.42,1.25,.48),'cream');m.box((0,-.64,1.56),(1,.06,.49),'rubber')
    m.panel((0,-.67,1.57),.84,.35,'fuel',.2);m.box((0,-.588,.63),(1.25,.03,.3),'oxide')
    for x in [-.26,0,.26]:m.box((x,-.595,1.13),(.13,.06,.1),'rubber')
    m.cylinder((.5,.5,.65),(.9,.5,.65),.17,'steel',8)
def reel(m):
    m.box((0,0,.08),(1.3,1.1,.16),'oxide')
    for x in [-.48,.48]:m.box((x,0,.42),(.15,.85,.75),'steel')
    for x in [-.59,.59]:m.cylinder((x-.04,0,.7),(x+.04,0,.7),.59,'oxide',12)
    m.cylinder((-.5,0,.7),(.5,0,.7),.44,'rubber',12)
    for i in range(7):
        x=-.45+i*.15;m.cylinder((x-.035,0,.7),(x+.035,0,.7),.49,'rubber',12)
    m.box((.7,-.2,.7),(.2,.14,.4),'trim')
    m.tube([(.5,-.35,.6),(.64,-.5,.3),(.56,-.52,.2),(.2,-.52,.2)],.07)
def pipe(m):
    m.cylinder((0,-1,0),(0,1,0),.18,'steel',8)
    for y in [-.9,.9]:m.cylinder((0,y-.05,0),(0,y+.05,0),.25,'oxide',8)
def elbow(m):
    r=.35;m.tube([(r-r*math.cos(t*math.pi/12),-r+r*math.sin(t*math.pi/12),0) for t in range(7)],.18,'steel',8)
def tee(m):
    m.cylinder((-.35,0,0),(.35,0,0),.19,'steel',8);m.cylinder((0,0,0),(0,.35,0),.18,'steel',8)
def pipe_support(m):
    m.box((0,0,.06),(.55,.5,.12),'oxide')
    m.box((0,0,.34),(.20,.22,.52),'steel')
    m.cylinder((0,-.15,.77),(0,.15,.77),.24,'trim',8)
def rail(m):
    m.box((0,0,1.12),(4,.12,.12),'oxide');m.box((0,0,.58),(4,.1,.1),'steel')
def post(m):
    m.box((0,0,.6),(.15,.15,1.2),'steel');m.box((0,0,.045),(.4,.4,.09),'trim');m.box((0,0,1.22),(.2,.2,.08),'oxide')
def beacon(m):
    m.box((0,0,.1),(.54,.54,.2),'steel');m.box((0,0,.35),(.29,.29,.4),'rubber');m.box((0,0,.63),(.25,.25,.25),'cyan',.8)
def lamp(m):
    m.box((0,0,.08),(.55,.55,.16),'steel');m.beam((0,0,.12),(0,0,2.7),.11,.11,'oxide')
    m.beam((0,0,2.7),(0,-.65,2.9),.11,.11,'oxide');m.box((0,-.7,2.86),(.65,.42,.23),'steel');m.box((0,-.72,2.75),(.53,.3,.04),'warm',.8)
def sign(m):
    m.box((0,0,.1),(.8,.7,.2),'steel');m.box((0,0,2.65),(.18,.18,5.3),'steel')
    m.box((0,0,5.22),(2.7,.35,1.55),'steel');m.box((0,-.2,5.22),(2.52,.08,1.37),'oxide');m.panel((0,-.24,5.22),2.3,1.12,'sign04',.42)
def terminal(m,icon):
    m.box((0,0,.08),(.8,.75,.16),'steel');m.box((0,0,.58),(.36,.36,1),'steel')
    m.box((0,0,1.28),(1.05,.6,.72),'cream');m.box((0,-.32,1.31),(.89,.05,.56),'rubber');m.panel((0,-.345,1.32),.72,.46,icon,.25)
    m.box((0,-.38,.96),(.85,.4,.09),'oxide');m.box((.3,-.43,1.03),(.12,.12,.06),'warm',.3)
def workbench(m):
    for x in [-.9,.9]:
        for y in [-.28,.28]:m.box((x,y,.48),(.09,.09,.96),'steel')
    m.box((0,0,.96),(2.1,.8,.12),'wood');m.box((0,0,.23),(1.95,.67,.1),'steel')
def toolcase(m):
    m.box((0,0,.22),(.9,.5,.44),'oxide');m.box((0,0,.47),(.94,.54,.1),'oxide')
    for x in [-.29,.29]:m.box((x,-.27,.36),(.1,.04,.17),'trim')
    m.box((0,0,.65),(.36,.08,.06),'rubber')
    for x in [-.15,.15]:m.box((x,0,.58),(.06,.08,.13),'steel')
def canister(m):
    m.prism([(-.25,-.18),(.25,-.18),(.3,-.12),(.3,.12),(.25,.18),(-.25,.18),(-.3,.12),(-.3,-.12)],0,.65,'cream')
    m.box((0,0,.75),(.34,.1,.08),'steel')
    for x in [-.14,.14]:m.box((x,0,.69),(.06,.1,.12),'steel')
    m.cylinder((.2,0,.65),(.2,0,.76),.07,'oxide',8)
    m.panel((0,-.18,.32),.35,.38,'hazard')
def radio(m):
    m.box((0,0,.22),(.62,.25,.44),'steel');m.panel((-.1,-.125,.23),.29,.28,'vent');m.panel((.19,-.125,.29),.15,.1,'fuel',.12)
    for z in [.11,.2]:m.cylinder((.2,-.12,z),(.2,-.19,z),.035,'oxide',8)
    m.cylinder((-.2,0,.44),(-.27,0,.95),.012,'steel',6)
def seal(m):
    # Flat gasket ring, with a true centre opening.
    n=16
    for i in range(n):
        a=i*math.tau/n;b=(i+1)*math.tau/n
        for z,flip in [(0,True),(.06,False)]:
            pts=[(.22*math.cos(a),.22*math.sin(a),z),(.32*math.cos(a),.32*math.sin(a),z),(.32*math.cos(b),.32*math.sin(b),z),(.22*math.cos(b),.22*math.sin(b),z)]
            m.face(list(reversed(pts)) if flip else pts,'rubber')
        for r,flip in [(.22,True),(.32,False)]:
            pts=[(r*math.cos(a),r*math.sin(a),0),(r*math.cos(b),r*math.sin(b),0),(r*math.cos(b),r*math.sin(b),.06),(r*math.cos(a),r*math.sin(a),.06)]
            m.face(list(reversed(pts)) if flip else pts,'rubber')
def vent(m):
    m.box((0,0,0),(1.1,.12,.75),'trim');m.panel((0,-.06,0),.9,.55,'vent')
def spanner(m):
    m.box((0,0,.025),(.09,.4,.05),'trim');m.box((0,.21,.025),(.23,.08,.05),'trim')
    for x in [-.09,.09]:m.box((x,.29,.025),(.05,.13,.05),'trim')
def parking(m):
    m.face([(-5,-6,0),(5,-6,0),(5,6,0),(-5,6,0)],'parking',0,[(0,0),(1,0),(1,1),(0,1)])

BUILDERS={
    'DeckTile4x5':lambda m:deck(m), 'DeckCornerLeft':lambda m:deck(m,-1), 'DeckCornerRight':lambda m:deck(m,1),
    'RimBeam4m':rim,'UnderDeckBrace':brace,'CabinShell':cabin,'CabinRoof':roof,'CounterAwning':awning,
    'ServiceCounter':counter,'FuelTank':tank,'FuelPump':pump,'HoseReel':reel,
    'PipeStraight2m':pipe,'PipeElbow90':elbow,'PipeTee':tee,'RailSection4m':rail,'RailPost':post,
    'DockBeacon':beacon,'WorkLamp':lamp,'Sign04':sign,'PostTerminal':lambda m:terminal(m,'post'),
    'RepairTerminal':lambda m:terminal(m,'repair'),'Workbench':workbench,'ToolCase':toolcase,
    'Canister':canister,'Radio':radio,'SpareSeal':seal,'VentPanel':vent,'Spanner':spanner,'ParkingDecal':parking,
    'RimJoint':rim_joint,'PipeSupport':pipe_support,'CabinDoor':cabin_door,
    'CabinShelf':cabin_shelf,'CabinCabinet':cabin_cabinet,
}
catalog=[];objects={}
for name,build in BUILDERS.items():
    started=time.monotonic()
    if args.only and name not in args.only:
        with bpy.data.libraries.load(str(SRC/'Models'/f'{name}.blend')) as (source,target):target.objects=[name]
        obj=target.objects[0];bpy.context.collection.objects.link(obj)
        obj.data.materials.clear();obj.data.materials.append(mat)
    else:
        m=Mesh();build(m);removed=m.remove_coincident_surfaces();obj=m.object(name)
        obj['coincidentFacesTrimmed']=removed
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        bpy.ops.export_scene.fbx(filepath=str(MODELS/f'{name}.fbx'),use_selection=True,object_types={'MESH'},
            add_leaf_bones=False,apply_unit_scale=True,bake_space_transform=True,axis_forward='-Z',axis_up='Y',
            mesh_smooth_type='FACE',use_mesh_modifiers=True,colors_type='LINEAR',path_mode='STRIP')
    # Include a scene, so opening the individual .blend directly displays the model.
    asset_scene=bpy.data.scenes.new(name);asset_scene.collection.objects.link(obj)
    previous_scene=bpy.context.window.scene;bpy.context.window.scene=asset_scene;bpy.context.view_layer.update()
    bpy.data.libraries.write(str(SRC/'Models'/f'{name}.blend'),{asset_scene},path_remap='RELATIVE_ALL',fake_user=True,compress=True)
    bpy.context.window.scene=previous_scene
    bpy.data.scenes.remove(asset_scene)
    objects[name]=obj;obj.hide_render=True;obj.hide_set(True)
    obj.data.calc_loop_triangles()
    elapsed=time.monotonic()-started
    if elapsed>900:raise RuntimeError(f'{name}: 15 minute per-model limit exceeded')
    lo=[min(v.co[i] for v in obj.data.vertices) for i in range(3)];hi=[max(v.co[i] for v in obj.data.vertices) for i in range(3)]
    catalog.append({'name':name,'vertices':len(obj.data.vertices),'triangles':len(obj.data.loop_triangles),'buildSeconds':round(elapsed,3),
        'size':[round(hi[0]-lo[0],4),round(hi[2]-lo[2],4),round(hi[1]-lo[1],4)],'collider':name!='ParkingDecal'})
    print('FUEL04_MODEL',name,len(obj.data.loop_triangles),round(elapsed,2),flush=True)

instances=[]
def add(asset,x,y,z,yaw=0,scale=(1,1,1),group='Structure',name=None):
    instances.append({'asset':asset,'name':name or asset,'group':group,'position':[x,y,z],'rotation':[0,yaw,0],'scale':list(scale)})
for ix,x in enumerate(range(-14,15,4)):
    for iz,z in enumerate(range(-10,11,5)):
        asset='DeckTile4x5';yaw=0
        if ix in [0,7] and iz in [0,4]:
            asset='DeckCornerLeft' if (ix==0)==(iz==0) else 'DeckCornerRight';yaw=0 if iz==0 else 180
        add(asset,x,0,z,yaw,name=f'Deck {ix+1:02}-{iz+1:02}')
def line_instances(asset,a,b,length,group='Structure',height=0,end_trim=0):
    dx,dz=b[0]-a[0],b[1]-a[1];distance=math.hypot(dx,dz);count=max(1,math.ceil(distance/length));yaw=math.degrees(math.atan2(-dz,dx))
    for i in range(count):
        start=i*distance/count+(end_trim if i==0 else 0)
        end=(i+1)*distance/count-(end_trim if i==count-1 else 0)
        t=(start+end)*.5/distance
        add(asset,a[0]+dx*t,height,a[1]+dz*t,yaw,((end-start)/length,1,1),group)
rim_segments=[((-14.7,-12.25),(14.7,-12.25)),((14.7,12.25),(-14.7,12.25)),((-15.75,11.2),(-15.75,-11.2)),((15.75,-11.2),(15.75,11.2)),((-15.75,-11.2),(-14.7,-12.25)),((14.7,-12.25),(15.75,-11.2)),((15.75,11.2),(14.7,12.25)),((-14.7,12.25),(-15.75,11.2))]
for a,b in rim_segments:line_instances('RimBeam4m',a,b,4,height=-.24,end_trim=.18)
for i,(x,z) in enumerate(sorted({p for segment in rim_segments for p in segment})):
    add('RimJoint',x,0,z,name=f'Corner sleeve {i+1}')
for x in [-12,-4,4,12]:
    for z in [-9,8]:add('UnderDeckBrace',x,0,z)
add('CabinShell',-9,0,8,group='Service');add('CabinRoof',-9,4,8,group='Service')
add('CounterAwning',-8.5,0,3.55,group='Service');add('ServiceCounter',-9,0,4.55,group='Service')
add('CabinDoor',-4.96,.10,10.05,-10,group='Interior',name='Open cabin door')
add('CabinShelf',-10.65,.08,10.35,group='Interior',name='Mail shelves')
add('CabinCabinet',-7.4,.08,10.20,group='Interior',name='Storage cabinet')
add('Workbench',-11.6,.08,7.6,90,group='Interior',name='Interior workbench')
add('FuelTank',4,0,7,group='Fuel',name='Tank 1');add('FuelTank',11,0,7,group='Fuel',name='Tank 2')
add('FuelPump',8.55,0,-4.1,group='Fuel');add('HoseReel',10.05,0,-4.25,group='Fuel')
def pipe_segment(a,b,name=None,supports=True):
    a,b=Vector(a),Vector(b);d=(b-a).normalized();length=(b-a).length;middle=(a+b)*.5
    yaw=math.degrees(math.atan2(d.x,d.y))
    add('PipeStraight2m',middle.x,.77,middle.y,yaw,(1,1,length/2),group='Pipes',name=name)
    if supports and length>1:
        count=max(1,math.ceil(length/2.6))
        for j in range(count):
            p=a+(b-a)*(j+.5)/count
            if name=='Fuel line 4' and p.x<11:p.x=11.15
            add('PipeSupport',p.x,0,p.y,yaw,group='Pipes',name=f'{name or "Pipe"} support {j+1}')
def pipeline(points):
    r=.35
    for i in range(1,len(points)-1):
        p=Vector(points[i]);arms=[(Vector(points[j])-p).normalized() for j in [i-1,i+1]]
        for yaw in [0,90,180,270]:
            a=math.radians(yaw);rot=lambda x,z:Vector((x*math.cos(a)+z*math.sin(a),-x*math.sin(a)+z*math.cos(a)))
            trial=[rot(0,-1),rot(1,0)]
            if all(any((v-t).length<.01 for t in trial) for v in arms):break
        add('PipeElbow90',p.x,.77,p.y,yaw,group='Pipes')
    for i,(aa,bb) in enumerate(zip(points,points[1:])):
        a,b=Vector(aa),Vector(bb);d=(b-a).normalized()
        if i>0:a+=d*r
        if i<len(points)-2:b-=d*r
        if abs(a.y-3.6)<.001 and abs(b.y-3.6)<.001:
            pipe_segment(a,(10.65,3.6),'Tank manifold left')
            pipe_segment((11.35,3.6),b,'Tank manifold right')
        else:pipe_segment(a,b,f'Fuel line {i+1}')
pipeline([[4,4.4],[4,3.6],[14.3,3.6],[14.3,-4],[9.2,-4]])
pipe_segment((11,3.95),(11,4.4),'Tank 2 outlet',False)
add('PipeTee',11,.77,3.6,group='Pipes')
for a,b in [((-15.4,-10),(-15.4,10)),((15.4,-10),(15.4,10)),((-14,11.9),(14,11.9)),((-14,-11.9),(-6,-11.9)),((6,-11.9),(14,-11.9))]:
    line_instances('RailSection4m',a,b,4,'Safety')
    dx,dz=b[0]-a[0],b[1]-a[1];count=round(math.hypot(dx,dz)/4)
    for i in range(count+1):add('RailPost',a[0]+dx*i/count,0,a[1]+dz*i/count,group='Safety')
for x,z in [(-5.4,-11.75),(5.4,-11.75),(-15.4,10.8),(15.4,10.8)]:add('DockBeacon',x,0,z,group='Lights')
add('WorkLamp',10.5,0,-3,group='Lights');add('Sign04',-4.15,0,6.4,group='Lights')
add('PostTerminal',-12,0,3.05,group='Service');add('RepairTerminal',-5.6,0,3.05,group='Service')
add('Workbench',-14.25,0,7.5,90,group='Props');add('ToolCase',-11.6,1.10,7.5,90,group='Interior')
add('Canister',-12.9,0,3.4,25,group='Props');add('Radio',-10.05,1.08,4.55,group='Props')
add('SpareSeal',-8.15,1.08,4.55,group='Props');add('Spanner',-14.22,1.02,7.5,30,group='Props')
add('VentPanel',-12.7,2.05,4.98,group='Service');add('ParkingDecal',0,.018,-5,group='Decals')
manifest={'version':2,'title':'Fuel stop 04','units':'metres','atlas':'FuelStop04_Atlas.png','textureCount':1,'materialCount':1,
    'approval':'User approved plan and concept, 2026-11-06. Vehicle and character excluded.',
    'deckSize':[32,25],'parkingSize':[10,12],'modelBudgetMinutes':15,'assets':catalog,'instances':instances,
    'anchors':[{'name':'Parking centre +Z forward','position':[0,1.04,-5]}, {'name':'Courier exit','position':[-3.4,.08,-5]},
        {'name':'Deck level','position':[0,0,0]}, {'name':'Parcel handover','position':[-9,1.08,4.55]},
        {'name':'Resident reference only','position':[-9,.08,6.6]}, {'name':'Post interaction','position':[-12,0,3.05]},
        {'name':'Cabin entrance','position':[-4.5,.08,9.35]},
        {'name':'Fuel interaction','position':[8.55,0,-4.1]}, {'name':'Repair interaction','position':[-5.6,0,3.05]},
        {'name':'Cruise arrival','position':[0,35,-210]}],
    'parcelRoute':[ [0,-9.4],[-7,-9.4],[-7,1.4],[-9,1.4],[-9,3.3] ],
    'fuelRoute':[[-3.4,-5],[-3.4,2],[7.2,2],[7.2,-3.2]]}
(SRC/'FuelStop04Manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2))
shutil.copy2(SRC/'FuelStop04Manifest.json',CONTENT/'FuelStop04Manifest.json')

# Editable assembled Blender source, sharing mesh data and the one material.
assembly=bpy.data.collections.new('Fuel 04 assembled');bpy.context.scene.collection.children.link(assembly)
for item in instances:
    base=objects[item['asset']];obj=bpy.data.objects.new(item['name'],base.data);assembly.objects.link(obj)
    x,y,z=item['position'];obj.location=(x,z,y);obj.rotation_euler.z=-math.radians(item['rotation'][1]);sx,sy,sz=item['scale'];obj.scale=(sx,sz,sy)
assembly_scene=bpy.data.scenes.new('FuelStop04');assembly_scene.collection.children.link(assembly)
previous_scene=bpy.context.window.scene;bpy.context.window.scene=assembly_scene;bpy.context.view_layer.update()
bpy.data.libraries.write(str(SRC/'FuelStop04_Assembly.blend'),{assembly_scene},path_remap='RELATIVE_ALL',compress=True)
bpy.context.window.scene=previous_scene
bpy.data.scenes.remove(assembly_scene)

if not args.no_render:
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24
    scene.render.resolution_x=1440;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
    if not scene.world:scene.world=bpy.data.worlds.new('Quiet space')
    scene.world.color=(.004,.005,.008);scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
    def area(name,pos,target,power,size,color):
        data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;data.color=color
        obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=pos;obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
    area('Soft overhead',(-12,-12,24),(0,0,0),4400,22,(.78,.85,.9))
    area('Back silhouette',(12,18,18),(0,5,2),2400,12,(.62,.73,.8))
    area('Service warmth',(-9,3.9,2.9),(-9,0,0),160,2.3,(1,.65,.29))
    area('Pump work light',(10.5,-3.7,2.8),(8.5,-5,0),50,.7,(1,.7,.38))
    camera=bpy.data.objects.new('Concept camera',bpy.data.cameras.new('Concept camera'));scene.collection.objects.link(camera)
    camera.location=(25,-43,30);camera.rotation_euler=(Vector((0,0,1.7))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=45;scene.camera=camera
    scene.render.filepath=str(OUT/'FuelStop04_assembled.png');bpy.ops.render.render(write_still=True)
    camera.location=(0,0,52);camera.rotation_euler=(0,0,0);camera.data.ortho_scale=36;camera.rotation_euler=(Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.resolution_x=1200;scene.render.resolution_y=1000;scene.render.filepath=str(OUT/'FuelStop04_top.png');bpy.ops.render.render(write_still=True)
    # A real 3D kit spread, excluding assembly and excluding all reference characters.
    assembly.hide_render=True
    for i,(name,obj) in enumerate(objects.items()):
        obj.hide_render=False;obj.hide_set(False);obj.location=((i%6)*10-25,(i//6)*11-22,0)
    camera.location=(32,-64,67);camera.rotation_euler=(Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=76
    scene.render.resolution_x=1600;scene.render.resolution_y=1400;scene.render.filepath=str(OUT/'FuelStop04_kit.png');bpy.ops.render.render(write_still=True)
print('FUEL04_DONE',len(catalog),'models',len(instances),'instances',flush=True)
