param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if(-not $OutputDirectory){$OutputDirectory=$projectRoot}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The Windows .NET Framework C# compiler is not installed.'
}
$sourceFiles = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -Recurse | Sort-Object FullName | ForEach-Object FullName)
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /warnaserror+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:System.Windows.Forms.DataVisualization.dll "/win32manifest:$PSScriptRoot\app.manifest" "/out:$OutputDirectory\RAFS-Search.exe" $sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Desktop app compilation failed.' }
Copy-Item -LiteralPath "$PSScriptRoot\app.config" -Destination "$OutputDirectory\RAFS-Search.exe.config" -Force
Write-Output "Desktop app built: $OutputDirectory\RAFS-Search.exe"
