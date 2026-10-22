"""Render the full grip assembly with review framing; exports remain untouched."""
import importlib.util, json
from pathlib import Path
import bpy
from mathutils import Vector

root=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('art',root/'build_competition_art.py')
art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)
art.setup()
manifest=json.loads((root/'competition-code'/'part-manifest.json').read_text(encoding='utf-8'))
parts={}
for entry in manifest['entries']:
    if entry['family']!='GripRig':continue
    ob=art.build_part(entry);ob.hide_render=True;parts[entry['partKind']]=(ob,entry)
mats={'Arena graphite':art.material('frame',(.105,.16,.23),.5,.38),
      'Award trim':art.material('trim',(.10,.65,.81),.55,.3),
      'Instrument face':art.material('face',(.022,.04,.065),.1,.5),
      'Indicator face':art.material('indicator',(.72,.82,.86),.1,.35)}
for role,at,label in art.placements('GripRig'):
    source,entry=parts[role];ob=bpy.data.objects.new(label,source.data.copy());bpy.context.collection.objects.link(ob)
    w,h,d=entry['referenceDimensions'];ob.scale=(w,d,h);ob.location=art.bvec(at)
    ob.data.materials.clear();ob.data.materials.append(mats[entry['material']])
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=16;sc.cycles.use_denoising=True
sc.render.threads_mode='FIXED';sc.render.threads=2
sc.render.resolution_x=960;sc.render.resolution_y=960;sc.render.resolution_percentage=100
sc.world.color=(.18,.18,.18)
floor=bpy.data.objects.new('review floor',art.cube_piece((5,.025,5),(0,-.025,0),0));bpy.context.collection.objects.link(floor)
floor.data.materials.append(art.material('review floor material',(.075,.09,.12),0,.8))
for at,power,size in [((2.5,-3.8,4),650,4),((-2,-1,2.8),400,3),((1,3,3.8),800,3)]:
    light=bpy.data.lights.new('review light','AREA');light.energy=power;light.shape='DISK';light.size=size
    ob=bpy.data.objects.new(light.name,light);bpy.context.collection.objects.link(ob);ob.location=at
    ob.rotation_euler=(Vector((0,0,1))-ob.location).to_track_quat('-Z','Y').to_euler()
cam=bpy.data.objects.new('review camera',bpy.data.cameras.new('review camera'));bpy.context.collection.objects.link(cam);sc.camera=cam
cam.data.type='ORTHO';cam.data.ortho_scale=2.7;cam.location=(2.2,3.8,2.65)
cam.rotation_euler=(Vector((0,-.2,.94))-cam.location).to_track_quat('-Z','Y').to_euler()
sc.render.filepath=str(root/'competition-art-02'/'Previews'/'GripRig-full-preview.png')
bpy.ops.render.render(write_still=True)
print('FULL_GRIP_REVIEW_RENDERED')
