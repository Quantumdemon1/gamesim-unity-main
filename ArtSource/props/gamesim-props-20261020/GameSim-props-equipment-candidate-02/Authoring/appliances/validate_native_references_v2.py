import bpy
import pathlib
import json
from io_scene_fbx import parse_fbx

root=pathlib.Path(__file__).resolve().parent
destination=root/'native-texture-references-02.json'
if destination.exists():raise RuntimeError('Refuse prior output')
results=[]
for folder,name in [('mini-fridge-02','bb_set_mini_fridge'),('headphone-case-02','bb_set_headphone_case')]:
    fbx=root/folder/(name+'.fbx')
    tree,version=parse_fbx.parse(str(fbx))
    filenames=[]
    def visit(node):
        if node.id in (b'FileName',b'Filename',b'RelativeFilename'):
            filenames.append({'field':node.id.decode(),'values':[p.decode('utf-8') if isinstance(p,bytes) else p for p in node.props]})
        for child in node.elems:visit(child)
    visit(tree)
    relatives=[]
    for record in filenames:
        if record['field']=='RelativeFilename':
            for value in record['values']:
                resolved=fbx.parent/value.replace('\\','/')
                if not resolved.is_file():raise RuntimeError('Missing relative FBX payload: '+str(resolved))
                relatives.append(value)
    if not relatives:raise RuntimeError('No portable relative FBX dependencies')
    results.append({'file':str(fbx),'fbx_version':version,'relative_texture_references_verified':relatives,'embedded_path_fields':filenames})
with destination.open('x',encoding='utf-8') as stream:json.dump(results,stream,indent=2)
print('RELATIVE_FBX_REFERENCES_PASSED='+str(len(results)))
