"""Readable catalog of the delivered models. Thumbnails use individual scale."""
import bpy
import json
import math
from pathlib import Path
from mathutils import Vector

root=Path(__file__).resolve().parents[1];source=root/'ArtSource/FuelStop04'
manifest=json.loads((source/'FuelStop04Manifest.json').read_text())
rows=math.ceil(len(manifest['assets'])/5)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
shared=None;labels=[]
for i,item in enumerate(manifest['assets']):
    name=item['name']
    with bpy.data.libraries.load(str(source/'Models'/f'{name}.blend')) as (src,dst):dst.objects=[name]
    obj=dst.objects[0];scene.collection.objects.link(obj);obj.hide_render=False;obj.hide_set(False)
    if shared is None:shared=obj.data.materials[0]
    else:obj.data.materials.clear();obj.data.materials.append(shared)
    low=Vector([min(v.co[a] for v in obj.data.vertices) for a in range(3)])
    high=Vector([max(v.co[a] for v in obj.data.vertices) for a in range(3)])
    scale=3.8/max(high-low);centre=(low+high)/2
    x=(i%5-2)*6.7;y=((rows-1)*.5-i//5)*7.2
    obj.scale=(scale,)*3;obj.location=(x-centre.x*scale,y-centre.y*scale,-low.z*scale)
    labels.append((f'{i+1:02}  {name}',x,y-2.6))
world=bpy.data.worlds.new('Catalog background');scene.world=world;world.color=(.009,.012,.017)
def area(name,pos,power,size,color):
    light=bpy.data.lights.new(name,'AREA');light.energy=power;light.size=size;light.color=color
    obj=bpy.data.objects.new(name,light);scene.collection.objects.link(obj);obj.location=pos;obj.rotation_euler=(Vector((0,0,0))-obj.location).to_track_quat('-Z','Y').to_euler()
area('Key',(-12,-10,30),18000,25,(1,.9,.78));area('Fill',(15,8,22),11000,24,(.7,.83,1))
camera=bpy.data.objects.new('Catalog camera',bpy.data.cameras.new('Catalog camera'));scene.collection.objects.link(camera)
camera.location=(0,-45,67);camera.rotation_euler=(Vector((0,0,1))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=max(46,rows*7.2+5);scene.camera=camera
textmat=bpy.data.materials.new('Preview captions only');textmat.use_nodes=True
nodes=textmat.node_tree.nodes;nodes.clear();emission=nodes.new('ShaderNodeEmission');emission.inputs[0].default_value=(.7,.75,.78,1);emission.inputs[1].default_value=1
output=nodes.new('ShaderNodeOutputMaterial');textmat.node_tree.links.new(emission.outputs[0],output.inputs[0])
for label,x,y in labels:
    data=bpy.data.curves.new(label,'FONT');data.body=label;data.align_x='CENTER';data.size=.28
    obj=bpy.data.objects.new(label,data);scene.collection.objects.link(obj);obj.location=(x,y,.05);obj.rotation_euler=camera.rotation_euler;data.materials.append(textmat)
scene.render.engine='CYCLES';scene.cycles.samples=24
scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
scene.render.resolution_x=1700;scene.render.resolution_y=rows*300;scene.render.resolution_percentage=100
scene.render.filepath=str(root/'Builds/FuelStop04/FuelStop04_catalog.png')
bpy.ops.render.render(write_still=True)
print('FUEL04_CATALOG_RENDERED',flush=True)
