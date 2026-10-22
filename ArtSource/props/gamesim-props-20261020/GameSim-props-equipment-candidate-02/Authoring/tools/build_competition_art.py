"""Original GameSim instrument geometry. Run only into a new isolated directory.

Unity design coordinates are (X width, Y height, Z depth); Blender uses
(X, -Z, Y). Every reusable mesh is authored in metres, then normalized by its
OWN reference dimensions. Runtime owns all text, color, fitting and movement.
Normalized mesh origins deliberately sit at the part centre (not the floor).
"""
import argparse, hashlib, json, math, sys
from pathlib import Path
import bpy, bmesh
from mathutils import Vector

def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def bvec(v): return Vector((v[0], -v[2], v[1]))
def setup():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system='METRIC'
    bpy.context.scene.unit_settings.scale_length=1
    bpy.context.scene.world=bpy.data.worlds.new('Review World')

def material(name, color, metal=0, rough=.4):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    return m

def cube_piece(dims, center=(0,0,0), bevel=.002, segments=2):
    bm=bmesh.new();bmesh.ops.create_cube(bm,size=1)
    ds=bvec((dims[0],dims[1],-dims[2]))
    for v in bm.verts:v.co=Vector((v.co.x*ds.x,v.co.y*ds.y,v.co.z*ds.z))+bvec(center)
    if bevel>0:bmesh.ops.bevel(bm,geom=list(bm.edges),offset=min(bevel,min(dims)*.24),segments=segments,affect='EDGES',clamp_overlap=True)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    me=bpy.data.meshes.new('piece');bm.to_mesh(me);bm.free()
    return me

def combine(parts,name):
    verts=[];faces=[]
    for me in parts:
        off=len(verts);verts.extend(tuple(v.co) for v in me.vertices)
        faces.extend(tuple(off+i for i in p.vertices) for p in me.polygons)
        bpy.data.meshes.remove(me)
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.triangulate(bm,faces=list(bm.faces));bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
    # Explicit per-face box projection. These untextured kit meshes retain UVs
    # for normal/roughness authoring without depending on a UI context operator.
    uv=me.uv_layers.new(name='UVMap')
    for p in me.polygons:
        axis=max(range(3),key=lambda a:abs(p.normal[a]));axes=[a for a in range(3) if a!=axis]
        for li in p.loop_indices:
            co=me.vertices[me.loops[li].vertex_index].co
            uv.data[li].uv=(co[axes[0]],co[axes[1]])
    return me

def build_part(e):
    w,h,d=e['referenceDimensions']; role=e['partKind']; parts=[]
    bevel=min(w,h,d)*.14
    if role in ('ConsoleHousing','Counterweight'):
        # A stepped equipment casing; upper shell and narrower lower plinth
        # share the full reserved envelope. Back access lid and grill ribs are
        # inset so they cannot enlarge the actor/station clearance reservation.
        parts.append(cube_piece((w,h,d*.93),(0,0,-d*.035),bevel,3))
        parts.append(cube_piece((w*.83,h*.78,d*.07),(0,0,d*.465),bevel*.4))
        for i in range(7):
            parts.append(cube_piece((w*.48,h*.014,d*.025),(0,h*(.20-i*.055),d*.4875),min(h*.004,d*.004)))
    elif role in ('RigUpright','ConsoleLeg','TrayPedestal'):
        # Extruded channel with full-width end collars. The runtime intentionally
        # changes upright/pedestal height; detailing is safe under that operation.
        parts.append(cube_piece((w*.80,h,d*.84),(0,0,0),bevel,2))
        collar=min(h*.045,min(w,d)*.45)
        for s in (-1,1):parts.append(cube_piece((w,collar,d),(0,s*(h-collar)*.5,0),bevel*.5))
        parts.append(cube_piece((w*.30,h*.90,d*.12),(0,0,d*.44),bevel*.35))
    elif role=='GripHandle':
        # Flat uninterrupted front contact plane at the runtime's two hand points.
        parts.append(cube_piece((w,h,d),(0,0,0),min(h,d)*.18,3))
        # Inset shallow grip marks on top, away from the front contact face.
        for s in (-1,1):
            for i in range(5):
                x=s*w*(.26+i*.022)
                parts.append(cube_piece((w*.007,h*.035,d*.45),(x,h*.4825,0),0))
    elif role=='Die':
        # Numbered faces remain the real game's readout. No decorative random pips.
        parts.append(cube_piece((w,h,d),bevel=min(w,h,d)*.11,segments=4))
    elif role in ('MemoryTile','LetterTile','SignalTarget','ResponsePad'):
        # Double-level keycap with an opaque central readout/contact face.
        parts.append(cube_piece((w,h,d*.58),(0,0,d*.21),bevel*.60,3))
        parts.append(cube_piece((w*.92,h*.92,d*.42),(0,0,-d*.29),bevel*.45,3))
        if role=='ResponsePad':
            # Preserve exact top-centre surface used by the hand press.
            parts=[cube_piece((w,h,d),bevel=min(w,h,d)*.13,segments=3)]
    elif role=='TrayBed':
        parts.append(cube_piece((w,h*.65,d),(0,-h*.175,0),bevel,3))
        parts.append(cube_piece((w*.94,h*.35,d*.91),(0,h*.325,0),bevel*.5,3))
    else:
        parts.append(cube_piece((w,h,d),bevel=bevel,segments=3 if role in ('RigBrace','TrayBackRail','TraySideRail') else 2))
    name='bb_comp_'+e['meshName'];me=combine(parts,name)
    # Convert real-metre Blender axes to unit local coordinates, keeping the
    # reference dimensions outside the mesh as the explicit runtime contract.
    for v in me.vertices:v.co=Vector((v.co.x/w,v.co.y/d,v.co.z/h))
    # Unit UVs are sufficient for the single material; authoring projections above
    # remain dimension-aware until geometry normalization.
    for uv in me.uv_layers.active.data:uv.uv=(uv.uv.x+.5,uv.uv.y+.5)
    me.update();ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob)
    ob.data.materials.append(material(name+'_neutral',(.16,.21,.29),.35,.38))
    return ob

def select(objects):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    if objects:bpy.context.view_layer.objects.active=objects[0]

def export_fbx(o,path):
    select([o]);bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=True,
        object_types={'MESH'},use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,
        add_leaf_bones=False,path_mode='COPY',embed_textures=False,use_custom_props=False)

def placements(f):
    rows=[]
    def p(k,x,y,z,label=None): rows.append((k,(x,y,z),label or k))
    p('StanceRail',-.30,.016,.02,'Left stance rail');p('StanceRail',.30,.016,.02,'Right stance rail')
    front=.56
    if f in ('Signals','PairConsole','WordConsole'):
        h,w={'Signals':(1.20,.95),'PairConsole':(1.27,.92),'WordConsole':(1.22,1.02)}[f]
        p('ConsoleLeg',-w*.37,.42,front+.08,'Left leg');p('ConsoleLeg',w*.37,.42,front+.08,'Right leg')
        p('ConsoleHousing',0,h,front+.04);p('RecessedDisplay',0,h,front-.039);p('AwardHeader',0,h+.38,front-.04)
        if f=='Signals':
            for i,(x,y) in enumerate([(0,.23),(.25,0),(0,-.23),(-.25,0)]):p('SignalTarget',x,1.22+y,front-.065,'Signal '+str(i))
            p('ResponsePad',0,1.16,.39)
        elif f=='PairConsole':
            for i in range(16):p('MemoryTile',(i%4-1.5)*.19,1.28-(i//4-1.5)*.16,front-.064,'Memory '+str(i))
        else:
            for i in range(12):p('LetterTile',(i%6-2.5)*.145,1.3 if i<6 else 1.08,front-.064,'Letter '+str(i))
    elif f=='GripRig':
        for x in (-.42,.42):p('RigUpright',x,.78,front,'Upright '+str(x))
        for k,xyz in {'RigBrace':(0,1.56,front),'GripHandle':(0,1.16,front-.055),'GripTrack':(.30,1.09,front-.075),'GripRemaining':(.30,1.09,front-.085),'CounterweightGuide':(0,1.10,front+.15),'Counterweight':(0,.85,front+.15)}.items():p(k,*xyz)
        for i in range(5):p('GripTick',.35,.77+i*.16,front-.085,'Tick '+str(i))
    else:
        for k,xyz in {'TrayPedestal':(0,.55,front),'TrayBed':(0,1.14,front),'TrayBackRail':(0,1.21,front+.20)}.items():p(k,*xyz)
        for x in (-.455,.455):p('TraySideRail',x,1.21,front,'Side rail '+str(x))
        for i in range(3):p('Die',(i-1)*.25,1.28,front-.115,'Die '+str(i))
    ph=1.73 if f=='GripRig' else .74
    pz=front-(.145 if f=='DiceTray' else .12 if f=='GripRig' else .08)
    p('ProgressBacking',0,ph,pz+.006,'Front progress backing');p('ProgressBacking',0,ph,front+.154,'Audience progress backing')
    return rows

def preview(f,objects,dest):
    sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=20;sc.cycles.use_denoising=True
    sc.render.threads_mode='FIXED';sc.render.threads=2
    sc.render.resolution_x=960;sc.render.resolution_y=960;sc.render.resolution_percentage=100
    sc.world.color=(.18,.18,.18)
    floor=bpy.data.objects.new('preview_floor',cube_piece((5,.025,5),(0,-.025,0),0));bpy.context.collection.objects.link(floor)
    floor.data.materials.append(material('preview_floor_mat',(.075,.09,.12),0,.8))
    lights=[]
    for at,power,size in [((2.5,-3.8,4),650,4),((-2,-1,2.8),400,3),((1,3,3.8),800,3)]:
        data=bpy.data.lights.new('Review softbox','AREA');data.energy=power;data.shape='DISK';data.size=size
        ob=bpy.data.objects.new(data.name,data);bpy.context.collection.objects.link(ob);ob.location=at;ob.rotation_euler=(Vector((0,0,1))-ob.location).to_track_quat('-Z','Y').to_euler();lights.append(ob)
    cam=bpy.data.objects.new('Review camera',bpy.data.cameras.new('Review camera'));bpy.context.collection.objects.link(cam);sc.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.25
    # Positive Blender Y is the negative-Z contestant-facing side.
    cam.location=(2.2,3.8,2.65);cam.rotation_euler=(Vector((0,-.2,.85))-cam.location).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=str(dest/(f+'-preview.png'));bpy.ops.render.render(write_still=True)
    for o in [floor,cam]+lights:bpy.data.objects.remove(o,do_unlink=True)

def main():
    pa=argparse.ArgumentParser();pa.add_argument('--manifest',required=True);pa.add_argument('--out',required=True);pa.add_argument('--render',action='store_true')
    a=pa.parse_args(sys.argv[sys.argv.index('--')+1:]);out=Path(a.out).resolve()
    if out.exists():raise RuntimeError('Output must be new: '+str(out))
    out.mkdir(parents=True);(out/'Parts').mkdir();(out/'Assemblies').mkdir();(out/'Previews').mkdir()
    manifest=json.loads(Path(a.manifest).read_text(encoding='utf-8-sig'));setup();report=[]
    originals={}
    for e in manifest['entries']:
        o=build_part(e);originals[(e['family'],e['partKind'])]=(o,e)
        lo=[min(v.co[i] for v in o.data.vertices) for i in range(3)];hi=[max(v.co[i] for v in o.data.vertices) for i in range(3)]
        if any(abs(x+.5)>1e-5 for x in lo) or any(abs(x-.5)>1e-5 for x in hi):raise RuntimeError('Wrong bounds: '+o.name+str((lo,hi)))
        o.data.calc_loop_triangles();n=len(o.data.loop_triangles)
        if n>3000:raise RuntimeError('Part triangle budget: '+o.name+' '+str(n))
        path=out/'Parts'/(o.name+'.fbx');export_fbx(o,path)
        report.append(dict(e,fbx=path.name,sha256=sha(path),triangles=n,boundsBlender=[lo,hi],uv=True,submeshes=1))
        o.hide_render=True;o.hide_set(True)
    mats={'Arena graphite':material('bb_mat_arena_graphite',(.105,.16,.23),.5,.38),'Award trim':material('bb_mat_arena_trim',(.10,.65,.81),.55,.3),'Instrument face':material('bb_mat_arena_face',(.022,.04,.065),.1,.5),'Indicator face':material('bb_mat_arena_indicator',(.72,.82,.86),.1,.35)}
    for fam in ['Signals','PairConsole','GripRig','DiceTray','WordConsole']:
        copies=[]
        for role,at,label in placements(fam):
            source,e=originals[(fam,role)];o=bpy.data.objects.new(label,source.data.copy());bpy.context.collection.objects.link(o)
            w,h,d=e['referenceDimensions'];o.scale=(w,d,h);o.location=bvec(at);o.data.materials.clear();o.data.materials.append(mats[e['material']]);copies.append(o)
        select(copies);p=out/'Assemblies'/('bb_comp_'+fam+'.glb')
        bpy.ops.export_scene.gltf(filepath=str(p),export_format='GLB',use_selection=True,export_apply=True,export_yup=True,export_animations=False,export_materials='EXPORT')
        if a.render:preview(fam,copies,out/'Previews')
        for o in copies:bpy.data.objects.remove(o,do_unlink=True)
    select([]);bpy.ops.wm.save_as_mainfile(filepath=str(out/'competition_parts.blend'))
    (out/'authoring-receipt.json').write_text(json.dumps({'blender':bpy.app.version_string,'sourceSha256':sha(__file__),'manifestSha256':sha(a.manifest),'entries':report,'scope':'isolated art; no Unity import or runtime acceptance'},indent=2),encoding='utf-8')
    print('COMPETITION_EXPORT_COMPLETE',len(report),str(out))

if __name__=='__main__':main()
