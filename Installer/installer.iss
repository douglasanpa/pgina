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

en.TypeStandard=Standard: pGina and the usual plugins
ptbr.TypeStandard=Padrão: pGina e os plugins mais usados
en.TypeFull=All plugins
ptbr.TypeFull=Todos os plugins
en.TypeCustom=Choose the plugins
ptbr.TypeCustom=Escolher os plugins
en.CompMain=pGina (service, logon screen and configuration)
ptbr.CompMain=pGina (serviço, tela de logon e configuração)
en.CompPlugins=Usual plugins
ptbr.CompPlugins=Plugins mais usados
en.CompLdap=LDAP: authentication, authorization and groups
ptbr.CompLdap=LDAP: autenticação, autorização e grupos
en.CompLocal=Local Machine: local accounts and offline logon
ptbr.CompLocal=Local Machine: contas locais e logon sem rede
en.CompSingle=SingleUser: opens every session in one local account
ptbr.CompSingle=SingleUser: abre todas as sessões numa só conta local
en.CompScripting=Scripting: runs scripts at logon and logoff
ptbr.CompScripting=Scripting: roda scripts no logon e no logoff
en.CompExtra=Other plugins
ptbr.CompExtra=Outros plugins
en.CompEmail=E-mail (POP/IMAP) authentication
ptbr.CompEmail=Autenticação por e-mail (POP/IMAP)
en.CompMultiEmail=E-mail authentication on several servers
ptbr.CompMultiEmail=Autenticação por e-mail em vários servidores
en.CompHttp=HTTP authentication
ptbr.CompHttp=Autenticação por HTTP
en.CompKerberos=Kerberos authentication
ptbr.CompKerberos=Autenticação Kerberos
en.CompMysqlAuth=MySQL authentication
ptbr.CompMysqlAuth=Autenticação no MySQL
en.CompMysqlLogger=MySQL session log
ptbr.CompMysqlLogger=Registro das sessões no MySQL
en.CompRadius=RADIUS authentication
ptbr.CompRadius=Autenticação RADIUS
en.CompSsh=SSH authentication
ptbr.CompSsh=Autenticação por SSH
en.CompPgsmb=pgSMB2: roaming profiles on an SMB share, with quota
ptbr.CompPgsmb=pgSMB2: perfis móveis num compartilhamento SMB, com cota
en.CompSessionLimit=Session time limit
ptbr.CompSessionLimit=Limite de tempo da sessão
en.CompUsernameMod=Change the username during logon
ptbr.CompUsernameMod=Altera o nome de usuário durante o logon

[Types]
; a silent install uses the first type, or what the previous installation chose;
; to choose: /COMPONENTS="plugins\ldap,plugins\localmachine,extra\radius"
Name: "standard"; Description: "{cm:TypeStandard}"
Name: "full"; Description: "{cm:TypeFull}"
Name: "custom"; Description: "{cm:TypeCustom}"; Flags: iscustom

[Components]
Name: "main"; Description: "{cm:CompMain}"; Types: standard full custom; Flags: fixed
Name: "plugins"; Description: "{cm:CompPlugins}"; Types: standard full
Name: "plugins\ldap"; Description: "{cm:CompLdap}"; Types: standard full
Name: "plugins\localmachine"; Description: "{cm:CompLocal}"; Types: standard full
Name: "plugins\singleuser"; Description: "{cm:CompSingle}"; Types: standard full
Name: "plugins\scripting"; Description: "{cm:CompScripting}"; Types: standard full
Name: "extra"; Description: "{cm:CompExtra}"; Types: full
Name: "extra\email"; Description: "{cm:CompEmail}"; Types: full
Name: "extra\multiemail"; Description: "{cm:CompMultiEmail}"; Types: full
Name: "extra\httpauth"; Description: "{cm:CompHttp}"; Types: full
Name: "extra\kerberos"; Description: "{cm:CompKerberos}"; Types: full
Name: "extra\mysqlauth"; Description: "{cm:CompMysqlAuth}"; Types: full
Name: "extra\mysqllogger"; Description: "{cm:CompMysqlLogger}"; Types: full
Name: "extra\radius"; Description: "{cm:CompRadius}"; Types: full
Name: "extra\ssh"; Description: "{cm:CompSsh}"; Types: full
Name: "extra\pgsmb2"; Description: "{cm:CompPgsmb}"; Types: full
Name: "extra\sessionlimit"; Description: "{cm:CompSessionLimit}"; Types: full
Name: "extra\usernamemod"; Description: "{cm:CompUsernameMod}"; Types: full

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
; plugins: libraries every plugin can use, then each plugin with what only it needs
#define P "..\Plugins\bin\"
#define PluginOnly "pGina.Plugin.*.dll,MySql.Data.dll,Google.Protobuf.dll,ZstdSharp.dll,K4os.*.dll,Renci.SshNet.dll,Microsoft.Extensions.*.dll,Microsoft.Bcl.*.dll,System.Formats.Asn1.dll,BouncyCastle.Cryptography.dll,authdll*.dll,proquota.*"
Source: "{#P}*.dll"; DestDir: "{app}\Plugins"; Excludes: "{#Skip},{#PluginOnly}"; Flags: ignoreversion
Source: "{#P}pGina.Plugin.Ldap.dll"; DestDir: "{app}\Plugins"; Components: plugins\ldap; Flags: ignoreversion
Source: "{#P}pGina.Plugin.LocalMachine.dll"; DestDir: "{app}\Plugins"; Components: plugins\localmachine; Flags: ignoreversion
Source: "{#P}pGina.Plugin.SingleUser.dll"; DestDir: "{app}\Plugins"; Components: plugins\singleuser; Flags: ignoreversion
Source: "{#P}pGina.Plugin.scripting.dll"; DestDir: "{app}\Plugins"; Components: plugins\scripting; Flags: ignoreversion
Source: "{#P}pGina.Plugin.Email.dll"; DestDir: "{app}\Plugins"; Components: extra\email; Flags: ignoreversion
Source: "{#P}pGina.Plugin.MultiEmail.dll"; DestDir: "{app}\Plugins"; Components: extra\multiemail; Flags: ignoreversion
Source: "{#P}pGina.Plugin.HttpAuth.dll"; DestDir: "{app}\Plugins"; Components: extra\httpauth; Flags: ignoreversion
Source: "{#P}pGina.Plugin.Kerberos.dll"; DestDir: "{app}\Plugins"; Components: extra\kerberos; Flags: ignoreversion
Source: "{#P}authdll*.dll"; DestDir: "{app}\Plugins"; Components: extra\kerberos; Flags: ignoreversion
Source: "{#P}pGina.Plugin.MySQLAuth.dll"; DestDir: "{app}\Plugins"; Components: extra\mysqlauth; Flags: ignoreversion
Source: "{#P}pGina.Plugin.MySqlLogger.dll"; DestDir: "{app}\Plugins"; Components: extra\mysqllogger; Flags: ignoreversion
Source: "{#P}MySql.Data.dll"; DestDir: "{app}\Plugins"; Components: extra\mysqlauth or extra\mysqllogger; Flags: ignoreversion
Source: "{#P}Google.Protobuf.dll"; DestDir: "{app}\Plugins"; Components: extra\mysqlauth or extra\mysqllogger; Flags: ignoreversion
Source: "{#P}ZstdSharp.dll"; DestDir: "{app}\Plugins"; Components: extra\mysqlauth or extra\mysqllogger; Flags: ignoreversion
Source: "{#P}K4os.*.dll"; DestDir: "{app}\Plugins"; Components: extra\mysqlauth or extra\mysqllogger; Flags: ignoreversion
Source: "{#P}BouncyCastle.Cryptography.dll"; DestDir: "{app}\Plugins"; Components: extra\mysqlauth or extra\mysqllogger or extra\ssh; Flags: ignoreversion
Source: "{#P}pGina.Plugin.RADIUSPlugin.dll"; DestDir: "{app}\Plugins"; Components: extra\radius; Flags: ignoreversion
Source: "{#P}pGina.Plugin.SSHAuth.dll"; DestDir: "{app}\Plugins"; Components: extra\ssh; Flags: ignoreversion
Source: "{#P}Renci.SshNet.dll"; DestDir: "{app}\Plugins"; Components: extra\ssh; Flags: ignoreversion
Source: "{#P}Microsoft.Extensions.*.dll"; DestDir: "{app}\Plugins"; Components: extra\ssh; Flags: ignoreversion
Source: "{#P}Microsoft.Bcl.*.dll"; DestDir: "{app}\Plugins"; Components: extra\ssh; Flags: ignoreversion
Source: "{#P}System.Formats.Asn1.dll"; DestDir: "{app}\Plugins"; Components: extra\ssh; Flags: ignoreversion
Source: "{#P}pGina.Plugin.pgSMB2.dll"; DestDir: "{app}\Plugins"; Components: extra\pgsmb2; Flags: ignoreversion
Source: "{#P}proquota.exe"; DestDir: "{app}\Plugins"; Components: extra\pgsmb2; Flags: ignoreversion
Source: "{#P}proquota.exe.config"; DestDir: "{app}\Plugins"; Components: extra\pgsmb2; Flags: ignoreversion
Source: "{#P}pGina.Plugin.SessionLimit.dll"; DestDir: "{app}\Plugins"; Components: extra\sessionlimit; Flags: ignoreversion
Source: "{#P}pGina.Plugin.UsernameMod.dll"; DestDir: "{app}\Plugins"; Components: extra\usernamemod; Flags: ignoreversion

[InstallDelete]
; plugins no longer shipped
Type: files; Name: "{app}\Plugins\pGina.Plugin.Sample.dll"
Type: files; Name: "{app}\Plugins\pGina.Plugin.pgSMB.dll"
Type: files; Name: "{app}\Plugins\*.pdb"
; plugins unchecked in this installation (an upgrade can remove them too)
Type: files; Name: "{app}\Plugins\pGina.Plugin.Ldap.dll"; Components: not plugins\ldap
Type: files; Name: "{app}\Plugins\pGina.Plugin.LocalMachine.dll"; Components: not plugins\localmachine
Type: files; Name: "{app}\Plugins\pGina.Plugin.SingleUser.dll"; Components: not plugins\singleuser
Type: files; Name: "{app}\Plugins\pGina.Plugin.scripting.dll"; Components: not plugins\scripting
Type: files; Name: "{app}\Plugins\pGina.Plugin.Email.dll"; Components: not extra\email
Type: files; Name: "{app}\Plugins\pGina.Plugin.MultiEmail.dll"; Components: not extra\multiemail
Type: files; Name: "{app}\Plugins\pGina.Plugin.HttpAuth.dll"; Components: not extra\httpauth
Type: files; Name: "{app}\Plugins\pGina.Plugin.Kerberos.dll"; Components: not extra\kerberos
Type: files; Name: "{app}\Plugins\authdll*.dll"; Components: not extra\kerberos
Type: files; Name: "{app}\Plugins\pGina.Plugin.MySQLAuth.dll"; Components: not extra\mysqlauth
Type: files; Name: "{app}\Plugins\pGina.Plugin.MySqlLogger.dll"; Components: not extra\mysqllogger
Type: files; Name: "{app}\Plugins\MySql.Data.dll"; Components: not (extra\mysqlauth or extra\mysqllogger)
Type: files; Name: "{app}\Plugins\Google.Protobuf.dll"; Components: not (extra\mysqlauth or extra\mysqllogger)
Type: files; Name: "{app}\Plugins\ZstdSharp.dll"; Components: not (extra\mysqlauth or extra\mysqllogger)
Type: files; Name: "{app}\Plugins\K4os.*.dll"; Components: not (extra\mysqlauth or extra\mysqllogger)
Type: files; Name: "{app}\Plugins\BouncyCastle.Cryptography.dll"; Components: not (extra\mysqlauth or extra\mysqllogger or extra\ssh)
Type: files; Name: "{app}\Plugins\pGina.Plugin.RADIUSPlugin.dll"; Components: not extra\radius
Type: files; Name: "{app}\Plugins\pGina.Plugin.SSHAuth.dll"; Components: not extra\ssh
Type: files; Name: "{app}\Plugins\Renci.SshNet.dll"; Components: not extra\ssh
Type: files; Name: "{app}\Plugins\Microsoft.Extensions.*.dll"; Components: not extra\ssh
Type: files; Name: "{app}\Plugins\Microsoft.Bcl.*.dll"; Components: not extra\ssh
Type: files; Name: "{app}\Plugins\System.Formats.Asn1.dll"; Components: not extra\ssh
Type: files; Name: "{app}\Plugins\pGina.Plugin.pgSMB2.dll"; Components: not extra\pgsmb2
Type: files; Name: "{app}\Plugins\proquota.exe*"; Components: not extra\pgsmb2
Type: files; Name: "{app}\Plugins\pGina.Plugin.SessionLimit.dll"; Components: not extra\sessionlimit
Type: files; Name: "{app}\Plugins\pGina.Plugin.UsernameMod.dll"; Components: not extra\usernamemod

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

// A plugin turned on in this computer's pGina settings stays installed, also in silent upgrades
// (Salt, automatic updates) and in upgrades from versions that didn't record the components.
procedure KeepIfEnabled(Component, Uuid: String);
var
  root: Integer;
  mask: Cardinal;
begin
  if IsWin64 then root := HKLM64 else root := HKLM;
  if RegQueryDWordValue(root, 'SOFTWARE\pGina3.fork', Uuid, mask) and (mask <> 0) then
  begin
    Log('Plugin ' + Component + ' is enabled in the settings: keeping it');
    WizardSelectComponents(Component);
  end;
end;

procedure InitializeWizard();
begin
  KeepIfEnabled('plugins\ldap', '0f52390b-c781-43ae-bd62-553c77fa4cf7');
  KeepIfEnabled('plugins\localmachine', '12fa152d-a2e3-4c8d-9535-5dcd49dfcb6d');
  KeepIfEnabled('plugins\singleuser', '81f8034e-e278-4754-b10c-7066656de5b7');
  KeepIfEnabled('plugins\scripting', 'ea94cebf-6ed1-454a-b333-81c4fad61fa4');
  KeepIfEnabled('extra\email', 'ec3221a6-621f-44ce-b77b-e074298d6b4e');
  KeepIfEnabled('extra\multiemail', '85f2ff31-0b1d-4d01-a462-635059343fe3');
  KeepIfEnabled('extra\httpauth', 'c4ff794f-5843-4bef-90ba-b5e4e488c662');
  KeepIfEnabled('extra\kerberos', '16e22b15-4116-4fa4-9bb2-57d54bf61a43');
  KeepIfEnabled('extra\mysqlauth', 'a89df410-53ca-4fe1-a6ca-4479b841ca19');
  KeepIfEnabled('extra\mysqllogger', 'b68cf064-9299-4765-ac08-acb49f93f892');
  KeepIfEnabled('extra\radius', '350047a0-2d0b-4e24-9f99-16cd18d6b142');
  KeepIfEnabled('extra\ssh', 'cc35057c-aca8-499c-b127-ae4cf978f238');
  KeepIfEnabled('extra\pgsmb2', '10c41590-07f9-4ff2-b77b-e51e7a51206f');
  KeepIfEnabled('extra\sessionlimit', 'd73131d7-7af2-47bb-bbf4-4f8583b44962');
  KeepIfEnabled('extra\usernamemod', '98477b3a-830d-4bee-b270-2d7435275f9c');
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
