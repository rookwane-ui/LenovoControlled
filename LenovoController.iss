; LenovoController.iss
[Setup]
AppName=LenovoController
AppVersion={#AppVersion}
AppPublisher=Your Name
AppPublisherURL=https://github.com/yourusername/LenovoController
AppSupportURL=https://github.com/yourusername/LenovoController
AppUpdatesURL=https://github.com/yourusername/LenovoController
DefaultDirName={autopf}\LenovoController
DefaultGroupName=LenovoController
OutputDir=.
OutputBaseFilename=LenovoController-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
SetupIconFile=app.ico  ; Add if you have an icon

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Include any additional files like .NET runtime if needed

[Icons]
Name: "{group}\LenovoController"; Filename: "{app}\LenovoController.exe"
Name: "{group}\Uninstall LenovoController"; Filename: "{uninstallexe}"
Name: "{autodesktop}\LenovoController"; Filename: "{app}\LenovoController.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\LenovoController.exe"; Description: "{cm:LaunchProgram,LenovoController}"; Flags: nowait postinstall skipifsilent
