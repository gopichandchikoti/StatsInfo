#ifndef AppName
#define AppName "StatsInfo"
#endif
#ifndef AppVersion
#define AppVersion "2.1.3"
#endif
#ifndef AppIdentifier
#define AppIdentifier "{{CBB4A99D-3D82-4C62-9D8F-59302BFDA702}"
#endif
#define AppPublisher "Portfolio Engineering"
#define AppExeName "StatsInfo.exe"
#ifndef PublishDirectory
#define PublishDirectory "..\artifacts\publish\win-x64"
#endif

[Setup]
AppId={#AppIdentifier}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=..\artifacts\installer
OutputBaseFilename={#AppName}-Setup-{#AppVersion}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "{#PublishDirectory}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD_PARTY_NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\licenses\*.txt"; DestDir: "{app}\licenses"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  StartupCommand: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run',
      '{#AppName}', StartupCommand) then
    begin
      if Pos(Lowercase(ExpandConstant('{app}\')), Lowercase(StartupCommand)) > 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#AppName}');
    end;
  end;
end;
