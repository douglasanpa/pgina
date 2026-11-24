; pGina.fork installer (Inno Setup 6.3 or later).
; Build pGina first (tools\build.ps1), then:  ISCC.exe /DMyAppVersion=3.10.0 installer.iss

#define MyAppName "pGina.fork"
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#define MyAppPublisher "pGina.fork"
#define MyAppURL "https://douglasanpa.github.io/pgina/"
#define MyAppExeName "pGina.Configuration.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application, keep it so upgrades replace older installs
AppID={{3D8D0F0D-7DBF-400C-9C44-00BD21986139}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL=https://github.com/douglasanpa/pgina/issues
AppUpdatesURL=https://github.com/douglasanpa/pgina/releases
AppCopyright=pGina Team
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
UsePreviousAppDir=no
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=auto
AllowNoIcons=yes
LicenseFile=..\LICENSE
OutputDir=Output
OutputBaseFilename=pGinaSetup-{#MyAppVersion}
SetupIconFile=pgina_icons.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x86compatible x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
; the credential provider DLL in System32 is replaced at the next restart
AlwaysRestart=yes

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
en.NetMissing=pGina needs the .NET Framework 4.8, which is not installed.%n%nInstall it from https://dotnet.microsoft.com/download/dotnet-framework/net48 and run this setup again.
ptbr.NetMissing=O pGina precisa do .NET Framework 4.8, que não está instalado.%n%nInstale-o pelo endereço https://dotnet.microsoft.com/download/dotnet-framework/net48 e execute esta instalação de novo.
en.InstallingService=Installing the service and the credential provider...
ptbr.InstallingService=Instalando o serviço e o provedor de credenciais...
en.RemovingService=Removing the service and the credential provider...
ptbr.RemovingService=Removendo o serviço e o provedor de credenciais...

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; test programs and build leftovers stay out
#define Skip "*.pdb,*.lib,*.exp,FakeWinlogon.exe,NativeLibTest.exe"
Source: "..\pGina\src\bin\*.exe"; DestDir: "{app}"; Excludes: "{#Skip}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\pGina\src\bin\*.dll"; DestDir: "{app}"; Excludes: "{#Skip}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\pGina\src\bin\*.xml"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\pGina\src\bin\*.config"; DestDir: "{app}"; Excludes: "log4net.config"; Flags: ignoreversion recursesubdirs createallsubdirs
; keep the logging setup of an existing install
Source: "..\pGina\src\bin\log4net.config"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "..\Plugins\bin\*.exe"; DestDir: "{app}\Plugins"; Excludes: "{#Skip}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\Plugins\bin\*.dll"; DestDir: "{app}\Plugins"; Excludes: "{#Skip}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\Plugins\bin\*.config"; DestDir: "{app}\Plugins"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\pGina.InstallUtil.exe"; Parameters: "post-install"; StatusMsg: "{cm:InstallingService}"; WorkingDir: "{app}"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "failure pgina reset= 0 actions= restart/5000"; Flags: runhidden
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runascurrentuser

[UninstallRun]
Filename: "{app}\pGina.InstallUtil.exe"; Parameters: "post-uninstall"; StatusMsg: "{cm:RemovingService}"; WorkingDir: "{app}"; Flags: runhidden; RunOnceId: "RemoveService"

[Code]
function IsNet48Installed(): Boolean;
var
  release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', release)
    and (release >= 528040);
end;

function InitializeSetup(): Boolean;
begin
  Result := IsNet48Installed();
  if not Result then
    SuppressibleMsgBox(CustomMessage('NetMissing'), mbCriticalError, MB_OK, IDOK);
end;

procedure StopPginaService();
var
  resultCode: Integer;
begin
  // fails harmlessly on a first install, when the service doesn't exist
  Exec(ExpandConstant('{sys}\net.exe'), 'stop pgina', '', SW_HIDE, ewWaitUntilTerminated, resultCode);
end;

procedure ReplaceCredentialProvider();
begin
  // LogonUI keeps the DLL loaded, so it is replaced at the next restart
  if Is64BitInstallMode() then
    RestartReplace(ExpandConstant('{app}\x64\pGinaCredentialProvider.dll'), ExpandConstant('{sys}\pGinaCredentialProvider.dll'))
  else
    RestartReplace(ExpandConstant('{app}\Win32\pGinaCredentialProvider.dll'), ExpandConstant('{sys}\pGinaCredentialProvider.dll'));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    StopPginaService()
  else if CurStep = ssPostInstall then
    ReplaceCredentialProvider();
end;
