$ErrorActionPreference = 'Stop'
$fixtureBase = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) 'GamesimMirrorFixtures'))
$fixture = Join-Path $fixtureBase ([Guid]::NewGuid().ToString('N'))
$source = Join-Path $fixture 'source'
$destination = Join-Path $fixture 'destination'
$previousAcceptance = $env:GAMESIM_ACCEPTANCE
function Put([string]$Root,[string]$Relative,[string]$Text) {
    $path = Join-Path $Root $Relative
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $path))
    [IO.File]::WriteAllText($path,$Text,[Text.UTF8Encoding]::new($false))
}
function Read([string]$Root,[string]$Relative) { [IO.File]::ReadAllText((Join-Path $Root $Relative)) }
try {
    Put $source 'ProjectSettings/ProjectVersion.txt' 'm_EditorVersion: fixture'
    Put $destination 'ProjectSettings/ProjectVersion.txt' 'm_EditorVersion: fixture'
    Put $source 'ProjectSettings/ProjectSettings.asset' "  scriptingDefineSymbols:`n    Standalone: SENTIS_ANALYTICS_ENABLED;KEEP;GAMESIM_UMA`n"
    Put $source 'Assets/Settings/PC_RPAsset.asset' "  m_GPUResidentDrawerMode: 1`n"
    Put $source 'Assets/Resources/retained.asset' 'source resource must not replace destination'
    Put $source 'Assets/Resources.meta' 'source resource GUID must not replace destination'
    Put $source 'Assets/UMAProjectData/source-only.asset' 'excluded source UMA project data'
    Put $destination 'Assets/Resources/retained.asset' 'destination resource'
    Put $destination 'Assets/Resources.meta' 'destination resource GUID'
    Put $destination 'Assets/UMAProjectData/destination-only.asset' 'destination-only UMA data'
    Put $destination 'Assets/UMAProjectData.meta' 'destination-only UMA project GUID'
    Put $source 'Assets/Ordinary/keep.txt' 'new ordinary data'
    Put $destination 'Assets/Ordinary/keep.txt' 'old ordinary data'
    Put $destination 'Assets/Ordinary/remove.txt' 'stale ordinary data'
    Put $source 'Packages/manifest.json' '{}'
    Put $source 'ArtSource/source.txt' 'fixture authoring'
    [void][IO.Directory]::CreateDirectory((Join-Path $source 'Tools'))
    foreach ($name in @('sync-acceptance.ps1','review-evidence.ps1')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $source "Tools/$name")
    }
    $env:GAMESIM_ACCEPTANCE = $destination
    & (Join-Path $source 'Tools/sync-acceptance.ps1') -DisableGpuResidentDrawer -WithoutUma
    $expected = @{
        'Assets/Resources/retained.asset'='destination resource'
        'Assets/Resources.meta'='destination resource GUID'
        'Assets/UMAProjectData/destination-only.asset'='destination-only UMA data'
        'Assets/UMAProjectData.meta'='destination-only UMA project GUID'
        'Assets/Ordinary/keep.txt'='new ordinary data'
        'ProjectSettings/ProjectSettings.asset'="  scriptingDefineSymbols:`n    Standalone: KEEP`n"
        'Assets/Settings/PC_RPAsset.asset'="  m_GPUResidentDrawerMode: 0`n"
    }
    foreach ($path in $expected.Keys) {
        if ((Read $destination $path) -cne $expected[$path]) { throw "Mirror fixture mismatch: $path" }
    }
    if (Test-Path -LiteralPath (Join-Path $destination 'Assets/Ordinary/remove.txt')) { throw 'Ordinary stale files did not mirror.' }
    if (Test-Path -LiteralPath (Join-Path $destination 'Assets/UMAProjectData/source-only.asset')) { throw 'Excluded source data entered the retained subtree.' }
    . (Join-Path $source 'Tools/review-evidence.ps1')
    $preview = New-ReviewInputManifest -ProjectRoot $source -WorkflowRoot $source -PreviewSync -RetainedRoot $destination -WithoutUma
    $actual = New-ReviewInputManifest -ProjectRoot $destination -WorkflowRoot $source -WithoutUma
    $differences = @(Compare-ReviewInputs -Before $actual -After $preview -AllowShippingGpu)
    if (@($differences | Where-Object {-not $_.allowed}).Count -ne 0) { throw 'Predicted sync inputs differ from the actual fixture mirror.' }
    Write-Output 'Ten actual mirror/prediction checks passed; only temporary fixture trees were changed.'
} finally {
    $env:GAMESIM_ACCEPTANCE = $previousAcceptance
    $resolved = [IO.Path]::GetFullPath($fixture)
    if (-not $resolved.StartsWith($fixtureBase + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture cleanup boundary.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
