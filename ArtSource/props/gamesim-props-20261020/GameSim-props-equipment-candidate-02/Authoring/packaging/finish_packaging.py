"""Assemble preview sheets and Unity PBR companions from this family's authored files."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,hashlib,struct,shutil
ROOT=Path(__file__).resolve().parent
FONT=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',17)
TITLE=ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf',25)
SMALL=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',13)
names=['bb_set_coffee_tin','bb_set_food_jar','bb_set_cereal_carton','bb_set_snack_blue','bb_set_snack_gold','bb_set_hoh_snack_blue','bb_set_hoh_snack_gold','bb_set_game_strategy','bb_set_game_alliance','bb_set_game_social','bb_set_toiletry','bb_set_cosmetic']
reports=[json.loads((ROOT/'Reports'/f'{n}.json').read_text()) for n in names]
for page,part in enumerate([names[:6],names[6:]],1):
    sheet=Image.new('RGB',(1104,1350),(18,25,31));draw=ImageDraw.Draw(sheet)
    draw.text((24,15),f'GAMESIM | Original packaging family | {page}/2',font=TITLE,fill=(217,224,225))
    draw.text((24,52),'Three-quarter / Front / Back  |  Isolated Blender previews; Unity acceptance pending',font=SMALL,fill=(139,174,184))
    for row,name in enumerate(part):
        y=85+row*207
        report=reports[names.index(name)]
        for column,view in enumerate(['threequarter','front','back']):
            im=Image.open(ROOT/'Previews'/f'{name}_{view}.png').convert('RGB');im.thumbnail((178,178),Image.Resampling.LANCZOS)
            sheet.paste(im,(350+column*248,y))
        label=name.replace('bb_set_','').replace('_',' ').upper()
        draw.text((24,y+36),label,font=FONT,fill=(217,224,225))
        draw.text((24,y+67),f"{report['triangles']} triangles | {len(report['materials'])} materials",font=SMALL,fill=(139,174,184))
        dims=' x '.join(f'{d:.2f}' for d in report['expectedUnityXYZ'])
        draw.text((24,y+91),dims+' m',font=SMALL,fill=(139,174,184))
        draw.text((24,y+111),'Unity X / Y / Z',font=SMALL,fill=(139,174,184))
    sheet.save(ROOT/'Previews'/f'packaging-multiangle-{page}.jpg',quality=94)
overview=Image.new('RGB',(1440,1180),(18,25,31));draw=ImageDraw.Draw(overview)
draw.text((24,15),'GAMESIM | Original house packaging | 12 models / 13 placements',font=TITLE,fill=(217,224,225))
for i,name in enumerate(names):
    x=(i%4)*360;y=65+(i//4)*365
    im=Image.open(ROOT/'Previews'/f'{name}_threequarter.png').convert('RGB').resize((328,328),Image.Resampling.LANCZOS)
    overview.paste(im,(x+16,y));draw.text((x+15,y+334),name.replace('bb_set_','').replace('_',' ').upper(),font=FONT,fill=(217,224,225))
overview.save(ROOT/'Previews'/'packaging-overview.jpg',quality=94)
maps=[]
for name in names:
    orm=Image.open(ROOT/'Textures'/f'{name}_orm.png').convert('RGBA')
    # Unity Standard/URP metallic workflow: metallic in R, smoothness in A.
    r,g,b,a=orm.split();smooth=g.point(lambda value:255-value)
    black=Image.new('L',orm.size,0);metallic=Image.merge('RGBA',(b,black,black,smooth))
    path=ROOT/'Textures'/f'{name}_metallic.png';metallic.save(path)
    raw=(ROOT/'GLB'/f'{name}.glb').read_bytes();length,kind=struct.unpack_from('<I4s',raw,12);assert kind==b'JSON';doc=json.loads(raw[20:20+length])
    materials=[]
    for material in doc['materials']:
        pbr=material.get('pbrMetallicRoughness',{})
        textured='baseColorTexture' in pbr
        materials.append({'name':material['name'],'baseColorFactorLinear':pbr.get('baseColorFactor',[1,1,1,1]),'metallicFactor':pbr.get('metallicFactor',1),'roughnessFactor':pbr.get('roughnessFactor',1),'usesLabelTextures':textured,'unityMetallic':pbr.get('metallicFactor',1),'unitySmoothness':1.0 if textured else 1-pbr.get('roughnessFactor',1),'unitySmoothnessNote':'Use scalar 1 with the supplied alpha smoothness map; do not derive zero from glTF roughnessFactor 1.' if textured else 'Solid material: 1 - glTF roughness factor.'})
    # Preserve exactly the same authored albedo bytes for the native FBX dependency.
    fbx_images=ROOT/'FBX'/f'{name}.fbm';fbx_images.mkdir(exist_ok=True)
    shutil.copy2(ROOT/'Textures'/f'{name}_albedo.png',fbx_images/f'{name}_albedo.png')
    maps.append({'name':name,'albedo':f'Textures/{name}_albedo.png','gltfOrm':f'Textures/{name}_orm.png','unityMetallicSmoothness':f'Textures/{name}_metallic.png','roughnessChannel':'gltf ORM green','unitySmoothnessChannel':'metallic alpha = 255 - ORM green','albedoSRGB':True,'ormSRGB':False,'metallicSRGB':False,'materials':materials})
(ROOT/'Reports'/'material-map.json').write_text(json.dumps({'version':1,'models':maps,'trim':'Untextured trim/base factors are authored in the FBX/GLB; do not apply label maps to these material slots.','fbxCaveat':'FBX carries diffuse images in .fbm folders; the separated ORM graph is authoritative in GLB. Use the companion metallic PNG and this map for URP material remapping.'},indent=2),encoding='utf-8')
# Rebuild the aggregate receipt from final per-model receipts, including any bounded rerun.
(ROOT/'Reports'/'build-receipt.json').write_text(json.dumps({'blender':'5.2.2 LTS','seed':20261020,'count':len(reports),'placements':13,'models':reports,'scriptSha256':hashlib.sha256((ROOT/'build_packaging.py').read_bytes()).hexdigest()},indent=2),encoding='utf-8')
print('Preview sheets, Unity material companions, and final aggregate receipt ready.')
