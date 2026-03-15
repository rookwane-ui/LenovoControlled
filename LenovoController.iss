; LenovoController.iss
[Setup]
AppName=LenovoController
AppVersion={#AppVersion}
AppPublisher=Your Name
AppPublisherURL=https://github.com/yourusername/LenovoController
DefaultDirName={autopf}\LenovoController
DefaultGroupName=LenovoController
OutputBaseFilename=LenovoController-Setup
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\LenovoController"; Filename: "{app}\LenovoController.exe"
Name: "{autodesktop}\LenovoController"; Filename: "{app}\LenovoController.exe"

[Run]
Filename: "{app}\LenovoController.exe"; Description: "Launch LenovoController"; Flags: postinstall nowait skipifsilent