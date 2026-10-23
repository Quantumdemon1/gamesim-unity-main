"""Original GameSim prop candidates. No project import or external source assets.

Run in Blender 5.2:
  blender --background --factory-startup --threads 2 --python-exit-code 1 
    --python author_appliances_v2.py -- mini_fridge <ABSENT_OUTPUT_DIRECTORY>
  substitute headphone_case for the second asset.

All dimensions are metres. Blender front -Y, up Z. FBX uses the project's
bb_export.py settings. The closed prop is one static mesh, one atlas material.
Editable parts and deterministic source are retained in the .blend and script.
"""
import sys
sys.dont_write_bytecode = True
import bpy
import bmesh
import json
import math
import hashlib
import pathlib
import numpy as np
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:]
skip_preview = '--skip-preview' in args
args = [a for a in args if a != '--skip-preview']
if len(args) != 2 or args[0] not in ('mini_fridge', 'headphone_case'):
    raise SystemExit('Expected asset name and absent output directory')
kind, raw_output = args
out = pathlib.Path(raw_output).resolve()
if out.exists():
    raise RuntimeError('Refuse existing output directory: ' + str(out))
out.mkdir(parents=True)
textures = out / 'Textures'
textures.mkdir()
name = 'bb_set_' + kind
scene = bpy.context.scene
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
scene.unit_settings.length_unit = 'METERS'
parts = []
part_notes = []

def write_json(path, value):
    with path.open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2, sort_keys=True, allow_nan=False)
        stream.write('\n')

def image_map(suffix, values, color=False):
    image = bpy.data.images.new(name + '_' + suffix, width=1024, height=1024, alpha=True)
    image.colorspace_settings.name = 'sRGB' if color else 'Non-Color'
    image.pixels.foreach_set(values.astype(np.float32).ravel())
    image.update()
    image.file_format = 'PNG'
    image.filepath_raw = str(textures / (name + '_' + suffix + '.png'))
    image.save()
    return image

def make_material():
    if kind == 'mini_fridge':
        colors = [(0.14,.18,.22),(.045,.09,.13),(.46,.34,.17),(.018,.024,.029),
                  (.033,.047,.06),(.53,.58,.61),(.10,.42,.34),(.26,.32,.36)]
        metal = [0.62,.35,.80,0,0,.90,.05,.22]
        rough = [.32,.30,.28,.72,.64,.30,.35,.48]
    else:
        colors = [(.045,.105,.125),(.022,.045,.058),(.44,.32,.15),(.014,.018,.022),
                  (.10,.20,.22),(.40,.43,.45),(.08,.12,.14),(.22,.28,.30)]
        metal = [0,0,.85,0,0,.80,0,0]
        rough = [.82,.78,.32,.72,.82,.35,.88,.78]
    y, x = np.mgrid[0:1024, 0:1024]
    tile = (x // 256) + (y // 256) * 4
    tile %= 8
    noise = (((x * 73856093) ^ (y * 19349663)) & 255) / 255.0 - .5
    weave = .5*np.sin(x * math.pi / 3) * np.cos(y * math.pi / 3)
    rgb = np.array(colors, dtype=np.float32)[tile]
    grain = noise * .018 + np.sin(y * .85) * .006
    if kind == 'headphone_case':
        grain = noise*.012 + weave*.025
    rgb = np.clip(rgb + grain[...,None], 0, 1)
    albedo = np.ones((1024,1024,4), dtype=np.float32)
    albedo[:,:,:3] = rgb
    orm = np.ones_like(albedo)
    orm[:,:,1] = np.clip(np.array(rough)[tile] + noise*.025, .04, 1)
    orm[:,:,2] = np.array(metal)[tile]
    normal = np.ones_like(albedo)
    dx = (np.sin(x*math.pi/3) * np.cos(y*math.pi/3) * .035
          if kind == 'headphone_case' else noise*.008)
    dy = (np.cos(x*math.pi/3) * np.sin(y*math.pi/3) * .035
          if kind == 'headphone_case' else np.sin(y*.85)*.004)
    normal[:,:,0] = .5 + dx*.5
    normal[:,:,1] = .5 + dy*.5
    normal[:,:,2] = .5 + .5*np.sqrt(np.maximum(0, 1-dx*dx-dy*dy))
    unity_metal = np.zeros_like(albedo)
    unity_metal[:,:,0] = orm[:,:,2]
    unity_metal[:,:,3] = 1-orm[:,:,1]
    maps = {'albedo':image_map('albedo',albedo,True), 'orm':image_map('orm',orm),
            'normal':image_map('normal',normal), 'metallic':image_map('metallic',unity_metal)}
    mat = bpy.data.materials.new('bb_mat_' + kind + '_atlas')
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes.get('Principled BSDF')
    base = nodes.new('ShaderNodeTexImage'); base.image=maps['albedo']; base.label='Original color atlas'
    packed = nodes.new('ShaderNodeTexImage'); packed.image=maps['orm']; packed.label='R neutral AO, G roughness, B metal'
    channels = nodes.new('ShaderNodeSeparateColor')
    links.new(base.outputs['Color'],bsdf.inputs['Base Color'])
    links.new(packed.outputs['Color'],channels.inputs['Color'])
    links.new(channels.outputs['Green'],bsdf.inputs['Roughness'])
    links.new(channels.outputs['Blue'],bsdf.inputs['Metallic'])
    normaltex = nodes.new('ShaderNodeTexImage'); normaltex.image=maps['normal']
    normalmap = nodes.new('ShaderNodeNormalMap'); normalmap.inputs['Strength'].default_value=.3
    links.new(normaltex.outputs['Color'],normalmap.inputs['Color'])
    links.new(normalmap.outputs['Normal'],bsdf.inputs['Normal'])
    return mat

material = make_material()

def finish(obj, label, tile):
    obj.name = 'bb_part_' + label
    obj.data.name = obj.name
    obj.data.materials.clear()
    obj.data.materials.append(material)
    # Box projection in object space, each part isolated inside an atlas tile.
    mesh = obj.data
    mesh.update()
    layer = mesh.uv_layers.new(name='UVMap') if not mesh.uv_layers else mesh.uv_layers[0]
    lo = [min(v.co[a] for v in mesh.vertices) for a in range(3)]
    hi = [max(v.co[a] for v in mesh.vertices) for a in range(3)]
    for poly in mesh.polygons:
        major = max(range(3), key=lambda a: abs(poly.normal[a]))
        axes = (0,1) if major == 2 else (0,2) if major == 1 else (1,2)
        for loop_index in poly.loop_indices:
            co = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            q = [(co[a]-lo[a])/max(hi[a]-lo[a],1e-8) for a in axes]
            layer.data[loop_index].uv = ((tile%4+.025+q[0]*.95)/4,(tile//4+.025+q[1]*.95)/4)
    parts.append(obj)
    part_notes.append({'name':obj.name,'atlas_tile':tile,'vertices':len(mesh.vertices)})
    return obj

def activate(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active=obj

def box(label, dims, at, tile, bevel=.003, segments=2):
    bpy.ops.mesh.primitive_cube_add(size=1, location=at)
    obj=bpy.context.object
    obj.dimensions=dims
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        mod=obj.modifiers.new('Authored edge radius','BEVEL')
        mod.width=min(bevel,min(dims)*.45); mod.segments=segments
        bpy.ops.object.modifier_apply(modifier=mod.name)
        normal=obj.modifiers.new('Weighted surface normals','WEIGHTED_NORMAL')
        normal.keep_sharp=True
        bpy.ops.object.modifier_apply(modifier=normal.name)
    return finish(obj,label,tile)

def cylinder(label, radius, depth, at, tile, vertices=20, bevel=.001):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=at)
    obj=bpy.context.object
    if bevel:
        mod=obj.modifiers.new('Authored edge radius','BEVEL'); mod.width=bevel; mod.segments=2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for poly in obj.data.polygons:
        poly.use_smooth=abs(poly.normal.z)<.8
    return finish(obj,label,tile)

def round_rect(width, depth, radius, segments=8):
    result=[]
    for cx,cy,start in ((width/2-radius,depth/2-radius,0),
                        (-width/2+radius,depth/2-radius,90),
                        (-width/2+radius,-depth/2+radius,180),
                        (width/2-radius,-depth/2+radius,270)):
        for i in range(segments):
            angle=math.radians(start+i*90/segments)
            result.append((cx+radius*math.cos(angle),cy+radius*math.sin(angle)))
    return result

def loft(label, rings, tile, segments=8):
    vertices=[]
    for w,d,r,z in rings:
        vertices.extend((x,y,z) for x,y in round_rect(w,d,r,segments))
    n=segments*4; faces=[]
    for ring in range(len(rings)-1):
        for j in range(n):
            faces.append((ring*n+j,ring*n+(j+1)%n,(ring+1)*n+(j+1)%n,(ring+1)*n+j))
    bottom=len(vertices);vertices.append((0,0,rings[0][3]))
    top=len(vertices);vertices.append((0,0,rings[-1][3]))
    for j in range(n):
        faces.append((bottom,(j+1)%n,j))
        k=(len(rings)-1)*n
        faces.append((top,k+j,k+(j+1)%n))
    mesh=bpy.data.meshes.new(label);mesh.from_pydata(vertices,[],faces);mesh.update()
    obj=bpy.data.objects.new(label,mesh);scene.collection.objects.link(obj)
    for poly in mesh.polygons:poly.use_smooth=True
    return finish(obj,label,tile)

def loop_tube(label, width,depth,radius,z,tube,tile,segments=10,sides=4):
    path=round_rect(width,depth,radius,segments)
    verts=[];faces=[];n=len(path)
    for i,(x,y) in enumerate(path):
        prev=Vector(path[(i-1)%n]);nxt=Vector(path[(i+1)%n]);tangent=(nxt-prev).normalized()
        outward=Vector((tangent.y,-tangent.x))
        for j in range(sides):
            a=j*2*math.pi/sides
            verts.append((x+outward.x*tube*math.cos(a),y+outward.y*tube*math.cos(a),z+tube*math.sin(a)))
    for i in range(n):
        for j in range(sides):faces.append((i*sides+j,((i+1)%n)*sides+j,((i+1)%n)*sides+(j+1)%sides,i*sides+(j+1)%sides))
    mesh=bpy.data.meshes.new(label);mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new(label,mesh);scene.collection.objects.link(obj)
    for p in mesh.polygons:p.use_smooth=True
    return finish(obj,label,tile)

if kind=='mini_fridge':
    # Exact overall envelope .50 wide, .50 deep, .70 high, including hardware.
    box('fridge_cabinet',(.50,.444,.665),(0,.028,.3675),0,.016,4)
    box('fridge_door_gasket',(.486,.012,.594),(0,-.196,.39),3,.012,3)
    box('fridge_door',(.478,.036,.58),(0,-.215,.39),1,.012,4)
    box('fridge_door_inset',(.429,.003,.531),(0,-.233,.39),1,.015,3)
    box('fridge_handle',(.018,.008,.190),(.180,-.246,.510),2,.003,3)
    for z in (.428,.592):
        box('fridge_handle_mount_'+str(int(z*1000)),(.022,.019,.015),(.180,-.239,.0+z),2,.003,2)
    for x in (-.19,.19):
        for y in (-.17,.18):
            cylinder('fridge_foot_'+str(int(x*100))+'_'+str(int(y*100)),.024,.035,(x,y,.0175),3,16,.003)
    box('fridge_toe_grille_recess',(.35,.007,.041),(0,-.198,.066),4,.004,2)
    for i in range(12):
        box('fridge_grille_louvre_'+str(i),(.008,.005,.028),(-.148+i*.027,-.202,.066),0,.001,1)
    # Rear heat-exchanger shield and embossed ribs, kept inside the body envelope.
    box('fridge_rear_service_panel',(.38,.003,.365),(0,.2485,.295),4,.006,2)
    for i in range(9):
        box('fridge_rear_rib_'+str(i),(.29,.002,.005),(0,.25-.001,.16+i*.035),7,.001,1)
    for x in (-.173,.173):
        for z in (.135,.455):
            screw=cylinder('fridge_rear_fixing_'+str(x)+'_'+str(z),.004,.002,(x,.250-.001,z),5,12,0)
            screw.rotation_euler=(math.pi/2,0,0)
    box('fridge_status_inset',(.037,.004,.012),(-.16,-.235,.636),3,.004,2)
    box('fridge_status_lens',(.020,.002,.004),(-.16,-.238,.636),6,.001,2)
    # Two small external hinge covers carry a believable serviceable-door detail.
    for z in (.126,.647):
        box('fridge_hinge_'+str(int(z*1000)),(.018,.022,.040),(-.237,-.183,z),0,.004,2)
    budget=6000; expected=(.50,.50,.70)
else:
    # Two separate tailored shell halves, zipper gap, bound rim and actual teeth.
    loft('case_lower_shell',[(.117,.117,.031,0),(.143,.143,.041,.004),(.156,.156,.047,.012),(.158,.158,.048,.025)],0)
    loft('case_upper_shell',[(.158,.158,.048,.034),(.158,.158,.048,.040),(.151,.151,.045,.051),(.133,.133,.038,.058),(.105,.105,.030,.060)],0)
    loft('case_zipper_tape',[(.158,.158,.048,.025),(.158,.158,.048,.034)],1)
    loop_tube('case_lower_piping',.158,.158,.048,.025,.001,4,8,4)
    loop_tube('case_upper_piping',.158,.158,.048,.034,.001,4,8,4)
    path=round_rect(.158,.158,.048,12)
    for i,(x,y) in enumerate(path):
        before=Vector(path[(i-1)%len(path)]);after=Vector(path[(i+1)%len(path)])
        tangent=(after-before).normalized()
        tooth=box('case_zipper_tooth_'+str(i),(.0022,.0014,.0038),(x,y,.0295),3,0)
        tooth.rotation_euler.z=math.atan2(tangent.y,tangent.x)
    # Pull tab lies on the front corner inside the case's maximum plan footprint.
    tab=box('case_zipper_pull',(.019,.007,.0018),(.045,-.068,.031),2,.002,2)
    tab.rotation_euler.z=-.34
    box('case_zipper_slider',(.006,.008,.004),(.034,-.073,.030),2,.001,2)
    # Soft rear hinge binding; its inset placement stays clear of the outer envelope.
    box('case_rear_hinge_binding',(.046,.004,.018),(0,.076,.030),1,.0018,2)
    # A small stitched panel gives the dome an intentional cut-and-sew construction.
    loop_tube('case_top_stitch',.116,.116,.034,.0584,.00035,4,8,3)
    budget=3500; expected=(.16,.16,.06)

# Keep editable semantic parts in a separate .blend before joining the shipping mesh.
for obj in parts:
    activate(obj)
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
for image in list(bpy.data.images):
    if image.filepath_raw and image.filepath_raw.endswith('.png'): image.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(out/(name+'_source.blend')),check_existing=False)

bpy.ops.object.select_all(action='DESELECT')
for obj in parts:obj.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.join()
obj=bpy.context.object;obj.name=name;obj.data.name=name
scene.cursor.location=(0,0,0)
bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
activate(obj)
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
bm=bmesh.new();bm.from_mesh(obj.data)
bmesh.ops.triangulate(bm,faces=[f for f in bm.faces if len(f.verts)>4])
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bm.to_mesh(obj.data);bm.free()
mesh=obj.data;mesh.update();mesh.calc_loop_triangles()
lo=[min((obj.matrix_world@v.co)[i] for v in mesh.vertices) for i in range(3)]
hi=[max((obj.matrix_world@v.co)[i] for v in mesh.vertices) for i in range(3)]
dimensions=[hi[i]-lo[i] for i in range(3)]
if len(mesh.loop_triangles)>budget:raise RuntimeError('Triangle budget exceeded')
if any(dimensions[i]>expected[i]+1e-6 for i in range(3)):raise RuntimeError('Asset exceeds envelope: '+str(dimensions))
if abs(lo[2])>1e-6:raise RuntimeError('Floor origin failed')
if any(abs(lo[i]+hi[i])>1e-5 for i in (0,1)):raise RuntimeError('Bottom-center XY failed: '+str((lo,hi)))
uv_values=[tuple(loop.uv) for loop in mesh.uv_layers.active.data]
if not all(math.isfinite(c) and 0<=c<=1 for uv in uv_values for c in uv):raise RuntimeError('UV failed')
if not all(math.isfinite(c) for v in mesh.vertices for c in v.co):raise RuntimeError('Nonfinite vertex')
zero_area=sum(poly.area<1e-12 for poly in mesh.polygons)
if zero_area:raise RuntimeError('Zero area faces')

fbx=out/(name+'.fbx');glb=out/(name+'.glb')
bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=True,
    object_types={'MESH'},use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,
    add_leaf_bones=False,path_mode='COPY',embed_textures=False,use_custom_props=False,bake_anim=False)
bpy.ops.export_scene.gltf(filepath=str(glb),export_format='GLB',use_selection=True,
    export_animations=False,export_cameras=False,export_lights=False,export_yup=True,export_tangents=True)

receipt={'asset':name,'status':'isolated Blender candidate; no Unity import or visual acceptance',
 'blender':bpy.app.version_string,'front':'Blender -Y; Unity -Z','units':'metres',
 'blender_bounds':{'min':lo,'max':hi,'size':dimensions},
 'unity_envelope_x_y_z':[expected[0],expected[2],expected[1]],
 'triangle_count':len(mesh.loop_triangles),'triangle_budget':budget,'vertex_count':len(mesh.vertices),
 'material_names':[s.material.name for s in obj.material_slots],
 'source_part_count':len(part_notes),'source_parts':part_notes,
 'uv_layers':len(mesh.uv_layers),'uv_finite_and_within_0_1':True,'zero_area_faces':zero_area,
 'root_location':list(obj.location),'root_rotation':list(obj.rotation_euler),'root_scale':list(obj.scale),
 'notes':['Original scripted geometry and texture atlas; no downloads or external imagery.',
          'Closed static dressing prop; no collider, rig, door interaction or simulation logic.',
          'ORM red channel is neutral white; this is not baked contact occlusion.',
          'Unity metallic map uses R metallic and A smoothness; normal is tangent OpenGL +Y.',
          'Preserve importer shader/map assignments; FBX material name alone does not prove URP setup.'],
 'files':{p.name:{'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in (fbx,glb)}}
write_json(out/'technical-receipt.json',receipt)

write_json(out/'provenance.json',{'created_by':'Codex for GameSim user request, 2026-10-20',
 'source':'author_appliances_v2.py; original algorithmic geometry and procedural PBR maps',
 'third_party_assets':[],'paid_services':[],'source_project_read_only':'D:/GamesimConsolidation',
 'source_conventions':'ArtSource/tools/bb_export.py settings; HouseRoomFinish.cs placement envelopes',
 'delivery_status':'External candidate only. No project admission, source freeze change or Unity acceptance.',
 'material_and_parts_editable_in':name+'_source.blend',
 'canonical_glb_candidate':glb.name})

if skip_preview:
    print('CANDIDATE_COMPLETE='+json.dumps({'asset':name,'output':str(out),'triangles':receipt['triangle_count'],'size':dimensions,'tangents_exported':True}))
    raise SystemExit(0)

# Render the exported production geometry in its source scene. CPU only, no UI prefs.
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=24
scene.cycles.use_denoising=True;scene.render.threads_mode='FIXED';scene.render.threads=2
scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
scene.view_settings.view_transform='AgX'
scene.world.color=(.12,.12,.12)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.001))
ground=bpy.context.object;ground.name='Preview_ground'
groundmat=bpy.data.materials.new('Preview_ground_material');groundmat.diffuse_color=(.12,.15,.18,1);ground.data.materials.append(groundmat)
scale=1 if kind=='mini_fridge' else .25
target=Vector((0,0,.35 if kind=='mini_fridge' else .025))
for label,at,energy,size in [('Key',(-1.2,-1.5,2.0),400,1.8),('Fill',(1.6,-.2,1.0),250,1.5),('Rim',(.3,1.4,1.6),450,1.4)]:
    light=bpy.data.lights.new('Preview_'+label,'AREA');light.energy=energy*scale*scale;light.shape='DISK';light.size=size*scale
    lightobj=bpy.data.objects.new('Preview_'+label,light);scene.collection.objects.link(lightobj)
    lightobj.location=Vector(at)*scale;lightobj.rotation_euler=(target-lightobj.location).to_track_quat('-Z','Y').to_euler()
camera_data=bpy.data.cameras.new('Preview_camera');camera=bpy.data.objects.new('Preview_camera',camera_data);scene.collection.objects.link(camera);scene.camera=camera
camera_data.type='ORTHO';camera_data.ortho_scale=.96 if kind=='mini_fridge' else .23
views=[('front',Vector((1.1,-1.5,1.0))*scale),('rear',Vector((-1.1,1.5,1.0))*scale)]
for label,position in views:
    camera.location=position;camera.rotation_euler=(target-position).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(out/(name+'_'+label+'.png'))
    bpy.ops.render.render(write_still=True)

print('CANDIDATE_COMPLETE='+json.dumps({'asset':name,'output':str(out),'triangles':receipt['triangle_count'],'size':dimensions}))
