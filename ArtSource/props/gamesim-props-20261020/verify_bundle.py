"""Read-only verification; does not copy files, import assets, or start Unity."""
import argparse, hashlib, json, re, subprocess
from pathlib import Path

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def within(root,relative):
    path=(root/relative).resolve()
    if not path.is_relative_to(root.resolve()):raise RuntimeError('Path escapes root: '+str(relative))
    return path

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--project',type=Path)
    args=parser.parse_args();root=Path(__file__).resolve().parent
    manifest=read(root/'bundle-manifest.json');rows=manifest['files']
    expected={row['path'] for row in rows}
    actual={p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file() and p.name!='bundle-manifest.json'}
    if actual!=expected:raise RuntimeError('Bundle file roster differs from manifest')
    digests=set()
    for row in rows:
        p=within(root,row['path']);h=sha(p)
        if h!=row['sha256'] or p.stat().st_size!=row['bytes']:raise RuntimeError('Changed bundle file: '+row['path'])
        digests.add(h)
    packages=list((root/'CanonicalPackages').glob('*/*/manifest.json'))
    if len(packages)!=19:raise RuntimeError('Expected 19 canonical packages')
    for path in packages:
        m=read(path)
        if not m['validation']['passed'] or m['validation']['errorCount'] or m['validation']['warningCount']:
            raise RuntimeError('Package policy did not pass: '+str(path))
        for entry in m['files']:
            p=within(path.parent,entry['path'])
            if sha(p)!=entry['sha256'] or p.stat().st_size!=entry['bytes']:raise RuntimeError('Changed canonical payload: '+str(p))
        provenance=read(path.parent/'provenance.json')
        for digest in re.findall(r'sha256=([0-9a-f]{64})',provenance.get('notes','')):
            if digest not in digests:raise RuntimeError('A bound authoring source is missing: '+digest)
    result={'bundleFilesVerified':len(rows),'packagesVerified':len(packages),'projectWrites':False,'unityStarted':False}
    if args.project:
        project=args.project.resolve()
        if not (project/'ProjectSettings/ProjectVersion.txt').is_file():raise RuntimeError('Not a Unity project')
        c=read(root/'Evidence/competition-code/handoff-manifest.json')
        h=read(root/'Evidence/house-code/source-guard.json')
        baselines=dict(c['sourceBaselineHashes'])
        baselines[h['relative_path']]=h['preimage_sha256']
        baselines[h['existing_test_guard']['relative_path']]=h['existing_test_guard']['preimage_sha256']
        for rel,digest in baselines.items():
            path=within(project,rel)
            if not path.is_file() or sha(path)!=digest:raise RuntimeError('Source drift; review/rebase required: '+rel)
        for staged in (root/'ProjectOverlay').rglob('*'):
            if not staged.is_file():continue
            rel=staged.relative_to(root/'ProjectOverlay').as_posix()
            if rel not in baselines and within(project,rel).exists():
                raise RuntimeError('New candidate destination already occupied; review required: '+rel)
        for p in sorted((root/'Patches').glob('*.patch')):
            r=subprocess.run(['git','--no-optional-locks','-C',str(project),'apply','--check',str(p)],capture_output=True,text=True,timeout=30)
            if r.returncode:raise RuntimeError('Patch check failed: '+p.name+'\n'+r.stderr)
        result.update(project=str(project),baselineFilesVerified=len(baselines),patchesApplicable=True)
    print(json.dumps(result,indent=2))

if __name__=='__main__':main()
