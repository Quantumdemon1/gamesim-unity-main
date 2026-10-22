"""Build and verify isolated canonical packages. Never writes into a Unity project."""
import argparse, hashlib, json, os, subprocess
from pathlib import Path

TASK = Path(__file__).resolve().parent
OUT = TASK.parents[1] / 'outputs' / 'gamesim-props-20261020'
NODE = Path(r'C:\Program Files\nodejs\node.exe')
CLI = Path(r'D:\CodexGamesimTools\game-dev-1.5.1\install\node_modules\@theisegoria\game-development-studio\dist\cli.js')

def digest(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()

def run(args, log):
    env = dict(os.environ, GAME_DEV_DATA_ROOT=str(OUT / '.game-dev'))
    proc = subprocess.run([str(NODE), str(CLI), *map(str,args), '--output-dir', str(OUT), '--json'],
                          capture_output=True, text=True, encoding='utf-8', env=env, timeout=90)
    log.parent.mkdir(parents=True, exist_ok=True)
    log.write_text(proc.stdout, encoding='utf-8')
    if proc.stderr: log.with_suffix('.stderr.txt').write_text(proc.stderr, encoding='utf-8')
    if proc.returncode: raise RuntimeError(f'{log.name}: code {proc.returncode}; see log')
    result=json.loads(proc.stdout)
    if not result.get('ok'): raise RuntimeError(f'{log.name}: operation did not succeed')
    return result['data']

def package(source, name, maxtri, textured, notes, source_files, maxmaterials=None, version='1.0.0'):
    policy={'requireUVs':True,'requireNormals':True,'requireTangentsWithNormalMap':True,
            'requireBaseColorTexture':textured,'maxTriangles':maxtri,
            'maxMaterials':maxmaterials or (1 if textured else 4), 'minTextureSize':256,
            'requirePowerOfTwoTextures':True,'maxDimensionMeters':3,'minDimensionMeters':0.001}
    binds='; '.join(str(Path(p).relative_to(TASK)).replace('\\','/')+' sha256='+digest(p) for p in source_files)
    metadata={'name':name,'version':version,'category':'environment_prop',
              'license':'LicenseRef-GameSim-Original','description':notes,
              'policy':policy,'provenance':{'origin':'authored','author':'GameSim original art batch',
              'model':'Blender 5.2.2 LTS','sourceUri':source.as_uri(),
              'prompt':'Original GameSim house dressing and competition equipment within existing placement and motion envelopes.',
              'notes':notes+' No third-party assets or provider services. '+binds}}
    logroot=OUT/'receipts'/name/version
    logroot.mkdir(parents=True,exist_ok=True)
    req=logroot/'request.json'; req.write_text(json.dumps(metadata,indent=2)+'\n',encoding='utf-8')
    plan=run(['package','build',source,'--request',req,'--dry-run'],logroot/'plan.json')
    if not plan['validation']['passed']: raise RuntimeError(f'{name}: policy gate failed')
    built=run(['package','build',source,'--request',req],logroot/'build.json')
    verified=run(['package','verify',built['packagePath']],logroot/'verify.json')
    if verified.get('hashesVerified') is not True:
        raise RuntimeError(f'{name}: verification failed')
    inspection=run(['asset','inspect',source],logroot/'inspection.json')
    return {'name':name,'source':str(source),'sourceSha256':digest(source),
            'packagePath':built['packagePath'],'packageId':built['packageId'],'version':version,
            'manifestSha256':built['manifestSha256'],'validation':built['validation'],
            'inspection':inspection,'verification':verified}

def main():
    ap=argparse.ArgumentParser();ap.add_argument('group',choices=['equipment','appliances','packaging']);ap.add_argument('--sources',type=Path)
    a=ap.parse_args(); OUT.mkdir(parents=True,exist_ok=True); records=[]
    if a.group=='equipment':
        src=TASK/'competition-art-02'
        for p in sorted((src/'Assemblies').glob('*.glb')):
            records.append(package(p,p.stem.lower(),16000,False,
                'Assembled reference station in metres. Unity consumes the separate normalized FBX part kit; runtime owns visible text, tint, moving parts and game state. Canonical GLB is an art reference, not a static runtime replacement.',
                [TASK/'build_competition_art.py',TASK/'competition-code'/'part-manifest.json',src/'competition_parts.blend']))
        assert len(records)==5
    else:
        if not a.sources: raise RuntimeError('--sources required for explicit approved source list')
        for row in json.loads(a.sources.read_text(encoding='utf-8-sig')):
            p=Path(row['glb']);records.append(package(p,p.stem.lower(),row['maxTriangles'],True,row['notes'],[Path(s) for s in row['sources']],row.get('maxMaterials'),row.get('version','1.0.0')))
    report=OUT/(a.group+'-package-index.json');report.write_text(json.dumps(records,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'group':a.group,'packages':len(records),'report':str(report),'passed':all(x['validation']['passed'] for x in records)}))

if __name__=='__main__': main()
