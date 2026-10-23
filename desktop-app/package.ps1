param([switch]$SkipEngineBuild,[string]$CertificateThumbprint,[uri]$TimestampServer)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    if (-not $SkipEngineBuild) {
        & cargo build --release --locked
        if ($LASTEXITCODE -ne 0) { throw 'Rust engine build failed.' }
    }
    $releaseName = 'RAFS-Search-Windows-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
    $releaseRoot = Join-Path $projectRoot 'target\releases'
    $packageDirectory = Join-Path $releaseRoot $releaseName
    New-Item -ItemType Directory -Path (Join-Path $packageDirectory 'engine') -Force | Out-Null
    & "$PSScriptRoot\build.ps1" -OutputDirectory $packageDirectory
    Copy-Item -LiteralPath (Join-Path $projectRoot 'target\release\fsearch.exe') -Destination (Join-Path $packageDirectory 'engine\fsearch.exe')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $packageDirectory
    Copy-Item -LiteralPath (Join-Path $projectRoot 'AUTHORS.md') -Destination $packageDirectory
    Copy-Item -LiteralPath (Join-Path $projectRoot 'PRODUCTION_GUIDE.md') -Destination (Join-Path $packageDirectory 'README.md')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'demo-data') -Destination $packageDirectory -Recurse
    if($CertificateThumbprint){
        if(-not $TimestampServer){throw 'A timestamp service is required for a signed release.'}
        & "$PSScriptRoot\sign-release.ps1" -CertificateThumbprint $CertificateThumbprint -TimestampServer $TimestampServer -Files @((Join-Path $packageDirectory 'RAFS-Search.exe'),(Join-Path $packageDirectory 'engine\fsearch.exe'))
    }
    # Package only explicit public files: no academic documents, datasets, preferences or test fixtures.
    $zip = Join-Path $releaseRoot ($releaseName + '.zip')
    Compress-Archive -LiteralPath $packageDirectory -DestinationPath $zip
    $checksum = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText($zip + '.sha256', $checksum + '  ' + [IO.Path]::GetFileName($zip) + [Environment]::NewLine)
    [IO.File]::WriteAllText((Join-Path $projectRoot 'target\package-location.txt'), $packageDirectory)
    Write-Output "Portable release: $zip"
} finally { Pop-Location }
