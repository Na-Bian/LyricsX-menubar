param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '0.1.0'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# --- publish folder checks (both RIDs) ---
$rids = @('win-x64', 'win-arm64')
$required = @(
    'LyricsX.Windows.App.exe',
    'Microsoft.WindowsAppRuntime.Bootstrap.dll',
    'Microsoft.WindowsAppRuntime.dll',
    'LyricsX.Windows.App.pri'
)

foreach ($rid in $rids) {
    $publish = Join-Path $root ("out\publish\" + $rid)
    foreach ($name in $required) {
        $path = Join-Path $publish $name
        if (-not (Test-Path $path)) { throw "Missing release file for ${rid}: $name" }
    }

    $unexpected = Get-ChildItem $publish -Recurse -File |
        Where-Object { $_.Extension -in '.pdb', '.pfx', '.snk' }
    if ($unexpected) {
        throw "Forbidden release files: $($unexpected.FullName -join ', ')"
    }
}

# --- artifact checks (per arch) ---
$artifacts = Join-Path $root ("artifacts\" + $Version)
Add-Type -AssemblyName System.IO.Compression.FileSystem

foreach ($arch in $rids) {
    $rid = $arch -replace '^win-', ''
    $dir = Join-Path $artifacts $rid
    $expected = @(
        "LyricsX-Windows-Setup-$rid.exe",
        "LyricsX-Windows-Portable-$rid.zip",
        'LICENSE.txt',
        'THIRD-PARTY-NOTICES.txt',
        'RELEASE-NOTES.md',
        'SHA256SUMS.txt'
    )
    foreach ($name in $expected) {
        if (-not (Test-Path (Join-Path $dir $name))) {
            throw "Missing artifact for ${rid}: $name"
        }
    }

    # zip opens and contains the app exe
    $zipPath = Join-Path $dir "LyricsX-Windows-Portable-$rid.zip"
    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        if (-not ($zip.Entries | Where-Object FullName -eq 'LyricsX.Windows.App.exe')) {
            throw "Portable zip missing app exe for $rid"
        }
    }
    finally {
        $zip.Dispose()
    }

    # every SHA256SUMS line matches the actual file
    foreach ($line in Get-Content (Join-Path $dir 'SHA256SUMS.txt')) {
        $parts = $line -split ' \*', 2
        $file = Join-Path $dir $parts[1]
        $actual = (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $parts[0]) {
            throw "SHA256 mismatch for $parts[1] ($rid)"
        }
    }

    # installer version metadata equals requested version
    $installer = Get-Item (Join-Path $dir "LyricsX-Windows-Setup-$rid.exe")
    $installerVersion = $installer.VersionInfo.ProductVersion
    if ($installerVersion -ne $Version) {
        throw "Installer version '$installerVersion' != $Version ($rid)"
    }

    # signature state: NotSigned (unsigned) or Valid, never bad-hash/unknown
    $signature = Get-AuthenticodeSignature $installer.FullName
    if ($signature.Status -in 'HashMismatch', 'UnknownError') {
        throw "Bad signature state for ${rid}: $($signature.Status)"
    }

    # no private key material anywhere under the artifact dir
    $keys = Get-ChildItem $artifacts -Recurse -Include '*.pfx', '*.pvk', '*.key' -ErrorAction SilentlyContinue
    if ($keys) {
        throw "Private key material in artifacts: $($keys.FullName -join ', ')"
    }
}
