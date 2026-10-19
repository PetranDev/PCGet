; PCGet - Inno Setup script
; Build with installer\build-installer.ps1, which publishes the app and passes AppVersion/Arch/SourceDir.
; Requires Inno Setup 6.3 or later.

#define AppName "PCGet"
#define AppPublisher "PCG"
#define AppExeName "PCGet.exe"

#ifndef AppVersion
  #define AppVersion "1.0.0.1"
#endif

#ifndef Arch
  #define Arch "x64"
#endif

#ifndef SourceDir
  #define SourceDir "build\" + Arch
#endif

[Setup]
; Never change AppId: it is how upgrades and winget (ProductCode "{5E6FA693-...}_is1") find the install.
AppId={{5E6FA693-90D9-4623-8321-BA0AC0A46ACA}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=admin
UsedUserAreasWarning=no
MinVersion=10.0.17763
#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
OutputDir=Output
OutputBaseFilename={#AppName}-Setup-{#AppVersion}-{#Arch}
SetupIconFile=..\PCGet\Assets\PCGet.ico
UninstallDisplayIcon={app}\PCGet\{#AppExeName}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=auto
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"
; Greek and Chinese (Simplified) are unofficial Inno Setup translations. To add them, download
; Greek.isl / ChineseSimplified.isl from https://jrsoftware.org/files/istrans/ into installer\Languages
; and uncomment:
; Name: "greek"; MessagesFile: "Languages\Greek.isl"
; Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"

[CustomMessages]
StartWithWindows=Start PCGet with Windows (checks for updates in the background)
WinGetMissing=PCGet uses the Windows Package Manager (winget), which was not found on this PC.%n%nInstall "App Installer" from the Microsoft Store, otherwise PCGet will not be able to list or install software.%n%nContinue anyway?

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "{cm:StartWithWindows}"; Flags: unchecked

[InstallDelete]
; Clear the previous version's files so upgrades never leave stale DLLs behind.
Type: filesandordirs; Name: "{app}\PCGet"
Type: filesandordirs; Name: "{app}\PCGet.Elevated"
Type: filesandordirs; Name: "{app}\PCGet.Updater"

[Files]
; Same layout as the MSIX package: PCGet looks for ..\PCGet.Elevated and ..\PCGet.Updater next to its own folder.
Source: "{#SourceDir}\PCGet\*"; DestDir: "{app}\PCGet"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceDir}\PCGet.Elevated\*"; DestDir: "{app}\PCGet.Elevated"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceDir}\PCGet.Updater\*"; DestDir: "{app}\PCGet.Updater"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\PCGet\{#AppExeName}"; WorkingDir: "{app}\PCGet"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\PCGet\{#AppExeName}"; WorkingDir: "{app}\PCGet"; Tasks: desktopicon

[Registry]
; Same value PCGet's own "Start with Windows" setting reads and writes (StartupService).
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "PCGet"; ValueData: """{app}\PCGet\{#AppExeName}"" --startup"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\PCGet\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
; Removes the Run key and the toast-notification registration PCGet creates for itself.
Filename: "{app}\PCGet\{#AppExeName}"; Parameters: "--uninstall-cleanup"; Flags: runhidden waituntilterminated; RunOnceId: "PCGetCleanup"

[Code]
procedure ClosePCGet();
var
  ResultCode: Integer;
begin
  // PCGet keeps running in the tray, so Restart Manager can't always close it gracefully.
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /T /IM PCGet.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /T /IM PCGet.Elevated.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function InitializeSetup(): Boolean;
begin
  Result := True;

  if WizardSilent() then
    Exit;

  if not FileExists(ExpandConstant('{localappdata}\Microsoft\WindowsApps\winget.exe')) then
    Result := SuppressibleMsgBox(CustomMessage('WinGetMissing'), mbConfirmation, MB_YESNO, IDYES) = IDYES;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  ClosePCGet();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  ClosePCGet();
  Result := True;
end;
