# Windows Taskbar Lyrics 04: Installer, Signing, and Verification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a reproducible x64 release, normal per-user EXE installer, optional private code-signing workflow, portable diagnostic ZIP, checksums, licenses, and a completed Windows 11 acceptance matrix.

**Architecture:** `dotnet publish` creates one deterministic self-contained folder. Inno Setup wraps that folder without elevation. Signing is optional and happens after publish and again after installer creation. Verification scripts inspect contents, hashes, signatures, startup, upgrade, uninstall, and logs. Manual acceptance covers the five target player categories and Windows shell edge cases that cannot be faithfully simulated on macOS.

**Tech Stack:** .NET 10 SDK, Windows App SDK 1.8.6, Inno Setup 6, PowerShell 7/Windows PowerShell 5.1-compatible scripts, Windows SDK SignTool, GitHub Actions Windows runner, SHA-256.

## Global Constraints

- Complete plans 01–03 first.
- Produce x64 Windows 11 artifacts only.
- Default distribution is unsigned and per-user; installation must not require administrator rights.
- A private self-signed certificate is optional. Never commit or share a `.pfx`, password, private key, certificate-store export, or signing secret.
- Do not claim publisher trust for an unsigned or self-signed build.
- Installer/uninstaller must preserve user settings, cache, associations, logs, and credentials unless the user explicitly selects a data-removal action.
- Release only after the five-player and shell matrix passes on the user's Windows 11 x64 PC/VM.
- Commit only files named by the current task.

## Release Artifact Contract

```text
Windows/artifacts/<version>/
├── LyricsX-Windows-Setup-x64.exe
├── LyricsX-Windows-Portable-x64.zip
├── SHA256SUMS.txt
├── LICENSE.txt
├── THIRD-PARTY-NOTICES.txt
└── RELEASE-NOTES.md
```

The installer name is stable across signing modes. `SHA256SUMS.txt` is generated after final signing because signing changes file hashes.

---

## Task 1: Add deterministic publish profiles and version stamping

**Files:**

- Create: `Windows/Directory.Build.targets`
- Create: `Windows/src/LyricsX.Windows.App/Properties/PublishProfiles/win-x64.pubxml`
- Create: `Windows/tools/Publish-Release.ps1`
- Create: `Windows/tests/Release.Tests.ps1`

**Interfaces produced:** `Windows/out/publish/win-x64/`.

- [ ] Create `Directory.Build.targets`:

```xml
<Project>
  <PropertyGroup>
    <VersionPrefix Condition="'$(VersionPrefix)' == ''">0.1.0</VersionPrefix>
    <VersionSuffix Condition="'$(VersionSuffix)' == ''"></VersionSuffix>
    <Version Condition="'$(Version)' == '' and '$(VersionSuffix)' == ''">$(VersionPrefix)</Version>
    <Version Condition="'$(Version)' == '' and '$(VersionSuffix)' != ''">$(VersionPrefix)-$(VersionSuffix)</Version>
    <AssemblyVersion>$(VersionPrefix).0</AssemblyVersion>
    <FileVersion>$(VersionPrefix).0</FileVersion>
    <InformationalVersion>$(Version)</InformationalVersion>
    <RepositoryUrl>https://github.com/Na-Bian/LyricsX-menubar</RepositoryUrl>
  </PropertyGroup>
</Project>
```

- [ ] Create `win-x64.pubxml`:

```xml
<Project>
  <PropertyGroup>
    <Configuration>Release</Configuration>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>false</PublishSingleFile>
    <PublishReadyToRun>true</PublishReadyToRun>
    <PublishTrimmed>false</PublishTrimmed>
    <DebugType>embedded</DebugType>
    <DebugSymbols>false</DebugSymbols>
    <PublishDir>$(MSBuildThisFileDirectory)..\..\..\..\..\out\publish\win-x64\</PublishDir>
  </PropertyGroup>
</Project>
```

- [ ] Create `Publish-Release.ps1`:

```powershell
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
$publish = Join-Path $root 'out\publish\win-x64'

if (Test-Path $publish) {
    Remove-Item $publish -Recurse -Force
}

dotnet restore $solution --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

dotnet test $solution -c Release --no-restore --filter 'TestCategory!=LiveProvider'
if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }

dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:Version=$Version -p:ContinuousIntegrationBuild=true `
    -p:PublishProfile=win-x64
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

$exe = Join-Path $publish 'LyricsX.Windows.App.exe'
if (-not (Test-Path $exe)) { throw "Published executable not found: $exe" }
Write-Output $publish
```

- [ ] Create `Release.Tests.ps1`:

```powershell
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root 'out\publish\win-x64'
$required = @(
    'LyricsX.Windows.App.exe',
    'Microsoft.WindowsAppRuntime.Bootstrap.dll',
    'Microsoft.WindowsAppRuntime.dll'
)

foreach ($name in $required) {
    $path = Join-Path $publish $name
    if (-not (Test-Path $path)) { throw "Missing release file: $name" }
}

$unexpected = Get-ChildItem $publish -Recurse -File |
    Where-Object { $_.Extension -in '.pdb', '.pfx', '.snk' }
if ($unexpected) {
    throw "Forbidden release files: $($unexpected.FullName -join ', ')"
}
```

- [ ] Generate and commit `packages.lock.json` for every project:

```powershell
dotnet restore LyricsX.Windows.slnx -p:RestorePackagesWithLockFile=true
```

Then set `<RestoreLockedMode Condition="'$(CI)' == 'true'">true</RestoreLockedMode>` in `Directory.Build.props`.

- [ ] Run:

```powershell
pwsh -File tools/Publish-Release.ps1 -Version 0.1.0
pwsh -File tests/Release.Tests.ps1
```

Expected: tests pass, publish succeeds, and no PDB/private-key files exist in output.

- [ ] Commit:

```powershell
git add Windows/Directory.Build.props Windows/Directory.Build.targets Windows/src/LyricsX.Windows.App/Properties/PublishProfiles Windows/tools/Publish-Release.ps1 Windows/tests/Release.Tests.ps1 Windows/**/packages.lock.json
git commit -m "build(windows): add reproducible release publish"
```

## Task 2: Build the per-user Inno Setup installer

**Files:**

- Create: `Windows/installer/LyricsX.iss`
- Create: `Windows/installer/Assets/wizard-small.bmp`
- Create: `Windows/installer/Assets/wizard-large.bmp`
- Create: `Windows/tools/Build-Installer.ps1`
- Create: `Windows/tests/Installer.Tests.ps1`

**Interfaces produced:** `Windows/out/installer/LyricsX-Windows-Setup-x64.exe`.

- [ ] Create `LyricsX.iss`:

```pascal
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

#define AppName "LyricsX"
#define AppPublisher "LyricsX contributors"
#define AppExeName "LyricsX.Windows.App.exe"
#define SourceDir "..\out\publish\win-x64"

[Setup]
AppId={{85B19E84-0D47-4B36-BDAF-D9AE1B1E7E24}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\LyricsX
DefaultGroupName=LyricsX
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\out\installer
OutputBaseFilename=LyricsX-Windows-Setup-x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
WizardSmallImageFile=Assets\wizard-small.bmp
WizardImageFile=Assets\wizard-large.bmp
SetupIconFile=..\src\LyricsX.Windows.App\Assets\LyricsX.ico
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes
RestartApplications=no
ChangesAssociations=no
ChangesEnvironment=no
MinVersion=10.0.22000
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=LyricsX Windows Installer
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
chinesesimp.DeleteUserDataPrompt=是否同时删除 LyricsX 的偏好设置、歌词缓存、手动关联和日志？选择“否”将保留这些数据。
english.DeleteUserDataPrompt=Also delete LyricsX preferences, lyric cache, manual associations, and logs? Choose No to keep this data.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "登录时启动 LyricsX"; GroupDescription: "启动选项"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\LyricsX"; Filename: "{app}\{#AppExeName}"
Name: "{group}\卸载 LyricsX"; Filename: "{uninstallexe}"
Name: "{autodesktop}\LyricsX"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "LyricsX"; ValueData: """{app}\{#AppExeName}"" --startup"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,LyricsX}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\{#AppExeName}"; Parameters: "--shutdown"; Flags: runhidden waituntilterminated skipifdoesntexist; RunOnceId: "StopLyricsX"

[Code]
var
  DeleteUserData: Boolean;

function InitializeSetup(): Boolean;
begin
  Result := IsWin64;
  if not Result then
    MsgBox('LyricsX 仅支持 Windows 11 x64。', mbError, MB_OK);
end;

function InitializeUninstall(): Boolean;
begin
  if UninstallSilent then
    DeleteUserData :=
      CompareText(ExpandConstant('{param:DELETEUSERDATA|0}'), '1') = 0
  else
    DeleteUserData :=
      MsgBox(ExpandConstant('{cm:DeleteUserDataPrompt}'),
        mbConfirmation, MB_YESNO) = IDYES;
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and DeleteUserData then
    DelTree(ExpandConstant('{localappdata}\LyricsX'), True, True, True);
end;
```

- [ ] Create installer artwork from the existing project icon without changing the icon design: 55×55 BMP for `wizard-small.bmp` and 164×314 BMP for `wizard-large.bmp`. Inspect both images for stretching and transparency artifacts.

- [ ] Create `Build-Installer.ps1`:

```powershell
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
$script = Join-Path $root 'installer\LyricsX.iss'
$output = Join-Path $root 'out\installer\LyricsX-Windows-Setup-x64.exe'

if (-not (Test-Path $compiler)) {
    throw "Inno Setup 6 compiler not found: $compiler"
}

& $compiler "/DAppVersion=$Version" $script
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
if (-not (Test-Path $output)) { throw "Installer not found: $output" }
Write-Output $output
```

- [ ] Create installer tests that use `Get-Content installer/LyricsX.iss` and assert `PrivilegesRequired=lowest`, `{localappdata}\Programs\LyricsX`, stable AppId, x64 architecture, minimum Windows build 22000, no `runas`, no `[UninstallDelete]`, that silent uninstall preserves data by default, and that `%LOCALAPPDATA%\LyricsX` deletion requires either an interactive Yes response or `/DELETEUSERDATA=1`.

- [ ] Run:

```powershell
pwsh -File tools/Build-Installer.ps1 -Version 0.1.0
pwsh -File tests/Installer.Tests.ps1
```

Expected: installer compiles and policy assertions pass.

- [ ] Commit:

```powershell
git add Windows/installer Windows/tools/Build-Installer.ps1 Windows/tests/Installer.Tests.ps1
git commit -m "build(windows): add per-user EXE installer"
```

## Task 3: Add optional private self-signing scripts

**Files:**

- Create: `Windows/tools/Create-PrivateSigningCertificate.ps1`
- Create: `Windows/tools/Sign-Release.ps1`
- Create: `Windows/tools/Export-PublicCertificate.ps1`
- Create: `Windows/tools/Export-PrivateSigningCertificate.ps1`
- Create: `Windows/docs/private-signing.md`
- Create: `Windows/tests/Signing.Tests.ps1`
- Modify: `.gitignore`

**Interfaces produced:** optional certificate in the current user's certificate store and signed release files.

- [ ] Create `Create-PrivateSigningCertificate.ps1`:

```powershell
[CmdletBinding()]
param(
    [string]$Subject = 'CN=LyricsX Private Distribution',
    [int]$Years = 3,
    [switch]$TrustLocally
)

$ErrorActionPreference = 'Stop'
$notAfter = (Get-Date).AddYears($Years)
$certificate = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -HashAlgorithm SHA256 `
    -KeyAlgorithm RSA `
    -KeyLength 3072 `
    -KeyExportPolicy Exportable `
    -NotAfter $notAfter

if ($TrustLocally) {
    $publicBytes = $certificate.Export(
        [System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
    $publicCertificate =
        [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
            $publicBytes)

    foreach ($storeName in @('Root', 'TrustedPublisher')) {
        $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
            $storeName,
            [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
        try {
            $store.Open(
                [System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
            $store.Add($publicCertificate)
        }
        finally {
            $store.Close()
        }
    }
}

Write-Output $certificate.Thumbprint
```

- [ ] Create `Sign-Release.ps1`:

```powershell
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
```

Do not add a public timestamp server: the private certificate is for a small known group, and timestamping would add an external dependency without creating public trust.

- [ ] Create `Export-PublicCertificate.ps1`:

```powershell
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9A-Fa-f]{40}$')]
    [string]$Thumbprint,
    [Parameter(Mandatory)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$certificate = Get-Item "Cert:\CurrentUser\My\$Thumbprint"
Export-Certificate -Cert $certificate -FilePath $OutputPath -Type CERT -Force |
    Out-Null
Write-Output (Resolve-Path $OutputPath).Path
```

- [ ] Create `Export-PrivateSigningCertificate.ps1` for an optional offline backup:

```powershell
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9A-Fa-f]{40}$')]
    [string]$Thumbprint,
    [Parameter(Mandatory)]
    [ValidateScript({ [IO.Path]::GetExtension($_) -eq '.pfx' })]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$certificate = Get-Item "Cert:\CurrentUser\My\$Thumbprint"
$password = Read-Host 'Enter a new strong PFX backup password' -AsSecureString
Export-PfxCertificate -Cert $certificate -FilePath $OutputPath `
    -Password $password -CryptoAlgorithmOption AES256_SHA256 -Force |
    Out-Null
Write-Warning 'Keep this PFX offline. Never commit or share it.'
Write-Output (Resolve-Path $OutputPath).Path
```

- [ ] Add these root `.gitignore` rules:

```gitignore
# Windows private signing material
Windows/**/*.pfx
Windows/**/*.pvk
Windows/**/*.key
```

- [ ] Write `private-signing.md` with these exact safety statements:

```text
- The default installer is unsigned and Windows may show a SmartScreen warning.
- A self-signed certificate does not create public publisher trust.
- Friends must verify the SHA-256 fingerprint with you through a separate channel before importing the public .cer.
- Import only the public .cer into Current User > Trusted Publishers and Current User > Trusted Root Certification Authorities.
- A password-protected .pfx is an optional private offline backup for the owner only.
- Never commit or share a .pfx, private key, password, or certificate-store backup.
- If the private key is exposed, stop distributing with it and create a new certificate.
```

Include commands to create, export the public `.cer`, inspect its SHA-256 fingerprint, sign every published `.exe`/`.dll`, rebuild installer, sign installer, and verify signatures.

- [ ] Create signing tests that assert scripts contain no hard-coded thumbprint, secret, PFX path, timestamp credential, or machine-specific user path.

- [ ] Run on a disposable private certificate:

```powershell
$thumbprint = pwsh -File tools/Create-PrivateSigningCertificate.ps1 -TrustLocally
pwsh -File tools/Sign-Release.ps1 -Thumbprint $thumbprint -Paths out/publish/win-x64/LyricsX.Windows.App.exe
Get-AuthenticodeSignature out/publish/win-x64/LyricsX.Windows.App.exe | Format-List
pwsh -File tests/Signing.Tests.ps1
```

Expected: signature status is `Valid` on the signing PC; tests find no embedded secret.

- [ ] Delete the disposable certificate after the test, then commit only scripts/docs:

```powershell
git add .gitignore Windows/tools/Create-PrivateSigningCertificate.ps1 Windows/tools/Sign-Release.ps1 Windows/tools/Export-PublicCertificate.ps1 Windows/tools/Export-PrivateSigningCertificate.ps1 Windows/docs/private-signing.md Windows/tests/Signing.Tests.ps1
git commit -m "docs(windows): add optional private signing workflow"
```

## Task 4: Add CI build and test workflow

**Files:**

- Create: `.github/workflows/windows.yml`
- Create: `Windows/tools/Invoke-CI.ps1`

**Interfaces produced:** reproducible Windows validation artifact; no automatic public release.

- [ ] Create `Invoke-CI.ps1`:

```powershell
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet restore LyricsX.Windows.slnx --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

dotnet test LyricsX.Windows.slnx -c Release --no-restore `
    --filter 'TestCategory!=LiveProvider&TestCategory!=WindowsManual'
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

pwsh -File tools/Publish-Release.ps1 -Version 0.1.0-ci
pwsh -File tests/Release.Tests.ps1
pwsh -File tests/Installer.Tests.ps1
pwsh -File tests/Signing.Tests.ps1
```

- [ ] Create `.github/workflows/windows.yml`:

```yaml
name: Windows

on:
  push:
    paths:
      - "Windows/**"
      - ".github/workflows/windows.yml"
  pull_request:
    paths:
      - "Windows/**"
      - ".github/workflows/windows.yml"

permissions:
  contents: read

jobs:
  build:
    runs-on: windows-2025
    timeout-minutes: 30
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"
          cache: true
          cache-dependency-path: "Windows/**/packages.lock.json"
      - name: Validate
        shell: pwsh
        run: ./Windows/tools/Invoke-CI.ps1
      - name: Upload diagnostic publish folder
        uses: actions/upload-artifact@v4
        with:
          name: LyricsX-Windows-ci
          path: Windows/out/publish/win-x64
          if-no-files-found: error
          retention-days: 7
```

The workflow intentionally does not upload a public release, install certificates, or use signing secrets.

- [ ] Run locally on Windows:

```powershell
pwsh -File Windows/tools/Invoke-CI.ps1
```

Expected: restore, tests, publish, and static installer/signing checks pass.

- [ ] Commit:

```powershell
git add .github/workflows/windows.yml Windows/tools/Invoke-CI.ps1
git commit -m "ci(windows): validate Windows port"
```

## Task 5: Complete licenses and Windows user documentation

**Files:**

- Create: `Windows/LICENSE.txt`
- Create: `Windows/THIRD-PARTY-NOTICES.txt`
- Create: `Windows/README.md`
- Create: `Windows/docs/troubleshooting.md`
- Create: `Windows/docs/privacy.md`

**Interfaces produced:** user-facing install/use/support documentation.

- [ ] Copy the repository MPL-2.0 license verbatim to `Windows/LICENSE.txt`.

- [ ] Generate `THIRD-PARTY-NOTICES.txt` from locked package versions and the checked-in/provider-derived implementations. Include package/project name, version, homepage, license identifier, and whether code or data is redistributed. Explicitly cover Windows App SDK, CommunityToolkit.Mvvm, OpenccNetLib, bundled OpenCC dictionaries, MSTest test-only dependencies, LyricsKit-derived provider behavior, and the original LyricsX project.

- [ ] Write `Windows/README.md` with:

```text
Requirements
Install
First launch and SmartScreen
Supported players
Taskbar behavior
Left-click compact menu
Right-click and tray full menu
Preferences
Manual lyric search
Spotify optional connection
Startup
Update by installing a newer version
Uninstall
User data location
Private sharing and checksum verification
Known limitations
Build from source
```

- [ ] State these known limitations plainly:

  - players must publish metadata and timing through GSMTC;
  - browser support depends on the tab/site exposing a media session;
  - primary taskbar only;
  - overlay hides when free width is below 80 DIPs, taskbar auto-hide is active, or a foreground app is full-screen;
  - no Apple Music/iTunes file metadata writing on Windows;
  - no Windows 10, ARM64, x86, MSIX, Store, or Explorer injection;
  - unsigned/self-signed builds may trigger SmartScreen.

- [ ] Write troubleshooting steps for no media, no lyrics, wrong player, overlay hidden, Explorer restart, hotkey conflict, Spotify reconnect, logs, clean settings reset, and checksum/signature verification.

- [ ] Write privacy statements: media metadata is used locally for provider queries; cache/settings remain under `%LOCALAPPDATA%\LyricsX`; Spotify refresh token is in Credential Manager; no telemetry; logs omit song title, artist, lyrics, browser URL, and credentials.

- [ ] Review every link and command on a clean Windows 11 user account.

- [ ] Commit:

```powershell
git add Windows/LICENSE.txt Windows/THIRD-PARTY-NOTICES.txt Windows/README.md Windows/docs/troubleshooting.md Windows/docs/privacy.md
git commit -m "docs(windows): add install privacy and support guides"
```

## Task 6: Execute the five-player and Windows shell acceptance matrix

**Files:**

- Create: `Windows/docs/acceptance-matrix.md`
- Create: `Windows/docs/manual-media-observations.md`
- Create: `Windows/tools/Collect-AcceptanceDiagnostics.ps1`

**Interfaces consumed:** installed application and redacted diagnostics.

- [ ] Create `Collect-AcceptanceDiagnostics.ps1`:

```powershell
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
```

- [ ] Build `acceptance-matrix.md` with a result/evidence column for every combination below:

| Area | Cases |
|---|---|
| Players | Apple Music, 网易云音乐, QQ 音乐, Edge/Chrome browser media, Windows Media Player |
| Playback | play, pause, resume, seek, next, previous, stop, player exits |
| Multiple players | one Playing, two Playing, recent activity switch, ignored player |
| Lyrics | cache hit, first online result, better result in 5 s, no result, manual match, offset, conversion |
| Taskbar | 100/125/150/200%, centered icons, Widgets on/off, small free area, auto-hide, full-screen |
| Shell | Explorer restart, display sleep/wake, resolution change, primary display change, sign-out/in |
| UI | compact left menu, full right menu, tray, preferences, shortcuts, accessibility |
| Lifecycle | first install, second launch, startup, upgrade, repair reinstall, uninstall, reinstall |

- [ ] For each player, record exact app version, source app ID, which metadata fields GSMTC supplied, position update behavior, and any limitation in `manual-media-observations.md`.

- [ ] Use at least two synchronized songs per player: one 2–3 minutes and one over 5 minutes. Do not commit song titles, artists, account names, browser URLs, or captured lyrics; record only pass/fail and metadata-field availability.

- [ ] Fail the release for any of:

  - crash/hang;
  - focus theft;
  - overlapping a discovered taskbar control;
  - stale lyrics after track change;
  - visible credential or private metadata in logs;
  - uninstall removing user data without explicit consent;
  - installer requiring elevation;
  - one of the five player categories never visible through GSMTC.

- [ ] If a specific third-party player version does not publish GSMTC, document the exact version and confirm the app shows `无正在播放的媒体` without crashing. Treat it as a release blocker until either a supported player setting enables GSMTC or the user accepts that documented limitation.

- [ ] Commit only the redacted matrix and observations:

```powershell
git add Windows/docs/acceptance-matrix.md Windows/docs/manual-media-observations.md Windows/tools/Collect-AcceptanceDiagnostics.ps1
git commit -m "test(windows): record Windows acceptance matrix"
```

## Task 7: Verify install, upgrade, uninstall, and retained user data

**Files:**

- Create: `Windows/tools/Test-InstallerLifecycle.ps1`
- Create: `Windows/docs/installer-lifecycle-results.md`

**Interfaces consumed:** two sequential installer versions.

- [ ] Create `Test-InstallerLifecycle.ps1`:

```powershell
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
```

- [ ] Test version N install, N+1 upgrade over the same AppId, settings retention, cache retention, startup task retention, process closure, silent uninstall, application-file removal, default user-data preservation, and a second disposable run with `/DELETEUSERDATA=1` proving explicit deletion.

- [ ] On a standard non-administrator account, confirm UAC never appears.

- [ ] Record OS build, installer versions, command results, and pass/fail in `installer-lifecycle-results.md`; do not include user paths or usernames.

- [ ] Commit:

```powershell
git add Windows/tools/Test-InstallerLifecycle.ps1 Windows/docs/installer-lifecycle-results.md
git commit -m "test(windows): verify installer lifecycle"
```

## Task 8: Assemble final release artifacts and checksums

**Files:**

- Create: `Windows/tools/Assemble-Release.ps1`
- Create: `Windows/RELEASE-NOTES.md`
- Modify: `Windows/tests/Release.Tests.ps1`

**Interfaces produced:** the complete Release Artifact Contract.

- [ ] Create `Assemble-Release.ps1`:

```powershell
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,
    [string]$SigningThumbprint
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root 'out\publish\win-x64'
$installer = Join-Path $root 'out\installer\LyricsX-Windows-Setup-x64.exe'
$artifact = Join-Path $root "artifacts\$Version"

& (Join-Path $PSScriptRoot 'Publish-Release.ps1') -Version $Version

if ($SigningThumbprint) {
    $binaries = Get-ChildItem $publish -Recurse -File |
        Where-Object { $_.Extension -in '.exe', '.dll' } |
        Select-Object -ExpandProperty FullName
    & (Join-Path $PSScriptRoot 'Sign-Release.ps1') `
        -Thumbprint $SigningThumbprint -Paths $binaries
}

& (Join-Path $PSScriptRoot 'Build-Installer.ps1') -Version $Version

if ($SigningThumbprint) {
    & (Join-Path $PSScriptRoot 'Sign-Release.ps1') `
        -Thumbprint $SigningThumbprint -Paths $installer
}

if (Test-Path $artifact) { Remove-Item $artifact -Recurse -Force }
New-Item -ItemType Directory -Force $artifact | Out-Null

Copy-Item $installer $artifact
Copy-Item (Join-Path $root 'LICENSE.txt') $artifact
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.txt') $artifact
Copy-Item (Join-Path $root 'RELEASE-NOTES.md') $artifact

$zip = Join-Path $artifact 'LyricsX-Windows-Portable-x64.zip'
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip

$hashTargets = Get-ChildItem $artifact -File |
    Where-Object Name -ne 'SHA256SUMS.txt' |
    Sort-Object Name
$hashLines = foreach ($file in $hashTargets) {
    $hash = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash *$($file.Name)"
}
$hashLines | Set-Content (Join-Path $artifact 'SHA256SUMS.txt') -Encoding ascii
Write-Output $artifact
```

- [ ] Write `RELEASE-NOTES.md` with version/date, Windows 11 x64 requirement, five target player categories, first-release features, known limitations, install/upgrade notes, user-data retention, checksum verification, and unsigned/self-signed trust wording.

- [ ] Extend `Release.Tests.ps1` to assert:

  - all six artifact files exist;
  - ZIP opens with `System.IO.Compression.ZipFile.OpenRead`;
  - every `SHA256SUMS.txt` line matches the actual file;
  - no PFX/private key/password file exists anywhere under the artifact directory;
  - installer version metadata equals requested version;
  - unsigned mode is either `NotSigned` or signed mode is `Valid`, never `HashMismatch` or `UnknownError`.

- [ ] Run unsigned assembly:

```powershell
pwsh -File tools/Assemble-Release.ps1 -Version 0.1.0
pwsh -File tests/Release.Tests.ps1 -Version 0.1.0
```

- [ ] If using private signing, run a separate clean assembly:

```powershell
$thumbprint = (Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
    Where-Object Subject -eq 'CN=LyricsX Private Distribution' |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1).Thumbprint
pwsh -File tools/Assemble-Release.ps1 -Version 0.1.0 -SigningThumbprint $thumbprint
pwsh -File tests/Release.Tests.ps1 -Version 0.1.0
```

Expected: all artifact, archive, hash, version, secret-scan, and signature assertions pass.

- [ ] Commit:

```powershell
git add Windows/tools/Assemble-Release.ps1 Windows/RELEASE-NOTES.md Windows/tests/Release.Tests.ps1
git commit -m "build(windows): assemble verified release artifacts"
```

## Task 9: Final clean-machine acceptance

**Files:**

- Modify: `Windows/docs/acceptance-matrix.md`
- Modify: `Windows/docs/installer-lifecycle-results.md`
- Modify: `Windows/RELEASE-NOTES.md`

**Interfaces consumed:** final artifact directory only.

- [ ] Create a fresh standard Windows 11 x64 local user or restore a clean VM snapshot.

- [ ] Verify the SHA-256 checksum before opening the installer:

```powershell
Get-FileHash .\LyricsX-Windows-Setup-x64.exe -Algorithm SHA256
Get-Content .\SHA256SUMS.txt
```

- [ ] Install without elevation, launch, complete the compact/full menu checks, open `偏好设置…`, enable startup, sign out/in, and confirm automatic launch.

- [ ] Run the full player and shell matrix from Task 6 against the installed artifact, not a development build.

- [ ] Install the same version again, then upgrade from N to N+1 build metadata, then uninstall. Confirm settings/cache/logs remain and Credential Manager contains no token after explicit Spotify disconnect.

- [ ] Check Windows Event Viewer and `%LOCALAPPDATA%\LyricsX\logs` for unhandled exceptions, hangs, Explorer crashes, or access violations.

- [ ] Update all result documents with final pass/fail evidence and reconcile known limitations in `RELEASE-NOTES.md`.

- [ ] Run final automated verification from a fresh checkout:

```powershell
pwsh -File Windows/tools/Invoke-CI.ps1
pwsh -File Windows/tests/Release.Tests.ps1 -Version 0.1.0
git status --short
```

Expected: all tests pass, artifacts verify, acceptance matrix contains no release blocker, and only intentional documentation evidence is modified.

- [ ] Commit:

```powershell
git add Windows/docs/acceptance-matrix.md Windows/docs/installer-lifecycle-results.md Windows/RELEASE-NOTES.md
git commit -m "test(windows): complete clean-machine acceptance"
```

## Plan 04 Exit Criteria

- [ ] `LyricsX-Windows-Setup-x64.exe` installs per-user without UAC on Windows 11 x64.
- [ ] The portable ZIP is diagnostic/optional; the normal installer is the primary delivery.
- [ ] Unsigned and optional self-signed trust expectations are documented accurately.
- [ ] No PFX, private key, password, token, song metadata, lyric content, browser URL, username, or user path exists in release artifacts or committed evidence.
- [ ] Install, startup, upgrade, uninstall, and reinstall behavior pass on a standard user account.
- [ ] Apple Music, NetEase Cloud Music, QQ Music, browser media, and Windows Media Player pass the installed-build matrix or have a user-accepted, version-specific GSMTC limitation.
- [ ] Taskbar placement passes DPI, Widgets, auto-hide, full-screen, display change, and Explorer-restart cases.
- [ ] SHA-256 checksums, licenses, notices, privacy, troubleshooting, and release notes ship beside the installer.
- [ ] The user receives the complete artifact directory only after all release blockers are cleared.
