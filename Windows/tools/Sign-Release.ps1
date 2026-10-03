[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9A-Fa-f]{40}$')]
    [string]$Thumbprint,
    [Parameter(Mandatory)]
    [string[]]$Paths
)

$ErrorActionPreference = 'Stop'
$kits = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" `
    -Directory -ErrorAction Stop |
    Sort-Object Name -Descending
$signtool = $kits |
    ForEach-Object { Join-Path $_.FullName 'x64\signtool.exe' } |
    Where-Object { Test-Path $_ } |
    Select-Object -First 1
if (-not $signtool) { throw 'Windows SDK SignTool was not found.' }

foreach ($path in $Paths) {
    $resolved = (Resolve-Path $path).Path
    & $signtool sign /sha1 $Thumbprint /fd SHA256 /a $resolved
    if ($LASTEXITCODE -ne 0) { throw "Signing failed: $resolved" }

    $signature = Get-AuthenticodeSignature $resolved
    if (-not $signature.SignerCertificate -or
        $signature.SignerCertificate.Thumbprint -ne $Thumbprint -or
        $signature.Status -in 'NotSigned', 'HashMismatch') {
        throw "Signature verification failed: $resolved ($($signature.Status))"
    }
    if ($signature.Status -ne 'Valid') {
        Write-Warning "The file is signed but this PC does not trust the certificate: $resolved"
    }
}
