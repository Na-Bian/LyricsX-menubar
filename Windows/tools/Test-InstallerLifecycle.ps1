[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Installer,
    [switch]$UninstallAtEnd,
    [switch]$DeleteUserData
)

$ErrorActionPreference = 'Stop'
$installerPath = (Resolve-Path $Installer).Path
$installDirectory = Join-Path $env:LOCALAPPDATA 'Programs\LyricsX'
$dataDirectory = Join-Path $env:LOCALAPPDATA 'LyricsX'
$log = Join-Path $env:TEMP 'LyricsX-install.log'

$process = Start-Process $installerPath `
    -ArgumentList '/VERYSILENT', '/NORESTART', "/LOG=$log" `
    -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Install failed: $($process.ExitCode)" }

$exe = Join-Path $installDirectory 'LyricsX.Windows.App.exe'
if (-not (Test-Path $exe)) { throw "Installed executable missing: $exe" }

$marker = Join-Path $dataDirectory 'lifecycle-marker.txt'
New-Item -ItemType Directory -Force $dataDirectory | Out-Null
'preserve-me' | Set-Content $marker -Encoding utf8

if ($UninstallAtEnd) {
    $uninstaller = Get-ChildItem $installDirectory -Filter 'unins*.exe' |
        Select-Object -First 1
    if (-not $uninstaller) { throw 'Uninstaller not found.' }
    $uninstallArguments = @('/VERYSILENT', '/NORESTART')
    if ($DeleteUserData) {
        $uninstallArguments += '/DELETEUSERDATA=1'
    }
    $uninstall = Start-Process $uninstaller.FullName `
        -ArgumentList $uninstallArguments -Wait -PassThru
    if ($uninstall.ExitCode -ne 0) {
        throw "Uninstall failed: $($uninstall.ExitCode)"
    }
    if (Test-Path $exe) { throw 'Application executable remained after uninstall.' }
    if ($DeleteUserData -and (Test-Path $marker)) {
        throw 'User data remained after explicit deletion was requested.'
    }
    if (-not $DeleteUserData -and -not (Test-Path $marker)) {
        throw 'User data was removed by default uninstall.'
    }
}
