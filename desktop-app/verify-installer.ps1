param([string]$Installer,[string]$TestDirectory)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
if(-not $Installer){$Installer=(Get-Content (Join-Path $projectRoot 'target\package-location.txt'))+'-Setup.exe'}
if(-not $TestDirectory){$TestDirectory=Join-Path $projectRoot ('target\install-test-'+[Guid]::NewGuid().ToString('N').Substring(0,8))}
$TestDirectory=[IO.Path]::GetFullPath($TestDirectory)
$allowed=[IO.Path]::GetFullPath((Join-Path $projectRoot 'target'))+[IO.Path]::DirectorySeparatorChar
if(-not $TestDirectory.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)-or(Test-Path -LiteralPath $TestDirectory)){throw 'Installer verification requires a new directory inside this project target folder.'}
$registration='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{A942A197-D009-4938-84C9-26EB27AF7BD8}_is1'
if(Test-Path -LiteralPath $registration){throw 'An existing RAFS installation is registered. Verification will not overwrite its registration.'}
$log=Join-Path $projectRoot 'target\installer-verification.txt'
$notes=@()
function Run-Setup {
    $process=Start-Process -FilePath $Installer -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="'+$TestDirectory+'"') -WindowStyle Hidden -Wait -PassThru
    if($process.ExitCode -ne 0){throw "Installer failed: $($process.ExitCode)"}
}
Run-Setup
if(-not(Test-Path -LiteralPath (Join-Path $TestDirectory 'engine\fsearch.exe'))-or -not(Test-Path -LiteralPath $registration)){throw 'Installation files or uninstall registration missing.'}
$notes+='PASS clean per-user installation and uninstall registration'
$sentinel=Join-Path $TestDirectory 'user-kept.txt'
[IO.File]::WriteAllText($sentinel,'This file is not owned by the installer and must survive upgrade and uninstall.')
$test=Start-Process -FilePath (Join-Path $TestDirectory 'RAFS-Search.exe') -ArgumentList '--production-test' -WindowStyle Hidden -PassThru -Wait
if($test.ExitCode -ne 0){throw 'Installed app production verification failed.'}
$notes+='PASS installed app production checks and bundled demo'
Run-Setup
if(-not(Test-Path -LiteralPath $sentinel)){throw 'Upgrade removed a user file.'}
$notes+='PASS reinstall/upgrade preserves user files'
$uninstaller=Join-Path $TestDirectory 'unins000.exe'
if(-not(Test-Path -LiteralPath $uninstaller)){throw 'Uninstaller is missing.'}
$remove=Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -WindowStyle Hidden -Wait -PassThru
if($remove.ExitCode -ne 0){throw 'Uninstaller failed.'}
if((Test-Path -LiteralPath (Join-Path $TestDirectory 'RAFS-Search.exe'))-or(Test-Path -LiteralPath $registration)-or -not(Test-Path -LiteralPath $sentinel)){throw 'Uninstall ownership checks failed.'}
$notes+='PASS uninstall removes application and registration while preserving user files'
[IO.File]::WriteAllLines($log,$notes)
Write-Output $notes
Write-Output "Verification fixtures retained: $TestDirectory"
