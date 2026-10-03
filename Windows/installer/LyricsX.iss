#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

#ifndef ArchTag
  #define ArchTag "x64"
#endif

#if ArchTag == "x64"
  #define SourceDir "..\out\publish\win-x64"
  #define ArchGuard "x64compatible"
#else
  #define SourceDir "..\out\publish\win-arm64"
  #define ArchGuard "arm64"
#endif

#define AppName "LyricsX"
#define AppPublisher "LyricsX contributors"
#define AppExeName "LyricsX.Windows.App.exe"

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
ArchitecturesAllowed={#ArchGuard}
ArchitecturesInstallIn64BitMode={#ArchGuard}
OutputDir=..\out\installer
OutputBaseFilename=LyricsX-Windows-Setup-{#ArchTag}
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
    MsgBox('LyricsX 仅支持 Windows 11。', mbError, MB_OK);
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
