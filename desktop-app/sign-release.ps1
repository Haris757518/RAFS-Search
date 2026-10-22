param([Parameter(Mandatory=$true)][string]$CertificateThumbprint,
      [Parameter(Mandatory=$true)][string[]]$Files,
      [Parameter(Mandatory=$true)][uri]$TimestampServer)
$ErrorActionPreference = 'Stop'
if ($TimestampServer.Scheme -notin @('http','https')) { throw 'Supply your certificate provider timestamp service URL.' }
$certificate = Get-Item -LiteralPath ("Cert:\CurrentUser\My\"+$CertificateThumbprint)
if (-not $certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date)) { throw 'A valid code-signing certificate with its private key is required.' }
$canSign=$false
foreach($extension in $certificate.Extensions){if($extension -is [Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]){foreach($usage in $extension.EnhancedKeyUsages){if($usage.Value -eq '1.3.6.1.5.5.7.3.3'){$canSign=$true}}}}
if(-not $canSign){throw 'The certificate is not valid for code signing.'}
foreach ($file in $Files) {
    $signature = Set-AuthenticodeSignature -LiteralPath $file -Certificate $certificate -HashAlgorithm SHA256 -TimestampServer $TimestampServer.AbsoluteUri
    if ($signature.Status -ne 'Valid') { throw "Signature verification failed: $file ($($signature.Status))" }
    $checksum = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText($file+'.sha256', $checksum+'  '+[IO.Path]::GetFileName($file)+[Environment]::NewLine)
}
