from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, re

root=Path(__file__).resolve().parent
before=json.loads((root/'before.json').read_text(encoding='utf-8'))
project=Path(before['sourceProject'])
sha=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
source_roots=['Assets/Gamesim/Simulation','Assets/Gamesim/Runtime','Assets/Gamesim/Editor','Assets/Gamesim/Tests/PlayMode','Assets/Gamesim/Tests/EditMode']
current={path.relative_to(project).as_posix():sha(path) for folder in source_roots for path in (project/folder).rglob('*.cs')}
changed=sorted(rel for rel in before['sourceFiles'].keys() | current.keys() if before['sourceFiles'].get(rel)!=current.get(rel))
outputs=list((root/'assemblies').iterdir())
if len(outputs)!=1: raise RuntimeError('Unexpected output directory count')
output=outputs[0]
replay=(root/'replay.log').read_text(encoding='utf-8-sig')
assemblies=[]
for name in ['Gamesim.Simulation','Gamesim.Runtime','Gamesim.Editor','Gamesim.PlayModeTests','Gamesim.EditModeTests']:
    dll=output/(name+'.dll')
    log=output/(name+'.compiler.log')
    diagnostics=log.read_text(encoding='utf-8-sig') if log.exists() else ''
    errors=re.findall(r'^.*\berror\s+[A-Z]+\d+:.*$',diagnostics,re.M)
    warnings=re.findall(r'^.*\bwarning\s+[A-Z]+\d+:.*$',diagnostics,re.M)
    assemblies.append(dict(name=name,compiled=dll.exists() and not errors,sha256=sha(dll) if dll.exists() else None,
        compilerErrors=errors,warningCount=len(warnings),compilerLog=str(log),responseFile=str(output/(name+'.rsp'))))
result=dict(completedUtc=datetime.now(timezone.utc).isoformat(),scope=before['scope'],
    wholeNoUmaOfflineReplayPassed='Fresh assemblies checked: 5 of 5' in replay and all(item['compiled'] for item in assemblies),
    sourceInputCount=len(current),sourceInputChangesDuringReplay=changed,
    stagedOriginalFilesUnchanged=all(sha(Path(item['source']))==item['sha256'] for item in before['staged'].values()),
    stagedSnapshotFilesMatch=all(sha(root/'postimage'/rel)==item['sha256'] for rel,item in before['staged'].items()),
    assemblies=assemblies,unityTestsRun=False,umaReplayPerformed=False,
    limitations='Offline C# compilation only using existing Unity reference-cache configuration; does not run Unity, validate native asset imports, execute tests, or verify updated asmdef dependency declarations.')
(root/'result.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,indent=2))
