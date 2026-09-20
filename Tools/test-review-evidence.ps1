# Pure PowerShell fixture checks. No Unity, project mirroring, or acceptance-copy writes.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'review-evidence.ps1')
$fixtureBase = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) 'GamesimEvidenceFixtures'))
$fixture = Join-Path $fixtureBase ([Guid]::NewGuid().ToString('N'))
$source = Join-Path $fixture 'source'
$retained = Join-Path $fixture 'retained'
$checks = 0
function Assert-Evidence([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:checks++
}
function Put-Fixture([string]$Root, [string]$Relative, [string]$Text) {
    $file = Join-Path $Root $Relative
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $file))
    [IO.File]::WriteAllText($file, $Text, [Text.UTF8Encoding]::new($false))
}
function Snapshot { New-ReviewInputManifest -ProjectRoot $source -WorkflowRoot $source }
try {
    Put-Fixture $source 'Assets/Settings/PC_RPAsset.asset' "  m_GPUResidentDrawerMode: 0`n  other: 7`n"
    Put-Fixture $source 'Assets/UMA/local.dat' 'local UMA dependency'
    Put-Fixture $source 'Assets/UMA.meta' 'local UMA folder guid'
    Put-Fixture $source 'Assets/InputSystem_Actions.inputactions' 'real input actions'
    Put-Fixture $source 'Assets/Gamesim/Art/Authored/SetPieces/pool.fbx.meta' "guid: stable`n    materialLocation: 0`n    materialName: 1`n"
    Put-Fixture $source 'ProjectSettings/ProjectSettings.asset' "  defaultScreenWidth: 1600`n  defaultScreenHeight: 900`n  fullscreenMode: 3`n  resizableWindow: 1`n  preloadedAssets:`n  - {fileID: -944628639613478452, guid: 052faaac586de48259a63d0c4782560b, type: 3}`n  scriptingDefineSymbols:`n    Standalone: GAMESIM_UMA`n"
    Put-Fixture $source 'Packages/manifest.json' '{}'
    Put-Fixture $source 'ArtSource/source.py' 'authored source'
    Put-Fixture $source 'Tools/runner.ps1' 'workflow'
    $before = Snapshot
    Assert-Evidence ($before.files.path -contains 'Assets/UMA/local.dat') 'Local UMA must be fingerprinted.'
    Assert-Evidence ($before.files.path -contains 'Assets/InputSystem_Actions.inputactions') 'Input actions must be fingerprinted.'
    Assert-Evidence ($before.files.path -contains 'ArtSource/source.py') 'Authored test inputs must be fingerprinted.'
    Assert-Evidence ($before.files.path -contains 'Tools/runner.ps1') 'Workflow must be fingerprinted.'
    Assert-Evidence (@(Compare-ReviewInputs $before (Snapshot)).Count -eq 0) 'Unchanged inputs should match.'
    $archive = Join-Path $fixture 'metadata-before'
    $archived = New-ReviewInputManifest -ProjectRoot $source -WorkflowRoot $source -MetaArchive $archive
    Assert-Evidence ((Get-ReviewHash (Join-Path $archive 'Assets/UMA.meta')) -eq
        ($archived.files | Where-Object path -eq 'Assets/UMA.meta').sha256) 'Imported metadata must be retained as exact bytes, not only hashes.'
    Assert-ReviewConfiguration $source $false 0
    Put-Fixture $source 'Assets/Settings/PC_RPAsset.asset' "  m_GPUResidentDrawerMode: 1`n  other: 7`n"
    $shipping = Snapshot
    Assert-Evidence (@(Compare-ReviewInputs $before $shipping | Where-Object { -not $_.allowed }).Count -eq 1) 'Unapproved rendering drift must fail.'
    Assert-Evidence (@(Compare-ReviewInputs $before $shipping -AllowShippingGpu | Where-Object { -not $_.allowed }).Count -eq 0) 'Only explicit test-to-shipping GPU normalization is allowed.'
    Put-Fixture $source 'Assets/Settings/PC_RPAsset.asset' "  m_GPUResidentDrawerMode: 1`n  other: 8`n"
    Assert-Evidence (@(Compare-ReviewInputs $before (Snapshot) -AllowShippingGpu | Where-Object { -not $_.allowed }).Count -eq 1) 'GPU normalization cannot hide another setting change.'
    Put-Fixture $source 'Assets/Settings/PC_RPAsset.asset' "  m_GPUResidentDrawerMode: 0`n  other: 7`n"
    Put-Fixture $source 'Assets/Gamesim/Art/Authored/SetPieces/pool.fbx.meta' "guid: stable`n    materialLocation: 1`n    materialName: 1`n"
    $imported = Snapshot
    Assert-Evidence (@(Compare-ReviewInputs $before $imported -AllowImportMeta | Where-Object { -not $_.allowed }).Count -eq 0) 'The exact supported material import migration should be documented and allowed.'
    Assert-Evidence (@(Compare-ReviewInputs $before $imported | Where-Object { -not $_.allowed }).Count -eq 1) 'Pre-build source matching must not silently allow import drift.'
    Put-Fixture $source 'Assets/Gamesim/Art/Authored/SetPieces/pool.fbx.meta' "guid: changed`n    materialLocation: 1`n    materialName: 1`n"
    $changed = Snapshot
    $differences = @(Compare-ReviewInputs $before $changed -AllowImportMeta)
    Assert-Evidence (@($differences | Where-Object { -not $_.allowed }).Count -eq 1) 'Never ignore an arbitrary meta/GUID change.'
    $approval = @([pscustomobject]@{path=$differences[0].path;beforeSha256=$differences[0].beforeSha256;afterSha256=$differences[0].afterSha256;reason='Reviewed synthetic fixture GUID change.'})
    Assert-Evidence (@(Compare-ReviewInputs $before $changed -AllowImportMeta -ApprovedMetaDrift $approval | Where-Object { -not $_.allowed }).Count -eq 0) 'An exact reviewed meta pair can be accounted for.'
    $approval[0].afterSha256 = ('0' * 64)
    Assert-Evidence (@(Compare-ReviewInputs $before $changed -AllowImportMeta -ApprovedMetaDrift $approval | Where-Object { -not $_.allowed }).Count -eq 1) 'Approvals cannot authorize a different resulting meta.'
    Put-Fixture $source 'Assets/Gamesim/Art/Authored/SetPieces/pool.fbx.meta' "guid: stable`n    materialLocation: 0`n    materialName: 1`n"
    Put-Fixture $source 'Tools/runner.ps1' 'reviewed newer workflow'
    $workflow = @(Compare-ReviewInputs $before (Snapshot))
    Assert-Evidence ($workflow.Count -eq 1 -and $workflow[0].kind -eq 'workflow-change' -and $workflow[0].allowed) 'Workflow differences remain visible without mislabelling product drift.'
    Put-Fixture $source 'Tools/runner.ps1' 'workflow'
    $settingsPath = Join-Path $source 'ProjectSettings/ProjectSettings.asset'
    $settings = [IO.File]::ReadAllText($settingsPath)
    Put-Fixture $source 'ProjectSettings/ProjectSettings.asset' ($settings.Replace('defaultScreenWidth: 1600','defaultScreenWidth: 1280'))
    $wrongSettings = Snapshot
    Assert-Evidence (@(Compare-ReviewInputs $before $wrongSettings -AllowBuildSettings | Where-Object { -not $_.allowed }).Count -eq 1) 'Build normalization cannot approve the wrong output settings.'
    Put-Fixture $source 'ProjectSettings/ProjectSettings.asset' $settings
    Assert-Evidence (@(Compare-ReviewInputs $wrongSettings (Snapshot) -AllowBuildSettings | Where-Object { -not $_.allowed }).Count -eq 0) 'U01 can apply its exact configured window size.'
    Put-Fixture $source 'Assets/Resources/local.asset' 'live resource must not overwrite retained resource'
    Put-Fixture $retained 'Assets/Resources/local.asset' 'acceptance local UMA index'
    Put-Fixture $source 'Assets/Resources.meta' 'live resources folder guid'
    Put-Fixture $retained 'Assets/Resources.meta' 'retained resources folder guid'
    Put-Fixture $retained 'Assets/UMAProjectData/local.asset' 'retained UMA project settings'
    Put-Fixture $retained 'Assets/UMAProjectData.meta' 'retained UMA project folder guid'
    $preview = New-ReviewInputManifest -ProjectRoot $source -WorkflowRoot $source -PreviewSync -RetainedRoot $retained -WithoutUma
    Assert-Evidence (@($preview.files | Where-Object { $_.path -match '^Assets/UMA(/|\.meta$)' }).Count -eq 0) 'UMA-free preview excludes only the installed package and its folder meta.'
    Assert-Evidence (($preview.files | Where-Object path -eq 'Assets/Resources/local.asset').sha256 -eq (Get-ReviewTextHash 'acceptance local UMA index')) 'Retained acceptance resources must participate in source preview.'
    Assert-Evidence (($preview.files | Where-Object path -eq 'Assets/Resources.meta').sha256 -eq (Get-ReviewTextHash 'retained resources folder guid')) 'Retained resources keep their own folder GUID.'
    Assert-Evidence (($preview.files | Where-Object path -eq 'Assets/UMAProjectData.meta').sha256 -eq (Get-ReviewTextHash 'retained UMA project folder guid')) 'A retained folder meta absent from live source must remain in the preview.'
    Assert-Evidence (($preview.files | Where-Object path -eq 'ProjectSettings/ProjectSettings.asset').sha256 -eq (Get-ReviewTextHash ($settings.Replace('GAMESIM_UMA','')))) 'UMA-free preview must account for the exact local define removal.'
    $packageSettings = "  label: SENTIS_ANALYTICS_ENABLED`r`n  scriptingDefineSymbols:`r`n    Standalone: SENTIS_ANALYTICS_ENABLED;APP_UI_EDITOR_ONLY;GAMESIM_UMA;CUSTOM_SENTIS_ANALYTICS_ENABLED`r`n    Android: SENTIS_ANALYTICS_ENABLED;GAMESIM_UMA`r`n"
    $umaSettings = $packageSettings.Replace('Standalone: SENTIS_ANALYTICS_ENABLED;', 'Standalone: ')
    Assert-Evidence ((ConvertTo-ReviewAcceptanceSettings $packageSettings $false) -ceq $umaSettings) 'Batch settings may remove only the exact Standalone analytics token, preserving other platform symbols and line endings.'
    $noUmaSettings = $umaSettings.Replace('Standalone: APP_UI_EDITOR_ONLY;GAMESIM_UMA;', 'Standalone: APP_UI_EDITOR_ONLY;')
    Assert-Evidence ((ConvertTo-ReviewAcceptanceSettings $packageSettings $true) -ceq $noUmaSettings) 'Authored-only settings remove only the exact Standalone UMA token.'
    Assert-Evidence ((ConvertTo-ReviewAcceptanceSettings $noUmaSettings $true) -ceq $noUmaSettings) 'Acceptance settings normalization must be idempotent.'
    $caseSensitiveSettings = "  scriptingDefineSymbols:`n    Standalone: sentis_analytics_enabled;gamesim_uma;SENTIS_ANALYTICS_ENABLED;GAMESIM_UMA`n"
    Assert-Evidence ((ConvertTo-ReviewAcceptanceSettings $caseSensitiveSettings $true) -ceq "  scriptingDefineSymbols:`n    Standalone: sentis_analytics_enabled;gamesim_uma`n") 'C# scripting symbol names are case-sensitive; different-case tokens must stay intact.'
    $otherSettings = "  unrelatedSettings:`n    Standalone: SENTIS_ANALYTICS_ENABLED;GAMESIM_UMA`n" + $packageSettings
    Assert-Evidence ((ConvertTo-ReviewAcceptanceSettings $otherSettings $true) -ceq ("  unrelatedSettings:`n    Standalone: SENTIS_ANALYTICS_ENABLED;GAMESIM_UMA`n" + $noUmaSettings)) 'Only scriptingDefineSymbols may be transformed, not other platform settings.'
    $emptyJson = Join-Path $fixture 'empty.json'
    Write-ReviewJson @() $emptyJson
    Assert-Evidence (([IO.File]::ReadAllText($emptyJson)).Trim() -match '^\[\s*\]$') 'An empty drift report must remain valid JSON.'
    Write-Output "$checks evidence checks passed; no Unity or acceptance copy used."
} finally {
    $resolved = [IO.Path]::GetFullPath($fixture)
    if (-not $resolved.StartsWith($fixtureBase + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture cleanup boundary.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
