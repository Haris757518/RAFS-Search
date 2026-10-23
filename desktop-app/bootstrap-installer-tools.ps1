param([string]$ToolsDirectory)
$ErrorActionPreference='Stop'
if(-not $ToolsDirectory){$ToolsDirectory=Join-Path (Split-Path -Parent $PSScriptRoot) 'target\tools'}
New-Item -ItemType Directory -Path $ToolsDirectory -Force | Out-Null
$download=Join-Path $ToolsDirectory 'innosetup-7.1.0-x64.exe'
Invoke-WebRequest -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_0/innosetup-7.1.0-x64.exe' -OutFile $download
$signature=Get-AuthenticodeSignature -LiteralPath $download
if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Pyrsys B.V.') {throw 'Inno Setup publisher signature verification failed.'}
$compilerDirectory=Join-Path ([IO.Path]::GetFullPath($ToolsDirectory)) 'InnoSetup'
$setup=Start-Process -FilePath $download -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/CURRENTUSER','/NOICONS',('/DIR="'+$compilerDirectory+'"') -WindowStyle Hidden -Wait -PassThru
if($setup.ExitCode -ne 0){throw 'Inno Setup compiler installation failed.'}
Write-Output (Join-Path $compilerDirectory 'ISCC.exe')
