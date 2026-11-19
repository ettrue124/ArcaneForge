# Creates a new local root and a TLS server certificate that also names extra
# IP addresses (for example this PC's Tailscale address, so an iPhone can
# connect). Same layout as ProtocolHost --create-local-cert: <prefix>.pfx,
# .cer, .root.cer, .password, .metadata.json. Writes files only; it never
# changes certificate trust and refuses to overwrite an existing prefix.
param(
    [Parameter(Mandatory)][string]$Prefix,
    [string[]]$IpAddress = @()
)
$ErrorActionPreference = 'Stop'
$Prefix = [System.IO.Path]::GetFullPath($Prefix)
foreach ($extension in '.pfx', '.cer', '.root.cer', '.password', '.metadata.json') {
    if (Test-Path -LiteralPath ($Prefix + $extension)) { throw "Certificate target exists; choose a fresh prefix: $Prefix$extension" }
}
$addresses = @($IpAddress | ForEach-Object { [System.Net.IPAddress]::Parse($_) })
New-Item -ItemType Directory -Force -Path (Split-Path -LiteralPath $Prefix) | Out-Null
$sha256 = [System.Security.Cryptography.HashAlgorithmName]::SHA256
$pkcs1 = [System.Security.Cryptography.RSASignaturePadding]::Pkcs1
$now = [DateTimeOffset]::UtcNow

$rootKey = [System.Security.Cryptography.RSA]::Create(2048)
$rootRequest = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=Arena Local Development Root', $rootKey, $sha256, $pkcs1)
$rootRequest.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($true, $true, 0, $true))
$rootRequest.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new('KeyCertSign, CrlSign', $true))
$root = $rootRequest.CreateSelfSigned($now.AddMinutes(-1), $now.AddDays(365))

$serverKey = [System.Security.Cryptography.RSA]::Create(2048)
$serverRequest = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=localhost', $serverKey, $sha256, $pkcs1)
$san = [System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
$san.AddDnsName('localhost')
$san.AddIpAddress([System.Net.IPAddress]::Loopback)
foreach ($address in $addresses) { $san.AddIpAddress($address) }
$serverRequest.CertificateExtensions.Add($san.Build())
$serverRequest.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false, $false, 0, $true))
$serverRequest.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new('DigitalSignature, KeyEncipherment', $true))
$eku = [System.Security.Cryptography.OidCollection]::new(); [void]$eku.Add([System.Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.1'))
$serverRequest.CertificateExtensions.Add([System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($eku, $true))
# iOS rejects TLS server certificates valid for more than 825 days; 365 is well inside.
$leaf = $serverRequest.Create($root, $now.AddMinutes(-1), $now.AddDays(365), [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(12))
$certificate = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::CopyWithPrivateKey($leaf, $serverKey)

$password = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(36))
[System.IO.File]::WriteAllBytes($Prefix + '.pfx', $certificate.Export('Pfx', $password))
[System.IO.File]::WriteAllBytes($Prefix + '.cer', $certificate.Export('Cert'))
[System.IO.File]::WriteAllBytes($Prefix + '.root.cer', $root.Export('Cert'))
[System.IO.File]::WriteAllText($Prefix + '.password', $password)
[ordered]@{
    subject = $certificate.Subject; thumbprint = $certificate.Thumbprint
    notBeforeUtc = $certificate.NotBefore.ToUniversalTime(); notAfterUtc = $certificate.NotAfter.ToUniversalTime()
    subjectAlternativeNames = @('localhost', '127.0.0.1') + @($addresses | ForEach-Object { $_.ToString() })
    certificateAuthority = $false; issuer = $root.Subject; rootThumbprint = $root.Thumbprint
    rootPath = $Prefix + '.root.cer'; enhancedKeyUsage = 'TLS server authentication'
    pfxPath = $Prefix + '.pfx'; certificatePath = $Prefix + '.cer'; passwordPath = $Prefix + '.password'; trustInstalled = $false
} | ConvertTo-Json | Set-Content -LiteralPath ($Prefix + '.metadata.json') -Encoding utf8NoBOM
Write-Output "Created $Prefix.metadata.json (root $($root.Thumbprint)); no trust changes performed."
