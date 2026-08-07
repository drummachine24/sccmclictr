; Client Center for Configuration Manager - Inno Setup script
; Built from tools/Build-Installers.ps1
;
; Required defines (passed by ISCC):
;   /DMyAppVersion=1.1.4
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
DisableProgramGroupPage=yes
LicenseFile=
OutputDir={#OutputDir}
OutputBaseFilename=ClientCenter-v{#MyAppVersion}-win-x64-setup
SetupIconFile=
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

[Files]
; Portable install/uninstall scripts are ZIP-only; MSI/EXE use ARP uninstall.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; \
  Excludes: "Install.cmd,Install-ClientCenter.ps1,Uninstall.cmd,Uninstall-ClientCenter.ps1"

[Icons]
Name: "{group}\Client Center"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\Uninstall Client Center"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Client Center"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
