"""Independent fresh-import and GLB container checks for authored packaging exports."""
import bpy, bmesh, json, struct, math, hashlib, sys
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parent
SPECS={
'bb_set_coffee_tin':(.12,.16,.12),'bb_set_food_jar':(.11,.14,.11),
'bb_set_cereal_carton':(.20,.30,.07),'bb_set_snack_blue':(.16,.22,.05),
'bb_set_snack_gold':(.16,.22,.05),'bb_set_hoh_snack_blue':(.14,.20,.05),
'bb_set_hoh_snack_gold':(.14,.20,.05),'bb_set_game_strategy':(.30,.05,.20),
'bb_set_game_alliance':(.30,.05,.20),'bb_set_game_social':(.30,.05,.20),
'bb_set_toiletry':(.06,.10,.06),'bb_set_cosmetic':(.06,.10,.06)}

def glb_report(path):
    data=path.read_bytes();magic,version,length=struct.unpack_from('<4sII',data)
    assert magic==b'glTF' and version==2 and length==len(data)
    offset=12;chunks={}
    while offset<len(data):
        size,kind=struct.unpack_from('<I4s',data,offset);offset+=8
        chunks[kind]=data[offset:offset+size];offset+=size
    doc=json.loads(chunks[b'JSON']);binary=chunks[b'BIN\x00']
    assert not doc.get('animations') and not doc.get('skins') and not doc.get('cameras')
    assert not any(n.get('extensions',{}).get('KHR_lights_punctual') for n in doc.get('nodes',[]))
    assert all('uri' not in b for b in doc['buffers'])
    assert len(doc.get('meshes',[]))==1
    images=[]
    for image in doc.get('images',[]):
        assert 'uri' not in image and image.get('mimeType')=='image/png'
        bv=doc['bufferViews'][image['bufferView']];start=bv.get('byteOffset',0)
        raw=binary[start:start+bv['byteLength']]
        assert raw[:8]==b'\x89PNG\r\n\x1a\n'
        width,height=struct.unpack_from('>II',raw,16)
        assert width==height==512
        images.append({'width':width,'height':height,'sha256':hashlib.sha256(raw).hexdigest()})
    tris=0;attrs=[]
    for primitive in doc['meshes'][0]['primitives']:
        assert primitive.get('mode',4)==4
        count=doc['accessors'][primitive['indices']]['count'];assert count%3==0;tris+=count//3
        attributes=primitive['attributes'];assert all(a in attributes for a in ['POSITION','NORMAL','TEXCOORD_0','TANGENT'])
        attrs.append(sorted(attributes))
    assert tris<=2500
    assert len(doc.get('materials',[]))<=3
    assert any('baseColorTexture' in m.get('pbrMetallicRoughness',{}) for m in doc['materials'])
    assert any('metallicRoughnessTexture' in m.get('pbrMetallicRoughness',{}) for m in doc['materials'])
    return {'triangles':tris,'materials':len(doc['materials']),'embeddedImages':images,'attributes':attrs,'byteLength':len(data),'sha256':hashlib.sha256(data).hexdigest()}

def imported_report(path,expected):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if path.suffix=='.glb':bpy.ops.import_scene.gltf(filepath=str(path))
    else:bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
    obs=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert len(obs)==1,(path.name,'mesh count',len(obs))
    assert not any(o.type in ['ARMATURE','CAMERA','LIGHT'] for o in bpy.context.scene.objects)
    ob=obs[0];pts=[ob.matrix_world@v.co for v in ob.data.vertices]
    lo=[min(p[i] for p in pts) for i in range(3)];hi=[max(p[i] for p in pts) for i in range(3)]
    size=[hi[i]-lo[i] for i in range(3)];want=[expected[0],expected[2],expected[1]]
    assert max(abs(size[i]-want[i]) for i in range(3))<.00001,(path.name,size,want)
    assert abs(lo[2])<.00001,(path.name,'floor',lo)
    assert max(abs(lo[i]+hi[i]) for i in [0,1])<.00001,(path.name,'centre',lo,hi)
    ob.data.calc_loop_triangles();tris=len(ob.data.loop_triangles)
    assert tris<=2500 and len(ob.data.materials)<=3 and len(ob.data.uv_layers)>=1
    assert all(math.isfinite(float(x)) for p in pts for x in p)
    assert all(math.isfinite(float(x)) for uv in ob.data.uv_layers.active.data for x in uv.uv)
    label_u_span=None
    if path.stem in ['bb_set_coffee_tin','bb_set_food_jar','bb_set_toiletry','bb_set_cosmetic']:
        uv=ob.data.uv_layers.active.data
        label_u_span=max(max(uv[li].uv.x for li in f.loop_indices)-min(uv[li].uv.x for li in f.loop_indices) for f in ob.data.polygons if f.material_index==0)
        assert label_u_span<.05,(path.name,'label crosses cylindrical UV wrap seam',label_u_span)
    zero=sum(1 for f in ob.data.polygons if f.area<1e-12)
    assert zero==0,(path.name,'degenerate faces',zero)
    # Importers split hard normals/UV seams, so weld a temporary mesh before testing closure.
    bm=bmesh.new();bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm,verts=bm.verts[:],dist=1e-7)
    boundary=sum(e.is_boundary for e in bm.edges);nonmanifold=sum(not e.is_manifold for e in bm.edges)
    assert boundary==0 and nonmanifold==0,(path.name,'open mesh',boundary,nonmanifold)
    bm.free()
    missing=[];textures=[]
    for m in ob.data.materials:
        if m and m.use_nodes:
            for n in m.node_tree.nodes:
                if n.type=='TEX_IMAGE' and n.image:
                    im=n.image;textures.append({'name':im.name,'size':list(im.size),'packed':bool(im.packed_file)})
                    if im.size[0]==0:missing.append(im.name)
    assert not missing,(path.name,'missing texture',missing)
    return {'meshCount':len(obs),'blenderWorldMin':lo,'blenderWorldMax':hi,'blenderBoundsXYZ':size,'unityBoundsXYZ':[size[0],size[2],size[1]],'originWorld':list(ob.matrix_world.translation),'triangles':tris,'materials':len(ob.data.materials),'uvLayers':len(ob.data.uv_layers),'maxCylindricalLabelTriangleUSpan':label_u_span,'degenerateFaces':zero,'boundaryEdgesAfterSeamWeld':boundary,'nonmanifoldEdgesAfterSeamWeld':nonmanifold,'textureNodes':textures,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}

out=[]
selected=set(sys.argv[sys.argv.index('--')+1:]) if '--' in sys.argv else set()
for name,want in SPECS.items():
    if selected and name not in selected:continue
    entry={'name':name,'expectedUnityXYZ':want,'container':glb_report(ROOT/'GLB'/f'{name}.glb')}
    entry['glbImport']=imported_report(ROOT/'GLB'/f'{name}.glb',want)
    entry['fbxImport']=imported_report(ROOT/'FBX'/f'{name}.fbx',want)
    out.append(entry);print('AUDIT_PASS '+name,flush=True)
(ROOT/'Reports'/'independent-audit.json').write_text(json.dumps({'blender':bpy.app.version_string,'models':out,'count':len(out),'passed':True,'scriptSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'limits':'Fresh Blender imports verify export integrity, not Unity rendering, interaction or human placement acceptance.'},indent=2),encoding='utf-8')
print('ALL_PACKAGING_EXPORTS_PASS',flush=True)
