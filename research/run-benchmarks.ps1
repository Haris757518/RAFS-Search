param([string]$PackageDirectory,[string]$WorkloadList,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
if(-not $PackageDirectory){$PackageDirectory=Join-Path $project 'target\hybrid-stage'}
if(-not $WorkloadList){$WorkloadList=Join-Path $project 'target\release-workloads-20261022.txt'}
if(-not $OutputDirectory){$OutputDirectory=Join-Path $project ('target\research-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
if(Test-Path -LiteralPath $OutputDirectory){throw 'Choose a new output directory to preserve earlier observations.'}
$env:RAFS_RESEARCH_ROOT=$OutputDirectory
$env:RAFS_PROJECT_ROOT=$project
$env:RAFS_WORKLOAD_LIST=$WorkloadList
$env:RAFS_SOURCE_SHA=(& git -C $project rev-parse HEAD)
Remove-Item Env:RAFS_RESEARCH_SMOKE -ErrorAction SilentlyContinue
$process=Start-Process -FilePath (Join-Path $PackageDirectory 'RAFS-Search.exe') -ArgumentList '--research-benchmark' -WindowStyle Hidden -PassThru -Wait
if($process.ExitCode -ne 0){throw "Benchmark failed: $($process.ExitCode); inspect $OutputDirectory"}
Get-Content -LiteralPath (Join-Path $OutputDirectory 'COMPLETE.txt')
