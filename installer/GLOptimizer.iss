; Machine-wide GL Optimizer setup. Compile on Windows with Inno Setup 6.
; Publish a self-contained win-x64 folder to ..\publish first.
; Pass the build version with: ISCC /DMyAppVersion=0.1.0 installer\GLOptimizer.iss

#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

#define AppName "GL Optimizer"
#define AppExe "GLOptimizer.exe"

[Setup]
AppId={{8F4E2A1C-6B3D-4E90-9C1A-7D5E2F8A4B16}
AppName={#AppName}
AppVersion={#MyAppVersion}
AppVerName={#AppName} {#MyAppVersion}
AppPublisher=GL Optimizer
VersionInfoVersion={#MyAppVersion}.0
VersionInfoProductVersion={#MyAppVersion}.0
DefaultDirName={autopf}\GL Optimizer
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=GL-Optimizer-Setup
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter={#AppExe}
RestartApplications=no
AppMutex=GLOptimizer
SetupMutex=GLOptimizerSetup

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataPath: String;
begin
  if CurUninstallStep <> usPostUninstall then
    Exit;

  if RegValueExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'GL Optimizer') then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'GL Optimizer');

  DataPath := ExpandConstant('{localappdata}\GLOptimizer');
  if DirExists(DataPath) then
  begin
    if MsgBox('Remove GL Optimizer backups, logs, and settings from ' + DataPath + '?' + #13#10 + #13#10 + 'Choose No to keep them.', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      DelTree(DataPath, True, True, True);
  end;
end;
