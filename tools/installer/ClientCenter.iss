; Client Center for Configuration Manager - Inno Setup script
; Built from tools/Build-Installers.ps1
;
; Required defines (passed by ISCC):
;   /DMyAppVersion=1.1.22
;   /DPublishDir=C:\path\to\publish
;   /DOutputDir=C:\path\to\artifacts

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\..\artifacts\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\artifacts"
#endif

#define MyAppName "Client Center for Configuration Manager"
#define MyAppPublisher "drummachine24"
#define MyAppURL "https://github.com/drummachine24/sccmclictr"
#define MyAppExeName "SCCMCliCtrWPF.exe"

[Setup]
AppId={{E8F3A2B1-5C4D-4E9F-A1B2-C3D4E5F60718}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableDirPage=no
DisableProgramGroupPage=no
LicenseFile=
OutputDir={#OutputDir}
OutputBaseFilename=ClientCenter-v{#MyAppVersion}-win-x64-setup
SetupIconFile=..\..\SCCMCliCtrWPF\SCCMCliCtrWPF\Icon16.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}
CloseApplications=yes
CloseApplicationsFilter=*.exe,*.dll
RestartApplications=no
AllowNoIcons=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "consoleext"; Description: "Register ConfigMgr console right-click extension"; GroupDescription: "Integration:"; Flags: checkedonce

[Files]
; Portable install/uninstall scripts are ZIP-only; MSI/EXE use ARP uninstall.
; Register-ConsoleExtension.cmd/.ps1 stay in Setup.exe so the console action can be added/removed.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; \
  Excludes: "Install.cmd,Install-ClientCenter.ps1,Uninstall.cmd,Uninstall-ClientCenter.ps1"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\Register-ConsoleExtension.cmd"; StatusMsg: "Registering ConfigMgr console extension..."; Flags: runhidden waituntilterminated; Tasks: consoleext

[UninstallRun]
Filename: "{app}\Register-ConsoleExtension.cmd"; Parameters: "-Unregister"; Flags: runhidden waituntilterminated; RunOnceId: "UnregCmConsoleExt"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
