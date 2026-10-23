#ifndef PackageDir
  #error PackageDir is required
#endif
#ifndef OutputDir
  #error OutputDir is required
#endif
#ifndef ReleaseName
  #define ReleaseName "RAFS-Search"
#endif

[Setup]
AppId={{A942A197-D009-4938-84C9-26EB27AF7BD8}
AppName=RAFS Search
AppVersion=1.1.0
AppPublisher=Haris K
DefaultDirName={localappdata}\Programs\RAFS-Search
DefaultGroupName=RAFS Search
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename={#ReleaseName}-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\RAFS-Search.exe
LicenseFile={#PackageDir}\LICENSE
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
ChangesAssociations=no

[Files]
Source: "{#PackageDir}\RAFS-Search.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\RAFS-Search.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\engine\fsearch.exe"; DestDir: "{app}\engine"; Flags: ignoreversion
Source: "{#PackageDir}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\demo-data\*"; DestDir: "{app}\demo-data"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\RAFS Search"; Filename: "{app}\RAFS-Search.exe"

[Run]
Filename: "{app}\RAFS-Search.exe"; Description: "Open RAFS Search"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
  if not Result then
    MsgBox('RAFS Search needs Microsoft .NET Framework 4.8. Install it from Microsoft, then run Setup again.', mbError, MB_OK);
end;

// No UninstallDelete rules: only installer-owned files are removed. User
// preferences, exports, generated workloads and academic folders are retained.
