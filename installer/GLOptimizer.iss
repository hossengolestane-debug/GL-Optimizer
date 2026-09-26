; Per-user GL Optimizer setup. Compile on Windows with Inno Setup.
; The workflow publishes self-contained win-x64 into ..\publish\win-x64 first.

#define AppName "GL Optimizer"
#define AppExe "GLOptimizer.exe"

[Setup]
AppId={{8F4E2A1C-6B3D-4E90-9C1A-7D5E2F8A4B16}
AppName={#AppName}
AppVersion=0.1.0
AppPublisher=GL Optimizer
DefaultDirName={localappdata}\GL Optimizer
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=GL-Optimizer-Setup
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\{#AppExe}
WizardStyle=modern

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
