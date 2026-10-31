#ifndef AppVersion
  #error AppVersion must be supplied by Package.ps1
#endif
#ifndef PackageDir
  #error PackageDir must be supplied by Package.ps1
#endif
#ifndef OutputDir
  #error OutputDir must be supplied by Package.ps1
#endif

[Setup]
AppId={{22D9452B-1DDC-49D3-A931-12E4661EAA77}
AppName=HDR Pilot
AppVersion={#AppVersion}
AppPublisher=HDR Pilot contributors
AppPublisherURL=https://github.com/hmh6a/hdr-pilot
AppSupportURL=https://github.com/hmh6a/hdr-pilot/issues
AppUpdatesURL=https://github.com/hmh6a/hdr-pilot/releases
DefaultDirName={localappdata}\Programs\HDR Pilot
DefaultGroupName=HDR Pilot
DisableDirPage=no
AlwaysShowDirOnReadyPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.22000
SetupArchitecture=x64
OutputDir={#OutputDir}
OutputBaseFilename=HDRPilot-{#AppVersion}-win-x64-Setup
SetupIconFile=..\assets\hdrpilot.ico
UninstallDisplayIcon={app}\HDRPilot.exe
AppMutex=Local\HDRPilot.Installation
CloseApplications=no
RestartApplications=no
WizardStyle=modern
Compression=lzma2
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PackageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\HDR Pilot"; Filename: "{app}\HDRPilot.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\HDR Pilot"; Filename: "{app}\HDRPilot.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\HDRPilot.exe"; Description: "Launch HDR Pilot"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent unchecked

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  StartupCommand: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    { Remove startup only when it belongs to this installation. Preserve settings and other copies. }
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'HDRPilot', StartupCommand) then
      if CompareText(StartupCommand, '"' + ExpandConstant('{app}\HDRPilot.exe') + '" --minimized') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'HDRPilot');
  end;
end;
