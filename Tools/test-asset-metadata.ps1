# Isolated staged-Git fixtures only. Never modifies project assets or starts Unity.
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'review-workflow.ps1')
$checker=Join-Path $PSScriptRoot 'check-asset-metadata.sh'
$checkerPin=Get-ReviewFileRecord $checker 'check-asset-metadata.sh'
$bash=Join-Path $env:ProgramFiles 'Git/bin/bash.exe'
if (-not (Test-Path -LiteralPath $bash -PathType Leaf)) { throw 'Git for Windows bash is required for these shell fixtures.' }
$fixtureBase=Assert-ReviewPlainPath (Join-Path ([IO.Path]::GetTempPath()) 'GamesimMetadataFixtures')
$fixture=Join-Path $fixtureBase ([Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($fixture)
$results=[Collections.Generic.List[object]]::new()
$failure=$null
function Put([string]$Root,[string]$Relative,[string]$Text) {
    $path=Join-Path $Root $Relative
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $path))
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes($Text)
    $stream=[IO.File]::Open($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $stream.Write($bytes,0,$bytes.Length) } finally { $stream.Dispose() }
}
function Meta([string]$Guid) { "fileFormatVersion: 2`nguid: $Guid`n" }
function Scenario([string]$Name,[bool]$Accepted,[scriptblock]$Arrange,[string]$ExpectedMessage) {
    $root=Join-Path $fixture $Name
    [void][IO.Directory]::CreateDirectory($root)
    $copied=Get-ReviewFileRecord $checker 'Tools/check-asset-metadata.sh' (Join-Path $root 'Tools/check-asset-metadata.sh')
    if ($copied.sha256 -cne $checkerPin.sha256 -or $copied.bytes -ne $checkerPin.bytes) { throw 'Fixture checker differs from the pinned source.' }
    Put $root 'Assets/Visible Source.cs' '// baseline visible source'
    Put $root 'Assets/Visible Source.cs.meta' (Meta ('1'*32))
    & $Arrange $root
    & git -C $root init --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Cannot initialize isolated fixture Git index.' }
    & git -C $root -c core.autocrlf=false -c core.safecrlf=false add --all
    if ($LASTEXITCODE -ne 0) { throw 'Cannot stage isolated fixture inputs.' }
    $output=@(& $bash --noprofile --norc (Join-Path $root 'Tools/check-asset-metadata.sh'))
    $exit=$LASTEXITCODE
    $text=$output -join "`n"
    Put $root 'checker-output.txt' ($text+"`n")
    $row=[pscustomobject]@{name=$Name;expectedAccepted=$Accepted;exit=$exit;matchedMessage=($text.Contains($ExpectedMessage));checkerSha256=$copied.sha256;root=$root}
    $results.Add($row)
    if (($exit -eq 0) -ne $Accepted -or -not $row.matchedMessage) { throw "Metadata fixture failed: $Name (exit $exit). Retained output: $root" }
}
try {
    Scenario 'visible-paired' $true {} 'Asset metadata OK'
    Scenario 'hidden-attributes-no-meta' $true {param($root) Put $root 'Assets/Fixtures/.gitattributes' '*.json -text -eol'} 'Asset metadata OK'
    Scenario 'hidden-directory-no-meta' $true {param($root) Put $root 'Assets/.fixture-cache/Nested.cs' '// Unity ignores this hidden directory'} 'Asset metadata OK'
    Scenario 'visible-cs-missing-meta' $false {param($root) Put $root 'Assets/Unpaired.cs' '// missing meta'} 'Assets/Unpaired.cs'
    Scenario 'ordinary-dotted-directory-missing-meta' $false {param($root) Put $root 'Assets/Fixture.v1/Unpaired.cs' '// still a visible asset'} 'Assets/Fixture.v1/Unpaired.cs'
    Scenario 'orphan-meta' $false {param($root) Put $root 'Assets/Gone.cs.meta' (Meta ('2'*32))} '.meta files with no asset'
    Scenario 'duplicate-guid' $false {
        param($root) Put $root 'Assets/Other.cs' '// second visible asset'
        Put $root 'Assets/Other.cs.meta' (Meta ('1'*32))
    } 'GUIDs used by more than one .meta'
    Scenario 'binary-forced-text' $false {
        param($root) Put $root '.gitattributes' '*.asset text'
        Put $root 'Assets/Binary.asset' ([string][char]0+'binary')
        Put $root 'Assets/Binary.asset.meta' (Meta ('2'*32))
    } 'Binary assets git would normalise as text'
    Scenario 'binary-unset-text' $true {
        param($root) Put $root '.gitattributes' '*.asset -text -eol'
        Put $root 'Assets/Binary.asset' ([string][char]0+'binary')
        Put $root 'Assets/Binary.asset.meta' (Meta ('2'*32))
    } 'Asset metadata OK'
    Scenario 'hidden-orphan-still-refuses' $false {param($root) Put $root 'Assets/.gone.asset.meta' (Meta ('2'*32))} '.meta files with no asset'
    Scenario 'hidden-duplicate-still-refuses' $false {
        param($root) Put $root 'Assets/.duplicate.asset' 'hidden asset'
        Put $root 'Assets/.duplicate.asset.meta' (Meta ('1'*32))
    } 'GUIDs used by more than one .meta'
    Scenario 'hidden-binary-text-still-refuses' $false {
        param($root) Put $root '.gitattributes' '*.asset text'
        Put $root 'Assets/.binary.asset' ([string][char]0+'binary')
        Put $root 'Assets/.binary.asset.meta' (Meta ('2'*32))
    } 'Binary assets git would normalise as text'
    $after=Get-ReviewFileRecord $checker 'check-asset-metadata.sh'
    if ($after.sha256 -cne $checkerPin.sha256 -or $after.bytes -ne $checkerPin.bytes) { throw 'Source checker changed during its isolated fixtures.' }
} catch { $failure=$_.Exception.Message }
finally {
    Write-ReviewNewJson ([ordered]@{accepted=($null -eq $failure -and $results.Count -eq 12);cases=$results.ToArray();failure=$failure;
        checker=$checkerPin;fixtureRoot=$fixture;projectAssetsModified=$false;unityLaunched=$false}) (Join-Path $fixture 'result.json')
}
if ($failure) { throw $failure }
Write-Output ('Asset metadata fixtures passed: '+$results.Count+'; retained: '+$fixture)
