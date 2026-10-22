# Supported full-suite entrypoint. Controller owns the complete snapshot/lease lifetime.
# -SuiteTimeoutSeconds 0 (the default) gives each suite its own measured deadline; see
# Get-ReviewSuiteDeadline in review-workflow.ps1. A late run is recorded failed and never killed.
param([Parameter(Mandatory=$true)][string]$Name,[switch]$WithoutUma,[switch]$RetainInstalledUma,
    [string]$ApprovedMetaDrift,[int]$SuiteTimeoutSeconds=0)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'run-review-suites.ps1') -Name $Name -WithoutUma:$WithoutUma -RetainInstalledUma:$RetainInstalledUma -ApprovedMetaDrift $ApprovedMetaDrift -SuiteTimeoutSeconds $SuiteTimeoutSeconds
