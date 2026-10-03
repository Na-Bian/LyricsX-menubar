$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$scripts = @(
    'tools\Create-PrivateSigningCertificate.ps1',
    'tools\Sign-Release.ps1',
    'tools\Export-PublicCertificate.ps1',
    'tools\Export-PrivateSigningCertificate.ps1'
)

foreach ($name in $scripts) {
    $text = Get-Content (Join-Path $root $name) -Raw

    if ($text -match '[0-9A-Fa-f]{40}') {
        throw "Hard-coded thumbprint found in ${name}"
    }
    if ($text -match '(?i)(password\s*=\s*["'']|\.pfx\s*["'']\s*[,)])' -and
        $name -ne 'tools\Export-PrivateSigningCertificate.ps1') {
        throw "Possible embedded secret in ${name}"
    }
    if ($text -match 'C:\\Users\\') {
        throw "Machine-specific user path in ${name}"
    }
    if ($text -match 'timestamp\.(digicert|sectigo|globalsign)') {
        throw "Public timestamp server in ${name}"
    }
}

$pfx = Get-ChildItem (Join-Path $root '.') -Recurse -Include *.pfx, *.pvk, *.key -ErrorAction SilentlyContinue
if ($pfx) {
    throw "Private key material in repository: $($pfx.FullName -join ', ')"
}
