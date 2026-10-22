"""Assemble a reviewable bundle outside Unity; never applies it to a project."""
import argparse, hashlib, json, shutil, zipfile
from pathlib import Path

TASK=Path(__file__).resolve().parent
OUT=TASK.parents[1]/'outputs'/'gamesim-props-20261020'
BUNDLE=OUT/'GameSim-props-equipment-candidate'

def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def copy(src,dst):
    src=Path(src);dst=Path(dst)
    if not src.is_file():raise RuntimeError('Missing input '+str(src))
    if dst.exists():raise RuntimeError('Destination already exists '+str(dst))
    dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dst)
def tree(src,dst,exclude=()):
    src=Path(src)
    for p in sorted(src.rglob('*')):
        if p.is_file() and not any(s in p.parts for s in exclude):copy(p,Path(dst)/p.relative_to(src))

def main():
    global BUNDLE
    parser=argparse.ArgumentParser();parser.add_argument('--revision',default='')
    args=parser.parse_args();suffix='-'+args.revision if args.revision else ''
    if args.revision and not args.revision.isalnum():raise RuntimeError('Revision must be alphanumeric')
    BUNDLE=OUT/('GameSim-props-equipment-candidate'+suffix)
    if BUNDLE.exists():raise RuntimeError('Use a new bundle destination; do not overwrite review evidence')
    BUNDLE.mkdir(parents=True)
    overlay=BUNDLE/'ProjectOverlay'
    native=overlay/'Assets/Gamesim/Art/Authored'
    sources=BUNDLE/'Authoring'
    # Native companion exports: keep exact filenames and relative texture dependencies.
    tree(TASK/'competition-art-02'/'Parts',native/'Competition')
    houseinputs=json.loads((TASK/'house-code'/'asset-input-manifest.json').read_text(encoding='utf-8'))
    for row in houseinputs['native_file_overlay']:
        if sha(row['source']) != row['sha256']:raise RuntimeError('House native input drift: '+row['source'])
        copy(row['source'],overlay/row['destination'])
    for folder in ['mini-fridge-02','headphone-case-02']:
        d=TASK/'appliances'/folder
        for p in d.glob('*.blend'):copy(p,sources/'appliances'/p.name)
        tree(d/'Textures',sources/'appliances'/'Textures')
    assert len(list((native/'HouseSmallProps').glob('*.fbx')))==14
    assert len(list((native/'Competition').glob('*.fbx')))==39
    # Postimages are separately reviewed against their baseline guards. No bulk copy to a project occurs here.
    for family in ['competition-code','house-code']:
        folder=TASK/family
        tree(folder/'postimage',overlay)
        primary='competition-authored-geometry.patch' if family=='competition-code' else 'all-house-source.patch'
        for p in folder.glob('*.patch'):copy(p,(BUNDLE/'Patches' if p.name==primary else BUNDLE/'Evidence'/family)/p.name)
        for p in folder.glob('*.json'):copy(p,BUNDLE/'Evidence'/family/p.name)
        for p in folder.glob('*.md'):copy(p,BUNDLE/'Evidence'/family/p.name)
        for p in folder.glob('*.log'):copy(p,BUNDLE/'Evidence'/family/p.name)
        for d in folder.glob('*probe*'):
            if d.is_dir():tree(d,BUNDLE/'Evidence'/family/d.name)
        if (folder/'compile-final-01').is_dir():
            for p in (folder/'compile-final-01').rglob('*'):
                if p.is_file() and p.suffix.lower() in ('.json','.log','.rsp','.txt','.md','.ps1','.py'):
                    copy(p,BUNDLE/'Evidence'/family/'compile-final-01'/p.relative_to(folder/'compile-final-01'))
    # The canonical packages are immutable six-file units, separate from Unity import derivatives.
    indexes=[]
    for group in ['equipment','appliances','packaging']:
        index=OUT/(group+'-package-index.json');copy(index,BUNDLE/'Evidence'/index.name)
        rows=json.loads(index.read_text(encoding='utf-8'));indexes+=rows
        for row in rows:
            assert row['validation']['passed'] and row['verification']['hashesVerified']
            tree(Path(row['packagePath']),BUNDLE/'CanonicalPackages'/row['name']/row['verification']['manifest']['version'])
    assert len(indexes)==19
    for p in ['build_competition_art.py','render_grip_review.py','package_candidates.py',Path(__file__).name]:
        copy(TASK/p,sources/'tools'/p)
    copy(TASK/'competition-art-02'/'competition_parts.blend',sources/'competition'/'competition_parts.blend')
    copy(TASK/'competition-code'/'part-manifest.json',sources/'competition'/'part-manifest.json')
    copy(TASK/'competition-art-02'/'authoring-receipt.json',sources/'competition'/'authoring-receipt.json')
    for p in ['author_appliances_v2.py','validate_roundtrips_v2.py','validate_native_references_v2.py']:
        copy(TASK/'appliances'/p,sources/'appliances'/p)
    for p in ['roundtrip-validation-02.json','native-texture-references-02.json','tangent-export-fix-02.json','delivery-manifest-02.json','ASSET_SPEC_AND_HANDOFF.md']:
        copy(TASK/'appliances'/p,BUNDLE/'Evidence'/'appliances'/p)
    copy(TASK/'appliances'/'competition-independent-check'/'validation-01.json',BUNDLE/'Evidence'/'competition-independent-check.json')
    for p in (TASK/'appliances'/'competition-independent-check').glob('*.py'):copy(p,sources/'competition'/p.name)
    tree(TASK/'packaging'/'Sources',sources/'packaging'/'Sources')
    tree(TASK/'packaging'/'Textures',sources/'packaging'/'Textures')
    for p in ['build_packaging.py','finish_packaging.py','audit_packaging.py','README.md']:
        copy(TASK/'packaging'/p,sources/'packaging'/p)
    tree(TASK/'packaging'/'Reports',BUNDLE/'Evidence'/'packaging')
    tree(TASK/'packaging'/'Previews',BUNDLE/'Previews'/'packaging')
    tree(TASK/'competition-art-02'/'Previews',BUNDLE/'Previews'/'competition')
    copy(TASK/'appliances'/'candidate-contact-sheet-01.png',BUNDLE/'Previews'/'appliances.png')
    for p in ['README.md','ORIGINAL-ASSET-NOTICE.txt','verify_bundle.py']:
        copy(OUT/p,BUNDLE/p)
    if (OUT/'isolation-check.json').is_file():copy(OUT/'isolation-check.json',BUNDLE/'Evidence'/'isolation-check.json')
    bridges=[]
    for p in sorted(native.rglob('*')):
        if p.is_file():bridges.append({'projectPath':p.relative_to(overlay).as_posix(),'sha256':sha(p),'bytes':p.stat().st_size})
    (BUNDLE/'native-bridge.json').write_text(json.dumps({'schema':'gamesim.original_art_bridge.v1',
        'status':'staged; no project admission or Unity import',
        'provenance':'FBX and canonical GLB are parallel exports of the bound original Blender sources; FBX is not claimed to be a byte copy of canonical GLB.',
        'canonicalPackages':[{'name':r['name'],'packageId':r['packageId'],'manifestSha256':r['manifestSha256']} for r in indexes],
        'files':bridges},indent=2)+'\n',encoding='utf-8')
    roster=[{'path':p.relative_to(BUNDLE).as_posix(),'sha256':sha(p),'bytes':p.stat().st_size}
            for p in sorted(BUNDLE.rglob('*')) if p.is_file()]
    (BUNDLE/'bundle-manifest.json').write_text(json.dumps({'schema':'gamesim.art_candidate_bundle.v1','files':roster},indent=2)+'\n',encoding='utf-8')
    archive=OUT/('GameSim-props-equipment-candidate-20261020'+suffix+'.zip')
    if archive.exists():raise RuntimeError('Archive already exists')
    with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for p in sorted(BUNDLE.rglob('*')):
            if p.is_file():z.write(p,Path(BUNDLE.name)/p.relative_to(BUNDLE))
    with zipfile.ZipFile(archive) as z:
        if z.testzip() is not None:raise RuntimeError('Archive CRC failed')
        assert len(z.namelist())==len(roster)+1
    receipt={'bundle':str(BUNDLE),'files':len(roster)+1,'archive':str(archive),'bytes':archive.stat().st_size,'sha256':sha(archive),'crcPassed':True,'projectWrites':False,'unityRun':False}
    (OUT/('delivery-receipt'+suffix+'.json')).write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(receipt))

if __name__=='__main__':main()
