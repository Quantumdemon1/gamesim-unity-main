# Supported full-suite entrypoint. Controller owns the complete snapshot/lease lifetime.
param([Parameter(Mandatory=$true)][string]$Name,[switch]$WithoutUma,[switch]$RetainInstalledUma,
    [string]$ApprovedMetaDrift,[int]$SuiteTimeoutSeconds=5400)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'run-review-suites.ps1') -Name $Name -WithoutUma:$WithoutUma -RetainInstalledUma:$RetainInstalledUma -ApprovedMetaDrift $ApprovedMetaDrift -SuiteTimeoutSeconds $SuiteTimeoutSeconds
