$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet restore LyricsX.Windows.slnx --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

dotnet test LyricsX.Windows.slnx -c Release --no-restore `
    --filter 'TestCategory!=LiveProvider&TestCategory!=InteractiveSession&TestCategory!=WindowsIntegration'
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

pwsh -File tools/Publish-Release.ps1 -Version 0.1.0-ci
pwsh -File tests/Release.Tests.ps1
pwsh -File tests/Installer.Tests.ps1
pwsh -File tests/Signing.Tests.ps1
