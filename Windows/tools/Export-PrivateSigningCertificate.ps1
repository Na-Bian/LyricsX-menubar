[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9A-Fa-f]{40}$')]
    [string]$Thumbprint,
    [Parameter(Mandatory)]
    [ValidateScript({ [IO.Path]::GetExtension($_) -eq '.pfx' })]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$certificate = Get-Item "Cert:\CurrentUser\My\$Thumbprint"
$password = Read-Host 'Enter a new strong PFX backup password' -AsSecureString
Export-PfxCertificate -Cert $certificate -FilePath $OutputPath `
    -Password $password -CryptoAlgorithmOption AES256_SHA256 -Force |
    Out-Null
Write-Warning 'Keep this PFX offline. Never commit or share it.'
Write-Output (Resolve-Path $OutputPath).Path
