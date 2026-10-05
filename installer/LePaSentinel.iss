; Inno Setup script for LePa Sentinel.
; Built by build.ps1, which passes /DAppVersion=... and /DPublishDir=...

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish"
#endif

#define AppName "LePa Sentinel"
#define AppExe "LePaSentinel.exe"

[Setup]
AppId={{7A58B748-E85C-4E4E-8A71-625310A1AE02}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=LePa s.r.l.
AppPublisherURL=https://github.com/emanueleparini/LePa-Sentinel
AppSupportURL=https://github.com/emanueleparini/LePa-Sentinel/issues
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; The hooks live in the user's own Claude Code settings: install per user, no admin rights.
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.17763
OutputDir=..\artifacts
OutputBaseFilename=LePa-Sentinel-Setup-{#AppVersion}-x64
SetupIconFile=..\src\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
LicenseFile=..\LICENSE
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
CloseApplications=no
#ifdef Sign
; build.ps1 -Sign defines the "artifactsigning" tool via ISCC /S
SignTool=artifactsigning
SignedUninstaller=yes
#endif

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "it"; MessagesFile: "compiler:Languages\Italian.isl"

[CustomMessages]
en.ConnectTask=Connect to Claude Code (adds hooks to %USERPROFILE%\.claude\settings.json, with a backup)
it.ConnectTask=Collega a Claude Code (aggiunge gli hooks a %USERPROFILE%\.claude\settings.json, con backup)
en.AutostartTask=Start with Windows
it.AutostartTask=Avvia con Windows
en.Options=Options:
it.Options=Opzioni:

[Tasks]
Name: "connect"; Description: "{cm:ConnectTask}"; GroupDescription: "{cm:Options}"
Name: "autostart"; Description: "{cm:AutostartTask}"; GroupDescription: "{cm:Options}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExe}"; Parameters: "--install-hooks"; Flags: runhidden waituntilterminated; Tasks: connect
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\{#AppExe}"; Parameters: "--uninstall-hooks"; Flags: runhidden waituntilterminated; RunOnceId: "UninstallHooks"

[UninstallDelete]
; Session status files and app settings
Type: filesandordirs; Name: "{%USERPROFILE}\.claude\lepa-sentinel"
Type: filesandordirs; Name: "{userappdata}\{#AppName}"

[Code]
// A tray app has no window to close: stop it before files are replaced or removed.
procedure StopRunningApp();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningApp();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopRunningApp();
  Result := True;
end;
