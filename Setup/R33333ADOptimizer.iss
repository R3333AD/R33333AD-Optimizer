; R33333AD Optimizer — installeur (Inno Setup 6)
#define MyAppName "R33333AD Optimizer"
#define MyAppVersion "1.2.1"
#define MyAppExeName "R33333ADOptimizer.exe"

[Setup]
AppId={{A3R3333A-D0PTI-MIZER-1100}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=R33333AD
DefaultDirName={autopf}\R33333AD Optimizer
DefaultGroupName=R33333AD Optimizer
OutputDir=Output
OutputBaseFilename=Setup-R33333ADOptimizer-{#MyAppVersion}
SetupIconFile=..\src\FPSBooster.App\Assets\logo.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
DisableProgramGroupPage=yes
CloseApplications=yes
CloseApplicationsFilter=R33333ADOptimizer.exe

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[Tasks]
Name: "desktopicon"; Description: "Icône sur le bureau"; GroupDescription: "Icônes :"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\R33333AD Optimizer"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\R33333AD Optimizer"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; shellexec+runascurrentuser : relance via l'utilisateur d'origine pour que l'UAC admin s'affiche (sinon erreur 740)
Filename: "{app}\{#MyAppExeName}"; Description: "Lancer R33333AD Optimizer"; Flags: nowait postinstall skipifsilent shellexec runascurrentuser
