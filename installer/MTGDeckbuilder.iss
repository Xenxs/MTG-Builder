#ifndef AppVersion
  #define AppVersion "1.4.0"
#endif
#ifndef PublishDir
  #error PublishDir is required
#endif
#ifndef ReleaseDir
  #error ReleaseDir is required
#endif
[Setup]
AppId={{B3161121-CA76-4F5B-839F-E095FAE460A7}
AppName=MTG Smart Deckbuilder
AppVersion={#AppVersion}
AppPublisher=MTGDeckbuilder
DefaultDirName={localappdata}\Programs\MTGDeckbuilder
DefaultGroupName=MTG Smart Deckbuilder
UsePreviousGroup=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#ReleaseDir}
OutputBaseFilename=MTGDeckbuilder-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\MTGDeckbuilder.Desktop\Assets\magic.ico
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\MTGDeckbuilder.Desktop.exe
CloseApplications=yes
RestartApplications=no
[Languages]
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\MTG Smart Deckbuilder"; Filename: "{app}\MTGDeckbuilder.Desktop.exe"
[InstallDelete]
Type: files; Name: "{userprograms}\MTG Arena Smart Deckbuilder\MTG Arena Smart Deckbuilder.lnk"
[Run]
Filename: "{app}\MTGDeckbuilder.Desktop.exe"; Description: "MTG Smart Deckbuilder starten"; Flags: nowait postinstall skipifsilent
