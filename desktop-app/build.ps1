$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The Windows .NET Framework C# compiler is not installed.'
}
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /warnaserror+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Windows.Forms.DataVisualization.dll "/out:$projectRoot\RAFS-Search.exe" "$PSScriptRoot\RafsApp.cs"
if ($LASTEXITCODE -ne 0) { throw 'Desktop app compilation failed.' }
Write-Output "Desktop app built: $projectRoot\RAFS-Search.exe"
