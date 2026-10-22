from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, shutil, subprocess

root = Path(__file__).resolve().parent
task = root.parent.parent
project = Path('D:/GamesimConsolidation')
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
source_roots = ['Assets/Gamesim/Simulation', 'Assets/Gamesim/Runtime', 'Assets/Gamesim/Editor', 'Assets/Gamesim/Tests/PlayMode', 'Assets/Gamesim/Tests/EditMode']

def current_inputs():
    return {path.relative_to(project).as_posix():sha(path) for folder in source_roots for path in (project/folder).rglob('*.cs')}

if (root/'before.json').exists():
    raise RuntimeError('This bounded evidence folder was already prepared; do not overwrite it.')
staged = {}
for package in ['competition-code','house-code']:
    post = task/package/'postimage'
    for path in post.rglob('*.cs'):
        rel = path.relative_to(post).as_posix()
        if rel in staged:
            raise RuntimeError('Overlapping candidates: '+rel)
        output=root/'postimage'/rel
        output.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(path,output)
        staged[rel]={'source':str(path),'sha256':sha(path)}
if len(staged)!=9:
    raise RuntimeError(f'Expected nine staged files, got {len(staged)}')
script=(root.parent/'compile-candidate.ps1').read_text(encoding='utf-8-sig')
original = "$CandidateRoot = 'C:\\Users\\kelli\\Documents\\Codex\\2026-09-20\\re\\work\\house-equipment-20261020\\competition-code\\postimage'"
if script.count(original)!=1:
    raise RuntimeError('Unexpected candidate script layout')
script=script.replace(original,"$CandidateRoot = '"+str(root/'postimage')+"'")
# Retain full compiler diagnostics in the new output directory as well as the compact replay log.
needle='    $compilerExit = $LASTEXITCODE\n'
if script.count(needle)!=1: raise RuntimeError('Unexpected compiler script layout')
script=script.replace(needle,needle+'    [IO.File]::WriteAllText((Join-Path $Out ($name + ".compiler.log")), ($result -join [Environment]::NewLine))\n')
(root/'replay.ps1').write_text(script,encoding='utf-8')
before=dict(preparedUtc=datetime.now(timezone.utc).isoformat(),sourceProject=str(project),
    sourceHead=subprocess.check_output(['git','-C',str(project),'rev-parse','HEAD'],text=True).strip(),
    staged=staged,sourceFiles=current_inputs(),
    scope='One NoUMA offline replay of all five normal assemblies, with nine external source postimages. Fresh local dependency outputs only. No Unity invocation or project writes.')
(root/'before.json').write_text(json.dumps(before,indent=2)+'\n',encoding='utf-8')
print(json.dumps(dict(stagedFiles=len(staged),currentSourceFiles=len(before['sourceFiles']),evidence=str(root)),indent=2))
