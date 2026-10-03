[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9A-Fa-f]{40}$')]
    [string]$Thumbprint,
    [Parameter(Mandatory)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$certificate = Get-Item "Cert:\CurrentUser\My\$Thumbprint"
Export-Certificate -Cert $certificate -FilePath $OutputPath -Type CERT -Force |
    Out-Null
Write-Output (Resolve-Path $OutputPath).Path
