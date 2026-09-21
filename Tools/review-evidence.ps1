# Shared, read-only input accounting. Dot-source; this file never launches Unity or mirrors files.
function Get-ReviewHash([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}
function Get-ReviewTextHash([string]$Text) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text)))).Replace('-','').ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}
function Write-ReviewJson($Value, [string]$Path) {
    ConvertTo-Json -InputObject $Value -Depth 12 | Set-Content -LiteralPath $Path -Encoding utf8
}
function ConvertTo-ReviewAcceptanceSettings([string]$Text, [bool]$WithoutUma) {
    # The installed inference package removes its editor analytics symbol on batch startup.
    # Apply that same exact Standalone-token change before hashing; never alter the live settings.
    [regex]::Replace($Text, '(?m)^(  scriptingDefineSymbols:\r?\n)((?:    [^\r\n]*(?:\r?\n|$))*)', [Text.RegularExpressions.MatchEvaluator]{
        param($block)
        $defines = [regex]::Replace($block.Groups[2].Value, '(?m)^(    Standalone: )([^\r\n]*)', [Text.RegularExpressions.MatchEvaluator]{
            param($match)
            $tokens = @($match.Groups[2].Value.Split(';') | Where-Object {
                $_ -cne 'SENTIS_ANALYTICS_ENABLED' -and (-not $WithoutUma -or $_ -cne 'GAMESIM_UMA')
            })
            $match.Groups[1].Value + ($tokens -join ';')
        })
        $block.Groups[1].Value + $defines
    })
}
function ConvertTo-ReviewBuildSettings([string]$Text) {
    # Only U01ProjectSetup.BuildConfiguredPort's explicit settings writes are normalized, plus the
    # inference package's own analytics symbol.
    #
    # sync-acceptance.ps1 strips SENTIS_ANALYTICS_ENABLED from the acceptance copy, and running the
    # editor to produce a player build hands it straight back: the post-build file differed from the
    # pre-build one by exactly ';SENTIS_ANALYTICS_ENABLED' and 25 bytes, which the audit could only
    # read as a build that had rewritten a committed input. The token is the package's rather than
    # the product's - the same judgement the sync already makes - and the committed project carries
    # it, so a build putting it back moves the copy towards the committed state rather than away
    # from it. Neutralizing it on both sides here is exactly that judgement and nothing wider:
    # $false keeps GAMESIM_UMA significant, the strip is Standalone-only, and every other define
    # still counts. It is idempotent, so the preview path having applied it already is harmless.
    #
    # Deliberately NOT normalized: the ORDER of the defines. No build here has ever reordered them,
    # and a safety gate should not grow tolerances for things that have not happened.
    $Text = ConvertTo-ReviewAcceptanceSettings $Text $false
    foreach ($setting in @(@('defaultScreenWidth','1600'), @('defaultScreenHeight','900'),
        @('fullscreenMode','3'), @('resizableWindow','1'))) {
        $Text = [regex]::Replace($Text, '(?m)^(  ' + $setting[0] + ': )[^\r\n]*', '${1}' + $setting[1])
    }
    $inputEntry = '  - {fileID: -944628639613478452, guid: 052faaac586de48259a63d0c4782560b, type: 3}'
    # Removing just the one known input-action entry makes its addition idempotent; other preloads stay significant.
    $Text = [regex]::Replace($Text, '(?m)^' + [regex]::Escape($inputEntry) + '\r?\n', '')
    $Text = [regex]::Replace($Text, '(?m)^  preloadedAssets: \[\](?=\r?$)', '  preloadedAssets:')
    return $Text
}
function New-ReviewInputManifest {
    param([string]$ProjectRoot, [string]$WorkflowRoot, [switch]$WithoutUma,
        [string]$RetainedRoot, [switch]$PreviewSync, [string]$MetaArchive)
    $ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\','/')
    if ($MetaArchive -and (Test-Path -LiteralPath $MetaArchive)) { throw 'Use a fresh metadata archive; previous evidence is never overwritten.' }
    $records = [Collections.Generic.List[object]]::new()
    foreach ($relative in @('Assets','ProjectSettings','Packages','ArtSource','Tools')) {
        $root = if ($relative -eq 'Tools') { $WorkflowRoot } else { $ProjectRoot }
        $folder = Join-Path $root $relative
        if (-not (Test-Path -LiteralPath $folder -PathType Container)) { throw "Missing evidence input: $folder" }
        $files = @(Get-ChildItem -LiteralPath $folder -File -Recurse -Force)
        if ($PreviewSync -and $relative -eq 'Assets') {
            $files = @($files | Where-Object {
                $local = $_.FullName.Substring($ProjectRoot.Length + 1).Replace('\','/')
                $local -notmatch '^Assets/(Resources|UMAProjectData)(/|\.meta$)' -and
                    (-not $WithoutUma -or $local -notmatch '^Assets/UMA(/|\.meta$)')
            })
            foreach ($retained in @('Assets/Resources','Assets/UMAProjectData')) {
                $retainedPath = Join-Path $RetainedRoot $retained
                if (Test-Path -LiteralPath $retainedPath) { $files += @(Get-ChildItem -LiteralPath $retainedPath -File -Recurse -Force) }
                if (Test-Path -LiteralPath ($retainedPath + '.meta')) { $files += Get-Item -LiteralPath ($retainedPath + '.meta') }
            }
        }
        foreach ($file in $files) {
            $fileRoot = $root
            if ($PreviewSync -and $RetainedRoot -and $file.FullName.StartsWith($RetainedRoot.TrimEnd('\','/') + '\', [StringComparison]::OrdinalIgnoreCase)) {
                $fileRoot = $RetainedRoot.TrimEnd('\','/')
            }
            $path = $file.FullName.Substring($fileRoot.Length + 1).Replace('\','/')
            $record = [ordered]@{path=$path; bytes=$file.Length; sha256=(Get-ReviewHash $file.FullName)}
            if ($path -eq 'ProjectSettings/ProjectSettings.asset') {
                $content = [IO.File]::ReadAllText($file.FullName)
                if ($PreviewSync) {
                    $content = ConvertTo-ReviewAcceptanceSettings $content ([bool]$WithoutUma)
                    $record.sha256 = Get-ReviewTextHash $content
                    $record.bytes = [Text.Encoding]::UTF8.GetByteCount($content)
                }
                $record.buildSettingsSha256 = Get-ReviewTextHash (ConvertTo-ReviewBuildSettings $content)
                $record.configuredBuildSettings = ($content -match '(?m)^  defaultScreenWidth: 1600\r?$' -and
                    $content -match '(?m)^  defaultScreenHeight: 900\r?$' -and $content -match '(?m)^  fullscreenMode: 3\r?$' -and
                    $content -match '(?m)^  resizableWindow: 1\r?$' -and
                    $content.Contains('  - {fileID: -944628639613478452, guid: 052faaac586de48259a63d0c4782560b, type: 3}'))
            }
            if ($path -eq 'Assets/Settings/PC_RPAsset.asset') {
                $content = [IO.File]::ReadAllText($file.FullName)
                $match = [regex]::Match($content, '(?m)^  m_GPUResidentDrawerMode: ([01])\r?$')
                if (-not $match.Success) { throw 'Cannot account for the GPU Resident Drawer setting.' }
                $record.gpuResidentDrawer = [int]$match.Groups[1].Value
                $record.shippingSha256 = Get-ReviewTextHash ([regex]::Replace($content,
                    '(?m)^(  m_GPUResidentDrawerMode: )[01](?=\r?$)', '${1}1'))
            }
            if ($path -match '^Assets/Gamesim/Art/Authored/.+\.fbx\.meta$') {
                $content = [IO.File]::ReadAllText($file.FullName)
                $match = [regex]::Match($content, '(?m)^    materialLocation: ([01])\r?$')
                if ($match.Success) {
                    $record.materialLocation = [int]$match.Groups[1].Value
                    $record.supportedMaterialSha256 = Get-ReviewTextHash ([regex]::Replace($content,
                        '(?m)^(    materialLocation: )[01](?=\r?$)', '${1}1'))
                }
            }
            if ($MetaArchive -and $path.EndsWith('.meta')) {
                $archiveFile = Join-Path $MetaArchive $path
                [void][IO.Directory]::CreateDirectory((Split-Path -Parent $archiveFile))
                Copy-Item -LiteralPath $file.FullName -Destination $archiveFile
                if ((Get-ReviewHash $archiveFile) -ne $record.sha256) { throw "Input changed while capturing metadata: $path" }
            }
            $records.Add([pscustomobject]$record)
        }
    }
    [pscustomobject][ordered]@{schema=3; capturedUtc=[DateTime]::UtcNow.ToString('o'); projectRoot=$ProjectRoot;
        workflowRoot=$WorkflowRoot; umaEnabled=(-not $WithoutUma); previewSync=[bool]$PreviewSync;metadataArchive=$MetaArchive;
        coverage=@('Assets/**','ProjectSettings/**','Packages/**','ArtSource/**','Tools/** (live workflow)');
        files=@($records | Sort-Object path)}
}
function Compare-ReviewInputs {
    param($Before, $After, [switch]$AllowShippingGpu, [switch]$AllowBuildSettings,
        [switch]$AllowImportMeta, [array]$ApprovedMetaDrift=@())
    $left = @{}; $right = @{}
    foreach ($entry in $Before.files) { $left[$entry.path] = $entry }
    foreach ($entry in $After.files) { $right[$entry.path] = $entry }
    foreach ($path in @(@($left.Keys) + @($right.Keys) | Sort-Object -Unique)) {
        $old = $left[$path]; $new = $right[$path]
        if ($old -and $new -and $old.sha256 -eq $new.sha256) { continue }
        $oldHash = if ($old) { $old.sha256 } else { $null }
        $newHash = if ($new) { $new.sha256 } else { $null }
        $reason = ''; $allowed = $false; $kind = 'unexpected-product-drift'
        if ($path.StartsWith('Tools/')) {
            $allowed = $true; $kind = 'workflow-change'; $reason = 'Workflow evidence is retained separately from product inputs.'
        } elseif ($old -and $new -and $AllowShippingGpu -and $path -eq 'Assets/Settings/PC_RPAsset.asset' -and
            $old.gpuResidentDrawer -eq 0 -and $new.gpuResidentDrawer -eq 1 -and
            $old.shippingSha256 -eq $new.shippingSha256) {
            $allowed = $true; $kind = 'shipping-render-setting'; $reason = 'Only GPU Resident Drawer 0 (test harness) to 1 (shipping).'
        } elseif ($old -and $new -and $AllowBuildSettings -and $path -eq 'ProjectSettings/ProjectSettings.asset' -and
            $new.configuredBuildSettings -and $old.buildSettingsSha256 -eq $new.buildSettingsSha256) {
            $allowed = $true; $kind = 'configured-build-settings'; $reason = 'Only U01 window settings/input-action preload and the inference package analytics symbol; full before/after hashes retained.'
        } elseif ($AllowImportMeta -and $old -and $new -and $path -match '^Assets/Gamesim/Art/Authored/.+\.fbx\.meta$' -and
            $old.materialLocation -eq 0 -and $new.materialLocation -eq 1 -and
            $old.supportedMaterialSha256 -and $old.supportedMaterialSha256 -eq $new.supportedMaterialSha256) {
            $allowed = $true; $kind = 'authored-material-import'; $reason = 'AuthoredAssetImporter replaces obsolete External materialLocation0 with supported InPrefab1; no other byte content changed.'
        } else {
            foreach ($approved in $ApprovedMetaDrift) {
                if ($AllowImportMeta -and $path.EndsWith('.meta') -and $approved.path -eq $path -and $approved.beforeSha256 -eq $oldHash -and
            $approved.afterSha256 -eq $newHash -and -not [string]::IsNullOrWhiteSpace($approved.reason)) {
                    $allowed = $true; $kind = 'reviewed-import-meta'; $reason = $approved.reason; break
                }
            }
        }
        [pscustomobject]@{path=$path; beforeSha256=$oldHash; afterSha256=$newHash; allowed=$allowed; kind=$kind; reason=$reason}
    }
}
function Assert-ReviewConfiguration([string]$Root, [bool]$WithoutUma, [int]$GpuMode) {
    $installed = Test-Path -LiteralPath (Join-Path $Root 'Assets/UMA') -PathType Container
    if ($installed -eq $WithoutUma) { throw 'The acceptance UMA installation does not match the requested configuration.' }
    $settings = [IO.File]::ReadAllText((Join-Path $Root 'ProjectSettings/ProjectSettings.asset'))
    $defines = [regex]::Match($settings, '(?ms)^  scriptingDefineSymbols:\r?\n(?<entries>(?:    [^\r\n]*\r?\n)*)').Groups['entries'].Value
    $standalone = [regex]::Match($defines, '(?m)^    Standalone: ([^\r\n]*)').Groups[1].Value
    if (($standalone.Split(';') -contains 'GAMESIM_UMA') -eq $WithoutUma) { throw 'The Standalone UMA define does not match the requested configuration.' }
    $pipeline = [IO.File]::ReadAllText((Join-Path $Root 'Assets/Settings/PC_RPAsset.asset'))
    if ($pipeline -notmatch ('(?m)^  m_GPUResidentDrawerMode: ' + $GpuMode + '\r?$')) { throw 'Unexpected GPU Resident Drawer configuration.' }
}
function Read-ReviewApproval([string]$Path) {
    if (-not $Path) { return @() }
    $entries = @(Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
    foreach ($entry in $entries) {
        if ($entry.path -notmatch '^Assets/.+\.meta$' -or $entry.path -match '(^|/)\.\.(/|$)' -or
            ($entry.beforeSha256 -and $entry.beforeSha256 -notmatch '^[a-fA-F0-9]{64}$') -or
            ($entry.afterSha256 -and $entry.afterSha256 -notmatch '^[a-fA-F0-9]{64}$') -or
            -not $entry.afterSha256 -or [string]::IsNullOrWhiteSpace($entry.reason)) {
            throw 'Import approvals require an exact Assets/*.meta path, before/after SHA256 (null before only for a new .meta), and a review reason.'
        }
    }
    return $entries
}
