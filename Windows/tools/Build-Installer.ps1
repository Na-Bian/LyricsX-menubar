[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,
    [ValidateSet('x64', 'arm64')]
    [string]$Arch = 'x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$compilerCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
)
$compiler = $compilerCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
$script = Join-Path $root 'installer\LyricsX.iss'
$output = Join-Path $root ("out\installer\LyricsX-Windows-Setup-" + $Arch + ".exe")

if (-not (Test-Path $compiler)) {
    throw "Inno Setup 6 compiler not found: $compiler"
}

& $compiler "/DAppVersion=$Version" "/DArchTag=$Arch" $script
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
if (-not (Test-Path $output)) { throw "Installer not found: $output" }
Write-Output $output
