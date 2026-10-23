import bpy
import json
import math
import pathlib
import hashlib
import time
from io_scene_fbx import parse_fbx

here=pathlib.Path(__file__).resolve().parent
root=here.parent.parent
manifest_path=root/'competition-code'/'part-manifest.json'
manifest=json.loads(manifest_path.read_text(encoding='utf-8-sig'))
parts_dir=root/'competition-art-02'/'Parts'
report=here/'validation-01.json'
if report.exists():raise RuntimeError('Refuse existing report')
start=time.monotonic(); rows=[]; failures=[]
expected={'bb_comp_'+e['meshName']+'.fbx' for e in manifest['entries']}
actual={p.name for p in parts_dir.glob('*.fbx')}
if actual!=expected:raise RuntimeError('Exact manifest/FBX file set mismatch')
for entry in manifest['entries']:
    if time.monotonic()-start>45:raise RuntimeError('Bounded check exceeded 45 seconds')
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path=parts_dir/('bb_comp_'+entry['meshName']+'.fbx')
    before=hashlib.sha256(path.read_bytes()).hexdigest()
    bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
    mesh_objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    errors=[]
    if len(mesh_objects)!=1:
        errors.append('expected exactly one mesh object')
        failures.append({'file':path.name,'errors':errors});continue
    obj=mesh_objects[0];mesh=obj.data;mesh.calc_loop_triangles()
    raw=[tuple(v.co) for v in mesh.vertices]
    world=[tuple(obj.matrix_world@v.co) for v in mesh.vertices]
    def bounds(points):
        lo=[min(v[i] for v in points) for i in range(3)]
        hi=[max(v[i] for v in points) for i in range(3)]
        return {'min':lo,'max':hi,'center':[(a+b)/2 for a,b in zip(lo,hi)],'size':[b-a for a,b in zip(lo,hi)]}
    raw_bounds=bounds(raw);world_bounds=bounds(world)
    for label,box in [('raw mesh',raw_bounds),('world',world_bounds)]:
        if any(abs(v) > 1e-5 for v in box['center']) or any(abs(v-1)>1e-5 for v in box['size']):errors.append(label+' not normalized unit bounds')
    material_slots=len(mesh.materials);material_indices=sorted({p.material_index for p in mesh.polygons})
    if material_slots!=1 or material_indices!=[0]:errors.append('not exactly one material/submesh')
    if not mesh.uv_layers:errors.append('no UV layer')
    uv_finite=bool(mesh.uv_layers) and all(math.isfinite(c) for loop in mesh.uv_layers.active.data for c in loop.uv)
    uv_range=bool(mesh.uv_layers) and all(-1e-5<=c<=1+1e-5 for loop in mesh.uv_layers.active.data for c in loop.uv)
    normals_finite=all(math.isfinite(c) for n in mesh.corner_normals for c in n.vector)
    normals_unit=all(abs(n.vector.length-1)<1e-4 for n in mesh.corner_normals)
    if not uv_finite or not normals_finite or not normals_unit:errors.append('nonfinite UV/normals or nonunit normals')
    if len(mesh.loop_triangles)>3000:errors.append('part triangle budget exceeded')
    degenerate=sum(t.area<1e-12 for t in mesh.loop_triangles)
    if degenerate:errors.append('degenerate triangles')
    # Inspect the actual export axis metadata and untransformed FBX control points.
    tree,version=parse_fbx.parse(str(path))
    globals={};geom_positions=[]
    def visit(node,in_globals=False):
        active=in_globals or node.id==b'GlobalSettings'
        if active and node.id==b'P' and node.props:
            key=node.props[0].decode() if isinstance(node.props[0],bytes) else str(node.props[0])
            if key in ('UpAxis','UpAxisSign','FrontAxis','FrontAxisSign','CoordAxis','CoordAxisSign','UnitScaleFactor'):
                globals[key]=list(node.props[4:])
        if node.id==b'Vertices':geom_positions.extend(node.props[0])
        for child in node.elems:visit(child,active)
    visit(tree)
    binary_raw=[tuple(geom_positions[i:i+3]) for i in range(0,len(geom_positions),3)]
    fbxbounds=bounds(binary_raw)
    row={'name':entry['meshName'],'family':entry['family'],'partKind':entry['partKind'],
      'file':str(path),'sha256':before,'triangles':len(mesh.loop_triangles),'vertices':len(mesh.vertices),
      'material_slots':material_slots,'used_material_indices':material_indices,
      'raw_mesh_bounds':raw_bounds,'world_bounds':world_bounds,'fbx_geometry_bounds':fbxbounds,
      'object_location':list(obj.location),'object_rotation_euler':list(obj.rotation_euler),
      'object_scale':list(obj.scale),'matrix_world':[list(r) for r in obj.matrix_world],
      'raw_mesh_vertex_mean':[sum(v[i] for v in raw)/len(raw) for i in range(3)],
      'world_vertex_mean':[sum(v[i] for v in world)/len(world) for i in range(3)],
      'fbx_axis_units':globals,'uv_finite':uv_finite,'uv_within_0_1':uv_range,
      'normals_finite':normals_finite,'normals_unit':normals_unit,'degenerate_triangles':degenerate,'errors':errors}
    rows.append(row)
    if errors:failures.append({'file':path.name,'errors':errors})
    if hashlib.sha256(path.read_bytes()).hexdigest()!=before:raise RuntimeError('Source changed during check')
result={'status':'PASS' if not failures else 'FAIL','elapsed_seconds':time.monotonic()-start,
        'blender':bpy.app.version_string,'manifest_sha256':hashlib.sha256(manifest_path.read_bytes()).hexdigest(),
        'expected_file_count':len(expected),'checked_count':len(rows),'failures':failures,'parts':rows,
        'scope':'Independent Blender import and direct FBX axis/geometry inspection; not a Unity importer acceptance test.'}
with report.open('x',encoding='utf-8') as stream:json.dump(result,stream,indent=2,sort_keys=True)
print('COMPETITION_PART_CHECK='+json.dumps({k:result[k] for k in ('status','elapsed_seconds','checked_count','failures')}))
print('ASYMMETRIC_SAMPLE='+json.dumps(next(r for r in rows if r['partKind']=='ConsoleHousing')))
if failures:raise RuntimeError('One or more independent part checks failed')
