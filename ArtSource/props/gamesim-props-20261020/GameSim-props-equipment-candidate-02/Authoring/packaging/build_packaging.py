"""Original GameSim packaging family, authored locally; no downloaded geometry/textures.

Blender 5.2.2, metres, Z-up, product front -Y, floor centre origin.
FBX settings follow the read-only Gamesim ArtSource/tools/bb_export.py contract.
Run: blender -b --factory-startup -t 2 --python-exit-code 1 -P build_packaging.py
"""
import bpy, bmesh, math, json, hashlib, sys
from pathlib import Path
import numpy as np
from mathutils import Vector

ROOT = Path(__file__).resolve().parent
for p in ['GLB','FBX','Textures','Sources','Previews','Reports']:
    (ROOT/p).mkdir(exist_ok=True)
SEED=20261020
SIZE=512
PALETTE={'graphite':(27,35,42),'cream':(231,220,191),'cyan':(72,144,163),'gold':(184,143,65),'navy':(37,66,83),'light':(212,210,196)}
FONT={
'A':['01110','10001','10001','11111','10001','10001','10001'],
'B':['11110','10001','10001','11110','10001','10001','11110'],
'C':['01111','10000','10000','10000','10000','10000','01111'],
'D':['11110','10001','10001','10001','10001','10001','11110'],
'E':['11111','10000','10000','11110','10000','10000','11111'],
'F':['11111','10000','10000','11110','10000','10000','10000'],
'G':['01111','10000','10000','10111','10001','10001','01110'],
'H':['10001','10001','10001','11111','10001','10001','10001'],
'I':['11111','00100','00100','00100','00100','00100','11111'],
'J':['00111','00010','00010','00010','10010','10010','01100'],
'K':['10001','10010','10100','11000','10100','10010','10001'],
'L':['10000','10000','10000','10000','10000','10000','11111'],
'M':['10001','11011','10101','10101','10001','10001','10001'],
'N':['10001','11001','10101','10011','10001','10001','10001'],
'O':['01110','10001','10001','10001','10001','10001','01110'],
'P':['11110','10001','10001','11110','10000','10000','10000'],
'Q':['01110','10001','10001','10001','10101','10010','01101'],
'R':['11110','10001','10001','11110','10100','10010','10001'],
'S':['01111','10000','10000','01110','00001','00001','11110'],
'T':['11111','00100','00100','00100','00100','00100','00100'],
'U':['10001','10001','10001','10001','10001','10001','01110'],
'V':['10001','10001','10001','10001','10001','01010','00100'],
'W':['10001','10001','10001','10101','10101','10101','01010'],
'X':['10001','10001','01010','00100','01010','10001','10001'],
'Y':['10001','10001','01010','00100','00100','00100','00100'],
'Z':['11111','00001','00010','00100','01000','10000','11111'],
'0':['01110','10001','10011','10101','11001','10001','01110'],
'1':['00100','01100','00100','00100','00100','00100','01110'],
'2':['01110','10001','00001','00010','00100','01000','11111'],
'3':['11110','00001','00001','01110','00001','00001','11110'],
'4':['00010','00110','01010','10010','11111','00010','00010'],
'5':['11111','10000','10000','11110','00001','00001','11110'],
'6':['01110','10000','10000','11110','10001','10001','01110'],
'7':['11111','00001','00010','00100','01000','01000','01000'],
'8':['01110','10001','10001','01110','10001','10001','01110'],
'9':['01110','10001','10001','01111','00001','00001','01110'],
' ':['00000']*7,'-':['00000','00000','00000','11111','00000','00000','00000'],
}

def rect(a,x0,y0,x1,y1,c): a[max(0,int(y0)):min(SIZE,int(y1)),max(0,int(x0)):min(SIZE,int(x1)),:3]=c
def disk(a,cx,cy,r,c):
    yy,xx=np.ogrid[:SIZE,:SIZE]; a[(xx-cx)**2+(yy-cy)**2<=r*r,:3]=c
def line(a,x0,y0,x1,y1,c,width=2):
    n=max(2,int(max(abs(x1-x0),abs(y1-y0))*1.5))
    for t in np.linspace(0,1,n): rect(a,x0+(x1-x0)*t-width,y0+(y1-y0)*t-width,x0+(x1-x0)*t+width+1,y0+(y1-y0)*t+width+1,c)
def words(a,s,y,scale,c,x=None):
    w=len(s)*6*scale-scale; x=(SIZE-w)//2 if x is None else x
    for i,ch in enumerate(s):
        for row,bits in enumerate(FONT.get(ch,FONT[' '])):
            for col,b in enumerate(bits):
                if b=='1': rect(a,x+i*6*scale+col*scale,y+row*scale,x+i*6*scale+(col+1)*scale,y+(row+1)*scale,c)

def make_image(name,arr,kind):
    img=bpy.data.images.new(name+'_'+kind,width=SIZE,height=SIZE,alpha=True)
    if kind!='albedo':img.colorspace_settings.name='Non-Color'
    # Array uses top-left image coordinates; Blender pixel data uses bottom-left.
    rgba=arr[::-1].astype(np.float32)/255
    img.pixels.foreach_set(rgba.ravel())
    texture_path=str(ROOT/'Textures'/f'{name}_{kind}.png')
    img.filepath_raw=texture_path;img.file_format='PNG';img.save()
    # Reload the saved bytes as a file texture so generated-buffer colour handling
    # and imported PNG handling cannot diverge between source and exported models.
    bpy.data.images.remove(img);img=bpy.data.images.load(texture_path,check_existing=False)
    if kind!='albedo':img.colorspace_settings.name='Non-Color'
    img.pack()
    return img

def label(name,title,subtitle,accent,kind,number):
    rng=np.random.default_rng(SEED+number)
    base=np.array(PALETTE['graphite'],dtype=np.int16)
    grain=rng.integers(-3,4,(SIZE,SIZE,1)); a=np.empty((SIZE,SIZE,4),np.uint8);a[:,:,:3]=np.clip(base+grain,0,255);a[:,:,3]=255
    ac=PALETTE[accent];cream=PALETTE['cream'];dark=PALETTE['graphite']
    rect(a,20,18,492,28,ac);rect(a,20,484,492,494,ac)
    for x in [22,488]:rect(a,x,28,x+2,484,ac)
    words(a,'THE HOUSE',53,4,cream)
    words(a,title,110,5 if len(title)<=8 else 4,ac)
    words(a,subtitle,163,2,cream)
    if kind in ['snack','cereal','food']:
        for k in range(13):
            x,y=rng.integers(135,378),rng.integers(235,382); r=int(rng.integers(14,24))
            disk(a,x+2,y+3,r,(92,67,40));disk(a,x,y,r,(194,162,107));disk(a,x-3,y-4,max(3,r-7),(218,190,134))
            if kind=='snack':
                for z in range(4):disk(a,x+int(rng.integers(-r//2,r//2)),y+int(rng.integers(-r//2,r//2)),2,dark)
    elif kind=='game':
        for j in range(5):
            for i in range(7):
                x=116+i*46;y=232+j*33
                if (i+j)%2==0:rect(a,x,y,x+38,y+27,ac)
                else:rect(a,x,y,x+38,y+27,PALETTE['navy'])
        for x,y in [(160,246),(297,278),(342,345)]:disk(a,x,y,13,cream);disk(a,x,y,6,dark)
    elif kind=='coffee':
        for cx,cy in [(192,291),(284,314),(260,245)]:
            yy,xx=np.ogrid[:SIZE,:SIZE];m=((xx-cx)/28)**2+((yy-cy)/42)**2<1;a[m,:3]=(146,103,61);line(a,cx+2,cy-34,cx-3,cy+34,dark,3)
    else:
        for radius in [68,57,45]:
            for t in np.linspace(0,2*math.pi,300):
                x=256+radius*math.cos(t);y=294+radius*math.sin(t);rect(a,x-1,y-1,x+2,y+2,ac)
        words(a,'H',273,6,cream)
    words(a,'HOUSE PANTRY' if kind in ['coffee','snack','food','cereal'] else 'HOUSE COLLECTION',421,2,cream)
    words(a,'SMALL BATCH' if kind!='game' else '2-6 PLAYERS',454,2,ac)
    # Deliberate package-print ink sheen and fine substrate roughness, all original.
    orm=np.full((SIZE,SIZE,4),255,np.uint8);orm[:,:,0]=255;orm[:,:,1]=rng.integers(158,180,(SIZE,SIZE));orm[:,:,2]=0
    ink=np.max(a[:,:,:3],axis=2)>150;orm[ink,1]=140
    return make_image(name,a,'albedo'),make_image(name,orm,'orm')

def mat(name,color,rough=.5,metal=0):
    m=bpy.data.materials.new('bb_mat_'+name);m.use_nodes=True
    n=m.node_tree.nodes.get('Principled BSDF');n.inputs['Base Color'].default_value=(*color,1);n.inputs['Roughness'].default_value=rough;n.inputs['Metallic'].default_value=metal
    return m
def labelmat(name,imgs):
    m=mat(name,(1,1,1));ns=m.node_tree.nodes;ls=m.node_tree.links;p=ns.get('Principled BSDF')
    t=ns.new('ShaderNodeTexImage');t.image=imgs[0];ls.new(t.outputs['Color'],p.inputs['Base Color'])
    d=ns.new('ShaderNodeTexImage');d.image=imgs[1];s=ns.new('ShaderNodeSeparateColor');ls.new(d.outputs['Color'],s.inputs['Color']);ls.new(s.outputs['Green'],p.inputs['Roughness']);ls.new(s.outputs['Blue'],p.inputs['Metallic'])
    return m

def meshobj(name,verts,faces,mats,mis=None,uvfunc=None):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob)
    for m in mats:me.materials.append(m)
    uv=me.uv_layers.new(name='UVMap')
    for p in me.polygons:
        if mis:p.material_index=mis[p.index]
        for li in p.loop_indices:
            co=me.vertices[me.loops[li].vertex_index].co
            uv.data[li].uv=uvfunc(p,co,me.loops[li].vertex_index) if uvfunc else (co.x+.5,co.z+.5)
    return ob

def lathe(name,profile,mats,labelrange,segments=48,knurl=False):
    verts=[]
    for j,(z,r) in enumerate(profile):
        for i in range(segments):
            a=2*math.pi*i/segments
            rr=r*(.9955+.0045*math.cos(24*a)) if knurl and j>=len(profile)-5 else r
            verts.append((rr*math.sin(a),-rr*math.cos(a),z))
    faces=[];mis=[]
    for j in range(len(profile)-1):
        for i in range(segments):
            k=(i+1)%segments;faces.append((j*segments+i,j*segments+k,(j+1)*segments+k,(j+1)*segments+i));mis.append(0 if labelrange[0]<=j<labelrange[1] else 1)
    verts.extend([(0,0,profile[0][0]),(0,0,profile[-1][0])]);bot=len(verts)-2;top=len(verts)-1
    for i in range(segments):
        k=(i+1)%segments;faces.append((bot,k,i));mis.append(1);faces.append((top,(len(profile)-1)*segments+i,(len(profile)-1)*segments+k));mis.append(1)
    h=profile[-1][0]
    def uv(p,c,vi):
        if p.material_index==0:
            # Position label centred on the front semicircle, with restrained side/back print.
            # Two loop UVs at the back vertex keep the wrap seam local instead of
            # interpolating the entire label into one compressed rear strip.
            column=vi%segments;u=(.5+column/segments)%1.0
            if p.index%segments==segments//2-1 and column==segments//2:u=1.0
            v=(c.z-profile[labelrange[0]][0])/(profile[labelrange[1]][0]-profile[labelrange[0]][0]);return (u,v)
        return (.5+c.x/(2*max(r for z,r in profile)),.5+c.y/(2*max(r for z,r in profile)))
    ob=meshobj(name,verts,faces,mats,mis,uv)
    for p in ob.data.polygons:p.use_smooth=len(p.vertices)==4
    return ob

def box(name,w,d,h,z,mats,toplabel=False,bevel=.0015):
    bpy.ops.mesh.primitive_cube_add(size=1,location=(0,0,z+h/2));ob=bpy.context.object;ob.name=name;ob.data.name=name;ob.dimensions=(w,d,h);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    for m in mats:ob.data.materials.append(m)
    if not ob.data.uv_layers:ob.data.uv_layers.new(name='UVMap')
    for p in ob.data.polygons:
        front=p.normal.y<-.5;top=p.normal.z>.5
        p.material_index=0 if (top if toplabel else front) else min(1,len(mats)-1)
        for li in p.loop_indices:
            co=ob.data.vertices[ob.data.loops[li].vertex_index].co
            if toplabel:uv=(co.x/w+.5,co.y/d+.5)
            else:uv=(co.x/w+.5,co.z/h+.5)
            ob.data.uv_layers.active.data[li].uv=uv
    mod=ob.modifiers.new('Folded edges','BEVEL');mod.width=bevel;mod.segments=2
    bpy.context.view_layer.objects.active=ob;bpy.ops.object.modifier_apply(modifier=mod.name)
    return ob

def bag(name,w,h,d,mats):
    nx=16;nz=20;verts=[];faces=[];mis=[]
    for side in [-1,1]:
        for j in range(nz+1):
            v=j/nz
            for i in range(nx+1):
                u=i/nx
                shoulder=.87+.13*math.sin(math.pi*v)
                x=(u-.5)*w*shoulder
                # Pillow body with shallow diagonal gathers and visibly pressed crimp strips.
                body=(math.sin(math.pi*u)**.38)*(math.sin(math.pi*v)**.45)
                gather=.92+.07*math.cos(6*math.pi*u+v*5)+.025*math.cos(13*u+18*v)
                crimp=v<.10 or v>.9
                thick=.0012+(.0018*(1+.45*math.cos(i*math.pi)) if crimp else (d/2-.0012)*body*gather)
                z=h*v
                if j==nz:z-=h*.007*(i%2)
                if j==0:z+=h*.007*(i%2)
                verts.append((x,side*thick,z))
    per=(nx+1)*(nz+1)
    for si in range(2):
        off=si*per
        for j in range(nz):
            for i in range(nx):
                a=off+j*(nx+1)+i;q=(a,a+1,a+nx+2,a+nx+1)
                faces.append(q if si==0 else tuple(reversed(q)));mis.append(0 if si==0 else 1)
    ring=list(range(nx+1))+[j*(nx+1)+nx for j in range(1,nz+1)]+[nz*(nx+1)+i for i in range(nx-1,-1,-1)]+[j*(nx+1) for j in range(nz-1,0,-1)]
    for i,a in enumerate(ring):b=ring[(i+1)%len(ring)];faces.append((a,per+a,per+b,b));mis.append(1)
    def uv(p,c,vi):idx=vi%per;return ((idx%(nx+1))/nx,(idx//(nx+1))/nz)
    ob=meshobj(name,verts,faces,mats,mis,uv)
    for p in ob.data.polygons:p.use_smooth=True
    # Keep the exact placement envelope; positive authored local scale baked into vertices.
    extrema=np.array(verts);bounds=extrema.max(0)-extrema.min(0)
    for v in ob.data.vertices:v.co.x*=w/bounds[0];v.co.y*=d/bounds[1]
    return ob

def combine(name,objects):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();ob=bpy.context.object
    bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');ob.name=name;ob.data.name=name
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.triangulate(bm,faces=bm.faces[:]);bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]);bm.to_mesh(ob.data);bm.free();ob.data.update()
    return ob

def target_camera(cam,point):cam.rotation_euler=(Vector(point)-cam.location).to_track_quat('-Z','Y').to_euler()
def render(ob,name,views):
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=16;scene.cycles.use_denoising=True;scene.render.threads_mode='FIXED';scene.render.threads=2
    scene.render.resolution_x=448;scene.render.resolution_y=448;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
    if scene.world is None:scene.world=bpy.data.worlds.new('_review_world')
    scene.world.color=(.16,.16,.16)
    scene.view_settings.view_transform='AgX'
    size=max(ob.dimensions);centre=Vector((0,0,ob.dimensions.z*.48))
    bpy.ops.mesh.primitive_plane_add(size=size*200,location=(0,0,-.0001));ground=bpy.context.object;ground.name='_review_floor';ground.data.materials.append(mat('_review_floor',(.055,.070,.083),.7))
    lights=[]
    for loc,power,lsize in [((1,-1.4,2),28,1.6),((-1,-.3,1.1),14,1.1),((.3,1.4,1.8),22,1.1)]:
        data=bpy.data.lights.new('_review_area','AREA');data.energy=power*size*size*2;data.shape='DISK';data.size=lsize*size
        light=bpy.data.objects.new('_review_area',data);bpy.context.collection.objects.link(light);light.location=Vector(loc)*size;target_camera(light,centre);lights.append(light)
    cd=bpy.data.cameras.new('_review_camera');cam=bpy.data.objects.new('_review_camera',cd);bpy.context.collection.objects.link(cam);scene.camera=cam;cd.type='ORTHO';cd.ortho_scale=size*1.55;cd.lens=50
    files=[]
    for view,vec in views:
        cam.location=centre+Vector(vec)*size;target_camera(cam,centre);scene.render.filepath=str(ROOT/'Previews'/f'{name}_{view}.png');bpy.ops.render.render(write_still=True);files.append(scene.render.filepath)
    for x in [ground,cam,*lights]:bpy.data.objects.remove(x,do_unlink=True)
    return files

SPECS=[
 ('bb_set_coffee_tin','coffee','ROAST','DARK ROAST','gold',(.12,.16,.12)),
 ('bb_set_food_jar','food','PANTRY','GRANOLA','cyan',(.11,.14,.11)),
 ('bb_set_cereal_carton','cereal','MORNINGS','OAT CLUSTERS','gold',(.20,.30,.07)),
 ('bb_set_snack_blue','snack','CRUNCH','SEA SALT','cyan',(.16,.22,.05)),
 ('bb_set_snack_gold','snack','CRUNCH','GOLDEN OAT','gold',(.16,.22,.05)),
 ('bb_set_hoh_snack_blue','snack','SELECT','SEA SALT','cyan',(.14,.20,.05)),
 ('bb_set_hoh_snack_gold','snack','SELECT','GOLDEN OAT','gold',(.14,.20,.05)),
 ('bb_set_game_strategy','game','STRATEGY','PLAN YOUR NEXT MOVE','cyan',(.30,.05,.20)),
 ('bb_set_game_alliance','game','ALLIANCE','PLAY YOUR CONNECTIONS','gold',(.30,.05,.20)),
 ('bb_set_game_social','game','SOCIAL','READ THE ROOM','cyan',(.30,.05,.20)),
 ('bb_set_toiletry','care','CLEAN','DAILY WASH','cyan',(.06,.10,.06)),
 ('bb_set_cosmetic','care','CARE','FACE CREAM','gold',(.06,.10,.06)),
]

def build(spec,index):
    name,kind,title,sub,accent,(w,h,d)=spec
    bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1;scene.unit_settings.length_unit='METERS'
    lm=labelmat(name,label(name,title,sub,accent,kind,index));trim=mat(name+'_trim',tuple(((c/255+.055)/1.055)**2.4 for c in PALETTE[accent]),.38,.28 if kind in ['coffee','food'] else 0)
    base=mat(name+'_base',(.038,.051,.063),.55)
    if kind=='coffee':
        r=w/2;p=[(0,r*.94),(.001,r),(.004,r),(.006,r*.955),(.010,r*.95),(.142,r*.95),(.145,r*.97),(.147,r),(.150,r),(.151,r*.95),(.156,r*.95),(.158,r),(.160,r),(.160,r*.90)]
        ob=lathe(name,p,[lm,trim],(4,5))
    elif kind=='food':
        r=w/2;p=[(0,r*.90),(.003,r*.97),(.008,r),(.104,r),(.112,r*.94),(.117,r*.80),(.121,r*.80),(.123,r*.94),(.126,r*.96),(.137,r*.96),(.140,r*.91)]
        ob=lathe(name,p,[lm,trim],(2,3),knurl=True)
    elif kind=='care':
        r=w/2
        if name.endswith('cosmetic'):p=[(0,r*.90),(.003,r),(.064,r),(.066,r*.92),(.068,r*.92),(.070,r),(.096,r),(.100,r*.90)]
        else:p=[(0,r*.86),(.004,r),(.064,r),(.075,r*.82),(.081,r*.60),(.084,r*.60),(.085,r*.69),(.097,r*.69),(.100,r*.60)]
        ob=lathe(name,p,[lm,trim],(1,2),segments=48,knurl=True)
    elif kind=='snack':ob=bag(name,w,h,d,[lm,trim])
    elif kind=='game':
        objs=[box(name+'_tray',w-.0015,d-.0015,h*.48,0,[base,base],True,.0013),box(name+'_lid',w,d,h*.52,h*.48,[lm,trim],True,.0013)]
        ob=combine(name,objs)
    else:
        objs=[box(name+'_body',w,d,h-.006,0,[lm,trim],False,.0018)]
        # Closed folded carton with two distinct top panels and a recessed fold crease.
        flap_depth=(d-.001-.0008)/2
        for side in [-1,1]:
            flap=box(name+'_flap',w-.001,flap_depth,.006,h-.006,[trim,trim],False,.0006)
            flap.location.y=side*(flap_depth+.0008)/2;objs.append(flap)
        objs.append(box(name+'_fold',w*.91,.0008,.001,h-.0012,[base,base],False,.00015))
        ob=combine(name,objs)
    if kind not in ['game','cereal']:ob=combine(name,[ob])
    # Exported source object is a single floor-root mesh, no cameras, lights or collider helpers.
    bpy.context.view_layer.update();size=tuple(float(x) for x in ob.dimensions)
    tris=len(ob.data.polygons);assert tris<=2500,(name,tris)
    assert max(abs(size[i]-[w,d,h][i]) for i in range(3))<.0006,(name,size)
    assert len(ob.data.materials)<=3
    assert len(ob.data.uv_layers)>0
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Sources'/f'{name}.blend'))
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
    fbx=ROOT/'FBX'/f'{name}.fbx';glb=ROOT/'GLB'/f'{name}.glb'
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=True,object_types={'MESH'},use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,add_leaf_bones=False,path_mode='COPY',embed_textures=False,use_custom_props=False,bake_anim=False)
    bpy.ops.export_scene.gltf(filepath=str(glb),export_format='GLB',use_selection=True,export_texcoords=True,export_normals=True,export_tangents=True,export_materials='EXPORT',export_animations=False,export_yup=True)
    previews=render(ob,name,[('threequarter',(1,-1.6,1.1)),('front',(0,-2,.2)),('back',(-1,1.5,1.1))])
    report={'name':name,'kind':kind,'expectedUnityXYZ':[w,h,d],'blenderBoundsXYZ':list(size),'triangles':tris,'materials':[m.name for m in ob.data.materials],'uvLayers':len(ob.data.uv_layers),'origin':list(ob.location),'sourceBlend':str(ROOT/'Sources'/f'{name}.blend'),'glb':str(glb),'fbx':str(fbx),'previews':previews,'sha256':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [glb,fbx]},'art':'Original authored geometry and deterministic procedural PBR textures; no external assets or generators. Rights reserved for GameSim project; no CC0 grant implied.'}
    (ROOT/'Reports'/f'{name}.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print('ASSET_COMPLETE '+json.dumps(report),flush=True)
    return report

if __name__=='__main__':
    selected=set(sys.argv[sys.argv.index('--')+1:]) if '--' in sys.argv else set()
    reports=[]
    # First tin and bag quickly establish the two main packaging shapes visually.
    order=[0,3]+[i for i in range(len(SPECS)) if i not in [0,3]]
    for i in order:
        if not selected or SPECS[i][0] in selected:reports.append(build(SPECS[i],i))
    (ROOT/'Reports'/'build-receipt.json').write_text(json.dumps({'blender':bpy.app.version_string,'seed':SEED,'models':reports,'count':len(reports),'scriptSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest()},indent=2),encoding='utf-8')
