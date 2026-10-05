[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'LyricsX.Windows.slnx'
$project = Join-Path $root 'src\LyricsX.Windows.App\LyricsX.Windows.App.csproj'
$rids = @('win-x64', 'win-arm64')

foreach ($rid in $rids) {
    $publish = Join-Path $root ("out\publish\" + $rid)
    if (Test-Path $publish) {
        Remove-Item $publish -Recurse -Force
    }
}

dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

dotnet test $solution -c Release --no-restore --filter 'TestCategory!=LiveProvider&TestCategory!=InteractiveSession&TestCategory!=WindowsIntegration'
if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }

foreach ($rid in $rids) {
    dotnet publish $project -c Release -r $rid --self-contained true `
        -p:Version=$Version -p:ContinuousIntegrationBuild=true `
        -p:PublishProfile=$rid
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $rid." }

    # third-party packages ship their own .pdb files; the release contract
    # forbids them in the output folder
    Get-ChildItem (Join-Path $root ("out\publish\" + $rid)) -Recurse -File |
        Where-Object { $_.Extension -in '.pdb' } |
        Remove-Item -Force

    $exe = Join-Path $root ("out\publish\" + $rid + '\LyricsX.Windows.App.exe')
    if (-not (Test-Path $exe)) { throw "Published executable not found: $exe" }
    Write-Output $exe
}
