[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,
    [string]$SigningThumbprint,
    [ValidateSet('x64', 'arm64')]
    [string]$Arch = 'x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root ("out\publish\win-" + $Arch)
$installer = Join-Path $root ("out\installer\LyricsX-Windows-Setup-" + $Arch + ".exe")
$artifact = Join-Path $root ("artifacts\" + $Version)

& (Join-Path $PSScriptRoot 'Publish-Release.ps1') -Version $Version

if ($SigningThumbprint) {
    $binaries = Get-ChildItem $publish -Recurse -File |
        Where-Object { $_.Extension -in '.exe', '.dll' } |
        Select-Object -ExpandProperty FullName
    & (Join-Path $PSScriptRoot 'Sign-Release.ps1') `
        -Thumbprint $SigningThumbprint -Paths $binaries
}

& (Join-Path $PSScriptRoot 'Build-Installer.ps1') -Version $Version -Arch $Arch

if ($SigningThumbprint) {
    & (Join-Path $PSScriptRoot 'Sign-Release.ps1') `
        -Thumbprint $SigningThumbprint -Paths $installer
}

if (Test-Path (Join-Path $artifact $Arch)) { Remove-Item (Join-Path $artifact $Arch) -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $artifact $Arch) | Out-Null

Copy-Item $installer (Join-Path $artifact $Arch)
Copy-Item (Join-Path $root 'LICENSE.txt') (Join-Path $artifact $Arch)
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.txt') (Join-Path $artifact $Arch)
Copy-Item (Join-Path $root 'RELEASE-NOTES.md') (Join-Path $artifact $Arch)

$zip = Join-Path $artifact ($Arch + '\LyricsX-Windows-Portable-' + $Arch + '.zip')
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -Force

$hashTargets = Get-ChildItem (Join-Path $artifact $Arch) -File |
    Where-Object Name -ne 'SHA256SUMS.txt' |
    Sort-Object Name
$hashLines = foreach ($file in $hashTargets) {
    $hash = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash *$($file.Name)"
}
$hashLines | Set-Content (Join-Path $artifact $Arch '\SHA256SUMS.txt') -Encoding ascii
Write-Output (Join-Path $artifact $Arch)
