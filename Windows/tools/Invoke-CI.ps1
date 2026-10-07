$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet restore LyricsX.Windows.slnx --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

# .NET 10 removed the vstest entry point for MTP test projects (MSTest runner),
# so tests run through each project's generated platform executable.
dotnet build LyricsX.Windows.slnx -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$filter = 'TestCategory!=LiveProvider&TestCategory!=InteractiveSession&TestCategory!=WindowsIntegration'
$testProjects = @(
    'LyricsX.Core.Tests',
    'LyricsX.Providers.Tests',
    'LyricsX.Platform.Windows.Tests',
    'LyricsX.Windows.App.Tests'
)
foreach ($project in $testProjects) {
    $exe = "tests/$project/bin/Release/net10.0-windows10.0.22000.0/$project.exe"
    if (-not (Test-Path $exe)) { throw "Test executable not found: $exe" }
    & $exe --filter $filter --no-ansi
    if ($LASTEXITCODE -ne 0) { throw "Tests failed for $project." }
}

pwsh -File tools/Publish-Release.ps1 -Version 0.1.0-ci
pwsh -File tests/Release.Tests.ps1
pwsh -File tests/Installer.Tests.ps1
pwsh -File tests/Signing.Tests.ps1
