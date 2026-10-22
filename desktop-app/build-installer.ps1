param([string]$PackageDirectory, [string]$Compiler,[string]$CertificateThumbprint,[uri]$TimestampServer)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $PackageDirectory) { $PackageDirectory = Get-Content (Join-Path $projectRoot 'target\package-location.txt') }
$PackageDirectory = [IO.Path]::GetFullPath($PackageDirectory)
if (-not $Compiler) {
    $candidates = @((Join-Path $projectRoot 'target\tools\InnoSetup\ISCC.exe'), 'C:\Program Files\Inno Setup 7\ISCC.exe', 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe')
    $Compiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $Compiler) { throw 'Install Inno Setup from https://jrsoftware.org/isdl.php or pass -Compiler with its ISCC.exe path.' }
$releaseName = [IO.Path]::GetFileName($PackageDirectory)
$outputDirectory = [IO.Path]::GetDirectoryName($PackageDirectory)
& $Compiler "/DPackageDir=$PackageDirectory" "/DOutputDir=$outputDirectory" "/DReleaseName=$releaseName" "$PSScriptRoot\installer.iss"
if ($LASTEXITCODE -ne 0) { throw 'Windows installer compilation failed.' }
$installer = Join-Path $outputDirectory ($releaseName + '-Setup.exe')
if($CertificateThumbprint){
    if(-not $TimestampServer){throw 'A timestamp service is required for signing.'}
    & "$PSScriptRoot\sign-release.ps1" -CertificateThumbprint $CertificateThumbprint -TimestampServer $TimestampServer -Files @($installer)
}
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($installer+'.sha256', $hash+'  '+[IO.Path]::GetFileName($installer)+[Environment]::NewLine)
Write-Output "Windows installer: $installer"
