param([string]$PackageDirectory)
$ErrorActionPreference='Stop'
if(-not $PackageDirectory){$PackageDirectory=Get-Content (Join-Path (Split-Path -Parent $PSScriptRoot) 'target\package-location.txt')}
$exe=Join-Path $PackageDirectory 'RAFS-Search.exe'
foreach($mode in @('--production-test','--feature-test','--layout-test','--soak-test')){
    $process=Start-Process -FilePath $exe -ArgumentList $mode -WindowStyle Hidden -PassThru
    if(-not $process.WaitForExit(180000)){if(-not $process.HasExited){$process.Kill()};throw "Release verification timed out: $mode"}
    if($process.ExitCode -ne 0){throw "Release verification failed: $mode (exit $($process.ExitCode))"}
    Write-Output "PASS $mode"
}
