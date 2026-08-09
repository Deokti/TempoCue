#ifndef AppVersion
  #error Параметр AppVersion обязателен. Запускайте сборку через scripts/build-release.ps1.
#endif

#ifndef PublishDir
  #error Параметр PublishDir обязателен. Запускайте сборку через scripts/build-release.ps1.
#endif

#ifndef OutputDir
  #error Параметр OutputDir обязателен. Запускайте сборку через scripts/build-release.ps1.
#endif

#define AppName "TempoCue"
#define AppExeName "TempoCue.App.exe"

[Setup]
AppId={{60F1BEEC-86C5-4C5D-AF3F-BF2C11AB6BEB}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=TempoCue
DefaultDirName={localappdata}\Programs\TempoCue
DefaultGroupName=TempoCue
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=TempoCue-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TempoCue"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\TempoCue"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,TempoCue}"; Flags: nowait postinstall skipifsilent
