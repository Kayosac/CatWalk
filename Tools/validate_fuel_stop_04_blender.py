"""Open every delivered Blender source and verify it is directly usable."""
import bpy
import json
import sys
from pathlib import Path
from mathutils import Vector, Euler

root=Path(__file__).resolve().parents[1]
source=root/'ArtSource/FuelStop04'
manifest=json.loads((source/'FuelStop04Manifest.json').read_text())
checks=[]
normalize='--normalize' in sys.argv
bpy.context.preferences.filepaths.save_version=0
def normalize_source(path, objects):
    active=bpy.context.scene
    for scene in list(bpy.data.scenes):
        if scene!=active:bpy.data.scenes.remove(scene)
    points=[obj.matrix_world@Vector(corner) for obj in objects for corner in obj.bound_box]
    low=Vector([min(p[a] for p in points) for a in range(3)]);high=Vector([max(p[a] for p in points) for a in range(3)])
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                space=area.spaces.active;space.region_3d.view_location=(low+high)/2
                space.region_3d.view_distance=max(high-low)*1.65
                space.region_3d.view_rotation=Euler((1.12,0,.36),'XYZ').to_quaternion()
                space.shading.type='MATERIAL'
    bpy.ops.wm.save_as_mainfile(filepath=str(path),check_existing=False,compress=True)
for item in manifest['assets']:
    name=item['name'];path=source/'Models'/f'{name}.blend'
    bpy.ops.wm.open_mainfile(filepath=str(path),load_ui=False,use_scripts=False)
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert len(objects)==1,(name,'not a single-model scene',len(objects))
    obj=objects[0]
    assert obj.name==name and not obj.hide_render,(name,'hidden or misnamed source')
    assert len(obj.data.materials)==1 and len(obj.data.uv_layers)==1,(name,'UV or material count')
    images=[n.image for n in obj.data.materials[0].node_tree.nodes if n.type=='TEX_IMAGE']
    assert len(images)==1 and Path(bpy.path.abspath(images[0].filepath)).resolve()==source/'FuelStop04_Atlas.png',(name,'atlas path')
    assert Path(bpy.path.abspath(images[0].filepath)).is_file(),(name,'missing image')
    obj.data.calc_loop_triangles()
    assert len(obj.data.loop_triangles)==item['triangles'],(name,'triangle count')
    if normalize:normalize_source(path,objects)
    checks.append(name+': opens with one visible mesh, one atlas, intact UVs and expected topology')
bpy.ops.wm.open_mainfile(filepath=str(source/'FuelStop04_Assembly.blend'),load_ui=False,use_scripts=False)
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert len(objects)==len(manifest['instances']),'Assembly object count'
assert len({o.data for o in objects})==len(manifest['assets']),'Instanced mesh sharing'
assert len({m for o in objects for m in o.data.materials})==1,'Assembly material sharing'
if normalize:normalize_source(source/'FuelStop04_Assembly.blend',objects)
checks.append(f'Assembly opens as {len(objects)} mesh instances sharing {len(manifest["assets"])} meshes and one material')
report={'success':True,'checks':checks,'sourceCount':len(manifest['assets']),'assemblyInstances':len(objects)}
(root/'Builds/FuelStop04/source-validation.json').write_text(json.dumps(report,indent=2))
print('FUEL04_SOURCES_VALID',len(checks),flush=True)
