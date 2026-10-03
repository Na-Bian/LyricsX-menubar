[CmdletBinding()]
param(
    [string]$Subject = 'CN=LyricsX Private Distribution',
    [int]$Years = 3,
    [switch]$TrustLocally
)

$ErrorActionPreference = 'Stop'
$notAfter = (Get-Date).AddYears($Years)
$certificate = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -HashAlgorithm SHA256 `
    -KeyAlgorithm RSA `
    -KeyLength 3072 `
    -KeyExportPolicy Exportable `
    -NotAfter $notAfter

if ($TrustLocally) {
    $publicBytes = $certificate.Export(
        [System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
    $publicCertificate =
        [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
            $publicBytes)

    foreach ($storeName in @('Root', 'TrustedPublisher')) {
        $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
            $storeName,
            [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
        try {
            $store.Open(
                [System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
            $store.Add($publicCertificate)
        }
        finally {
            $store.Close()
        }
    }
}

Write-Output $certificate.Thumbprint
