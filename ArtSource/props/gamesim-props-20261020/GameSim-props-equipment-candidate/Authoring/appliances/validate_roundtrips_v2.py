"""Independent fresh-scene FBX/GLB reimports; no GameSim writes."""
import bpy
import json
import math
import pathlib
import hashlib
import struct
import numpy as np

root=pathlib.Path(__file__).resolve().parent
report_path=root/'roundtrip-validation-02.json'
contact_path=root/'candidate-contact-sheet-02.png'
if report_path.exists() or contact_path.exists():
    raise RuntimeError('Refuse existing validation output')
reports=[]
assets=[('mini-fridge-02','bb_set_mini_fridge',6000),('headphone-case-02','bb_set_headphone_case',3500)]
for folder,name,budget in assets:
    base=root/folder
    receipt=json.loads((base/'technical-receipt.json').read_text(encoding='utf-8'))
    expected=receipt['blender_bounds']['size']
    for extension in ('fbx','glb'):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        path=base/(name+'.'+extension)
        payload=path.read_bytes()
        if hashlib.sha256(payload).hexdigest()!=receipt['files'][path.name]['sha256']:
            raise RuntimeError('Export changed since source check')
        if extension=='fbx':bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
        else:bpy.ops.import_scene.gltf(filepath=str(path))
        objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
        coords=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
        lo=[min(v[i] for v in coords) for i in range(3)]
        hi=[max(v[i] for v in coords) for i in range(3)]
        dims=[hi[i]-lo[i] for i in range(3)]
        tris=0;normals=True;uv_valid=True
        for obj in objects:
            obj.data.calc_loop_triangles();tris+=len(obj.data.loop_triangles)
            normals &= all(math.isfinite(c) for v in obj.data.vertices for c in v.normal)
            uv_valid &= bool(obj.data.uv_layers) and all(
                math.isfinite(c) and -1e-6<=c<=1+1e-6
                for loop in obj.data.uv_layers.active.data for c in loop.uv)
        if any(abs(dims[i]-expected[i])>1e-5 for i in range(3)):
            raise RuntimeError('Reimport dimensions changed: '+str((path,dims,expected)))
        if abs(lo[2])>1e-6:raise RuntimeError('Reimport lost floor contact')
        if tris!=receipt['triangle_count'] or tris>budget:raise RuntimeError('Reimport triangle budget failed')
        if not normals or not uv_valid:raise RuntimeError('Invalid normal or UV')
        entry={'file':str(path),'sha256':hashlib.sha256(payload).hexdigest(),'mesh_objects':len(objects),
               'triangles':tris,'bounds_min':lo,'bounds_max':hi,'size_blender_xyz':dims,
               'uvs_present_finite_within_0_1':uv_valid,'normals_finite':normals,
               'materials':sorted({m.name for o in objects for m in o.data.materials if m})}
        image_nodes=[]
        for imported_material in {m for o in objects for m in o.data.materials if m}:
            if not imported_material.use_nodes:continue
            for node in imported_material.node_tree.nodes:
                if node.type=='TEX_IMAGE':
                    img=node.image
                    image_nodes.append({'node':node.name,'image':img.name if img else None,
                        'size':list(img.size) if img else [],'loaded':bool(img and img.has_data),
                        'filepath':img.filepath if img else None})
        if not image_nodes or any(not node['loaded'] or node['size']!=[1024,1024] for node in image_nodes):
            raise RuntimeError('Reimport lost expected texture payload')
        entry['imported_image_nodes']=image_nodes
        if extension=='glb':
            if payload[:4]!=b'glTF' or struct.unpack_from('<I',payload,8)[0]!=len(payload):raise RuntimeError('GLB header failed')
            json_len,json_type=struct.unpack_from('<II',payload,12)
            doc=json.loads(payload[20:20+json_len].decode())
            if any('uri' in img for img in doc.get('images',[])):raise RuntimeError('GLB texture not embedded')
            mat=doc['materials'][0]
            if 'baseColorTexture' not in mat['pbrMetallicRoughness'] or 'metallicRoughnessTexture' not in mat['pbrMetallicRoughness'] or 'normalTexture' not in mat:
                raise RuntimeError('GLB PBR texture binding incomplete')
            entry['glb_embedded_images']=len(doc.get('images',[]))
            entry['glb_material']=mat
        reports.append(entry)


result={'status':'four independent reimports passed','imports':reports,'scope':'No Unity validation'}
with report_path.open('x',encoding='utf-8') as f:json.dump(result,f,indent=2)
print('ROUNDTRIP_COMPLETE='+str(report_path))
