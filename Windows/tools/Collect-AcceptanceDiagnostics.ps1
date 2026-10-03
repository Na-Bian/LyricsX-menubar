[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$destination = New-Item -ItemType Directory -Force -Path $OutputDirectory
$appData = Join-Path $env:LOCALAPPDATA 'LyricsX'
$logs = Join-Path $appData 'logs'

Get-ComputerInfo |
    Select-Object WindowsProductName, WindowsVersion, OsBuildNumber, OsArchitecture |
    ConvertTo-Json |
    Set-Content (Join-Path $destination 'windows.json') -Encoding utf8

Get-Process LyricsX.Windows.App -ErrorAction SilentlyContinue |
    Select-Object ProcessName, Id, StartTime |
    ConvertTo-Json |
    Set-Content (Join-Path $destination 'process.json') -Encoding utf8

if (Test-Path $logs) {
    Get-ChildItem $logs -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 2 |
        Copy-Item -Destination $destination
}

Get-FileHash (Join-Path $env:LOCALAPPDATA 'Programs\LyricsX\LyricsX.Windows.App.exe') `
    -Algorithm SHA256 |
    Format-List |
    Out-File (Join-Path $destination 'installed-hash.txt') -Encoding utf8
