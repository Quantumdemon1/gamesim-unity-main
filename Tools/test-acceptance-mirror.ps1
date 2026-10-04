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
$optionalChecks = New-Object 'Collections.Generic.List[string]'
function CheckOptional([string]$Name,[bool]$Passed) {
    if (-not $Passed) { throw "Optional UMA isolation fixture failed: $Name" }
    $optionalChecks.Add($Name)
}
$guardJunction = $null
try {
    Put $source 'ProjectSettings/ProjectVersion.txt' 'm_EditorVersion: fixture'
    Put $destination 'ProjectSettings/ProjectVersion.txt' 'm_EditorVersion: fixture'
    Put $source 'ProjectSettings/ProjectSettings.asset' "  scriptingDefineSymbols:`n    Standalone: SENTIS_ANALYTICS_ENABLED;KEEP;GAMESIM_UMA`n"
    Put $source 'Assets/Settings/PC_RPAsset.asset' "  m_GPUResidentDrawerMode: 1`n"
    Put $source 'Assets/Resources/retained.asset' 'source resource must not replace destination'
    Put $source 'Assets/Resources.meta' 'source resource GUID must not replace destination'
    Put $source 'Assets/UMAProjectData/source-only.asset' 'excluded source UMA project data'
    $projectIndex = @{
        'Assets/UMAProjectData.meta' = 'project UMA folder GUID'
        'Assets/UMAProjectData/Resources.meta' = 'project UMA resources GUID'
        'Assets/UMAProjectData/Resources/AssetIndexerProject.asset' = 'source-owned garment index'
        'Assets/UMAProjectData/Resources/AssetIndexerProject.asset.meta' = 'project garment index GUID'
    }
    foreach ($path in $projectIndex.Keys) { Put $source $path $projectIndex[$path] }
    Put $destination 'Assets/Resources/retained.asset' 'destination resource'
    Put $destination 'Assets/Resources.meta' 'destination resource GUID'
    Put $destination 'Assets/UMAProjectData/destination-only.asset' 'destination-only UMA data'
    Put $destination 'Assets/UMAProjectData.meta' 'destination-only UMA project GUID'
    Put $source 'Assets/Ordinary/keep.txt' 'new ordinary data'
    Put $source 'Assets/UMA/source-only.asset' 'excluded vendor dependency'
    Put $source 'Assets/Gamesim/Uma/Adapter.cs' 'retained optional provider code'
    Put $source 'Assets/Gamesim/Uma/Gamesim.Uma.asmdef' 'retained optional provider assembly'
    $optionalContent = @{
        'Assets/Gamesim/Uma/Content/HouseGarments/source-slot.asset' = 'source native slot'
        'Assets/Gamesim/Uma/Content.meta' = 'source native content GUID'
        'Assets/Gamesim/Uma/Resources/Gamesim/CharacterCatalog/source-catalog.asset' = 'source native catalog'
        'Assets/Gamesim/Uma/Resources.meta' = 'source native resources GUID'
    }
    foreach ($path in $optionalContent.Keys) { Put $source $path $optionalContent[$path] }
    Put $destination 'Assets/Gamesim/Uma/Content/stale-slot.asset' 'stale missing-type slot'
    Put $destination 'Assets/Gamesim/Uma/Content.meta' 'stale content GUID'
    Put $destination 'Assets/Gamesim/Uma/Resources/stale-catalog.asset' 'stale missing-type catalog'
    Put $destination 'Assets/Gamesim/Uma/Resources.meta' 'stale resources GUID'
    Put $destination 'Assets/Ordinary/keep.txt' 'old ordinary data'
    Put $destination 'Assets/Ordinary/remove.txt' 'stale ordinary data'
    Put $source 'Packages/manifest.json' '{}'
    Put $source 'ArtSource/source.txt' 'fixture authoring'
    [void][IO.Directory]::CreateDirectory((Join-Path $source 'Tools'))
    foreach ($name in @('sync-acceptance.ps1','review-evidence.ps1')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $source "Tools/$name")
    }
    $env:GAMESIM_ACCEPTANCE = $destination
    . (Join-Path $source 'Tools/review-evidence.ps1')
    $rejected = $false
    try { Assert-ReviewNoUmaContentAbsent $destination } catch {
        if ($_.Exception.Message -notlike '*omit optional native content*') { throw }
        $rejected = $true
    }
    CheckOptional 'Configuration refuses stale native content before isolation' $rejected
    # Predict while the stale destination still exists: /XD alone would preserve it.
    $preview = New-ReviewInputManifest -ProjectRoot $source -WorkflowRoot $source -PreviewSync -RetainedRoot $destination -WithoutUma
    CheckOptional 'Preview omits optional content/resources while retaining code' (
        @($preview.files | Where-Object {$_.path -match '^Assets/Gamesim/Uma/(Content|Resources)(/|\.meta$)'}).Count -eq 0 -and
        @($preview.files | Where-Object {$_.path -eq 'Assets/Gamesim/Uma/Adapter.cs'}).Count -eq 1)
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
    foreach ($path in @(Get-ReviewOptionalUmaContentPaths)) {
        CheckOptional "Source and stale destination omitted: $path" (-not (Test-Path -LiteralPath (Join-Path $destination $path)))
    }
    CheckOptional 'NoUMA keeps provider code and asmdef' (
        (Read $destination 'Assets/Gamesim/Uma/Adapter.cs') -ceq 'retained optional provider code' -and
        (Read $destination 'Assets/Gamesim/Uma/Gamesim.Uma.asmdef') -ceq 'retained optional provider assembly')
    CheckOptional 'NoUMA never imports vendor package' (-not (Test-Path -LiteralPath (Join-Path $destination 'Assets/UMA')))
    Assert-ReviewConfiguration $destination $true 0
    $actual = New-ReviewInputManifest -ProjectRoot $destination -WorkflowRoot $source -WithoutUma
    $differences = @(Compare-ReviewInputs -Before $actual -After $preview -AllowShippingGpu)
    if (@($differences | Where-Object {-not $_.allowed}).Count -ne 0) { throw 'Predicted sync inputs differ from the actual fixture mirror.' }
    CheckOptional 'Pre-sync NoUMA preview matches isolated copy under the existing GPU rule' (
        @($differences | Where-Object {$_.path -ne 'Assets/Settings/PC_RPAsset.asset'}).Count -eq 0)
    if (Test-Path -LiteralPath (Join-Path $destination 'Assets/UMAProjectData/Resources/AssetIndexerProject.asset')) {
        throw 'The NoUMA copy received the optional source-owned index.'
    }
    # SDK and idle checks must fail before removing even stale optional native assets.
    Put $destination 'Assets/Gamesim/Uma/Content/guarded.asset' 'guarded optional content'
    Put $destination 'Assets/UMA/installed.asset' 'installed dependency'
    $rejected = $false
    try { & (Join-Path $source 'Tools/sync-acceptance.ps1') -WithoutUma } catch {
        if ($_.Exception.Message -notlike '*UMA-free acceptance copy*') { throw }
        $rejected = $true
    }
    CheckOptional 'Installed SDK guard precedes optional cleanup' ($rejected -and (Read $destination 'Assets/Gamesim/Uma/Content/guarded.asset') -ceq 'guarded optional content')
    $fixtureSdk = [IO.Path]::GetFullPath((Join-Path $destination 'Assets/UMA'))
    if (-not $fixtureSdk.StartsWith($fixtureBase + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture SDK cleanup boundary.' }
    Remove-Item -LiteralPath $fixtureSdk -Recurse -Force
    function Get-CimInstance {
        param([string]$ClassName,[string]$Filter)
        [pscustomobject]@{Name='Unity.exe';CommandLine=('Unity.exe -projectPath "' + $destination + '"')}
    }
    try {
        $rejected = $false
        try { & (Join-Path $source 'Tools/sync-acceptance.ps1') -WithoutUma } catch {
            if ($_.Exception.Message -notlike '*editor is still running*') { throw }
            $rejected = $true
        }
        CheckOptional 'Idle guard precedes optional cleanup' ($rejected -and (Read $destination 'Assets/Gamesim/Uma/Content/guarded.asset') -ceq 'guarded optional content')
    } finally { Remove-Item Function:Get-CimInstance }
    Put $fixture 'protected/keep.asset' 'outside optional removal target'
    $guardJunction = Join-Path $destination 'Assets/Gamesim/Uma/Content/outside-link'
    [void](New-Item -ItemType Junction -Path $guardJunction -Target (Join-Path $fixture 'protected'))
    try {
        $rejected = $false
        try { & (Join-Path $source 'Tools/sync-acceptance.ps1') -WithoutUma } catch {
            if ($_.Exception.Message -notlike '*reparse child*') { throw }
            $rejected = $true
        }
        CheckOptional 'Nested junction fails before any optional cleanup' ($rejected -and
            (Read $destination 'Assets/Gamesim/Uma/Content/guarded.asset') -ceq 'guarded optional content' -and
            (Read $fixture 'protected/keep.asset') -ceq 'outside optional removal target')
    } finally {
        # Nonrecursive removal unlinks this fixture-owned junction, preserving its target.
        [IO.Directory]::Delete($guardJunction); $guardJunction = $null
    }
    & (Join-Path $source 'Tools/sync-acceptance.ps1') -DisableGpuResidentDrawer -WithoutUma
    CheckOptional 'Root retained caches survive optional cleanup' (
        (Read $destination 'Assets/Resources/retained.asset') -ceq 'destination resource' -and
        (Read $destination 'Assets/UMAProjectData/destination-only.asset') -ceq 'destination-only UMA data')
    Put $source 'Assets/UMA/installed.asset' 'installed dependency'
    Put $destination 'Assets/UMA/installed.asset' 'installed dependency'
    Put $destination 'Assets/UMAProjectData/Resources/AssetIndexerProject.asset' 'old retained index'
    Put $destination 'Assets/UMAProjectData/Resources/AssetIndexerProject.asset.meta' 'old retained index GUID'
    & (Join-Path $source 'Tools/sync-acceptance.ps1') -DisableGpuResidentDrawer
    foreach ($path in $optionalContent.Keys) {
        CheckOptional "UMA copies optional content/resources: $path" ((Read $destination $path) -ceq $optionalContent[$path])
    }
    foreach ($path in $projectIndex.Keys) {
        if ((Read $destination $path) -cne $projectIndex[$path]) { throw "The project-owned index was not copied: $path" }
    }
    if ((Read $destination 'Assets/UMAProjectData/destination-only.asset') -cne 'destination-only UMA data' -or
        (Read $destination 'Assets/Resources/retained.asset') -cne 'destination resource') {
        throw 'Copying the project-owned index changed local retained data.'
    }
    $umaPreview = New-ReviewInputManifest -ProjectRoot $source -WorkflowRoot $source -PreviewSync -RetainedRoot $destination
    $umaActual = New-ReviewInputManifest -ProjectRoot $destination -WorkflowRoot $source
    $umaDifferences = @(Compare-ReviewInputs -Before $umaActual -After $umaPreview -AllowShippingGpu)
    if (@($umaDifferences | Where-Object {-not $_.allowed}).Count -ne 0) {
        throw 'Predicted UMA index override inputs differ from the actual fixture mirror.'
    }
    # Validate the four-file group before any ordinary mirror can change the copy.
    $missingMeta = Join-Path $source 'Assets/UMAProjectData/Resources/AssetIndexerProject.asset.meta'
    Remove-Item -LiteralPath $missingMeta
    Put $source 'Assets/Ordinary/keep.txt' 'must not enter incomplete index candidate'
    $rejected = $false
    try { & (Join-Path $source 'Tools/sync-acceptance.ps1') } catch {
        if ($_.Exception.Message -notlike '*source-owned UMA index is incomplete*') { throw }
        $rejected = $true
    }
    if (-not $rejected -or (Read $destination 'Assets/Ordinary/keep.txt') -cne 'new ordinary data') {
        throw 'An incomplete index did not fail before mirror mutation.'
    }
    Write-Output ("Existing mirror/prediction fixtures and {0} optional-content checks passed; only temporary fixture trees were changed." -f $optionalChecks.Count)
    $optionalChecks | ForEach-Object { Write-Output ("Passed: " + $_) }
} finally {
    $env:GAMESIM_ACCEPTANCE = $previousAcceptance
    if ($guardJunction) { [IO.Directory]::Delete($guardJunction) }
    $resolved = [IO.Path]::GetFullPath($fixture)
    if (-not $resolved.StartsWith($fixtureBase + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture cleanup boundary.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
