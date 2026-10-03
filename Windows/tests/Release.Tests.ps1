$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$rids = @('win-x64', 'win-arm64')
$required = @(
    'LyricsX.Windows.App.exe',
    'Microsoft.WindowsAppRuntime.Bootstrap.dll',
    'Microsoft.WindowsAppRuntime.dll'
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
