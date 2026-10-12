[Setup]
AppId={{94CB85DC-2356-4C38-904B-38BC51226FB3}
AppName=Litfix
AppVersion=2.0.26.0
AppPublisher=Lushen16
AppPublisherURL=https://github.com/Lushen16/LIT-fix
DefaultDirName={localappdata}\Programs\Litfix
DefaultGroupName=Litfix
UsePreviousGroup=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..
OutputBaseFilename=Litfix-Setup-2.0.26.0
SetupIconFile=..\PKAproxy.ico
UninstallDisplayIcon={app}\Litfix-brand-2.0.5.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
CloseApplications=no
RestartApplications=no
InfoBeforeFile=Instalacao.txt

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Files]
Source: "..\PKAproxy.ico"; DestDir: "{app}"; DestName: "Litfix-brand-2.0.5.ico"; Flags: ignoreversion
Source: "..\PKA-Proxy.exe"; DestDir: "{app}"; Flags: ignoreversion
#ifndef LocalUnsigned
Source: "..\PKA-Proxy.exe.sig"; DestDir: "{app}"; Flags: ignoreversion
#endif
Source: "..\Verificar-assinatura.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PUBLIC-KEY.xml"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PUBLIC-KEY-SHA256.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Recuperar-rede.cmd"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\DISCORD-PERMANENTE.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LEIA-ME.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\INSTALADOR.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\SEGURANCA.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\AVISOS.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE-sing-box.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion recursesubdirs
Source: "..\upstream\*"; DestDir: "{app}\upstream"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\Litfix"; Filename: "{app}\PKA-Proxy.exe"; IconFilename: "{app}\Litfix-brand-2.0.5.ico"; AppUserModelID: "Lushen16.Litfix"
Name: "{group}\Recuperar rede (executar como administrador)"; Filename: "{app}\Recuperar-rede.cmd"; IconFilename: "{app}\Litfix-brand-2.0.5.ico"
Name: "{group}\Desinstalar Litfix"; Filename: "{uninstallexe}"; IconFilename: "{app}\Litfix-brand-2.0.5.ico"
Name: "{autodesktop}\Litfix"; Filename: "{app}\PKA-Proxy.exe"; IconFilename: "{app}\Litfix-brand-2.0.5.ico"; AppUserModelID: "Lushen16.Litfix"; Tasks: desktopicon

[InstallDelete]
Type: files; Name: "{userprograms}\PKA Proxy Launcher\PKA Proxy Launcher.lnk"
Type: files; Name: "{userprograms}\PKA Proxy Launcher\Desinstalar PKA Proxy Launcher.lnk"
Type: files; Name: "{userdesktop}\PKA Proxy Launcher.lnk"

[Run]
Filename: "{app}\PKA-Proxy.exe"; Description: "Abrir Litfix"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 461808);
  if not Result then MsgBox('Instale o .NET Framework 4.7.2 ou superior antes de continuar.', mbError, MB_OK);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if CheckForMutexes('Global\PKAproxy-TUN') then
    Result := 'Pare o proxy e feche o Litfix antes de instalar. O motor de rede está ativo.';
end;

function InitializeUninstall(): Boolean;
var ResultCode: Integer;
begin
  Result := False;
  if FileExists(ExpandConstant('{app}\PKA-Proxy.exe')) then begin
    if not ShellExec('runas', ExpandConstant('{app}\PKA-Proxy.exe'), '--wireguard-remove', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then exit;
    if ResultCode <> 0 then begin
      MsgBox('Desative a VPN na opção WireGuard VPN antes de desinstalar.', mbError, MB_OK);
      exit;
    end;
    if not ShellExec('runas', ExpandConstant('{app}\PKA-Proxy.exe'), '--discord-disable', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then begin
      MsgBox('Permita a remoção da proxy permanente do Discord para desinstalar.', mbError, MB_OK);
      exit;
    end;
    if ResultCode <> 0 then begin
      MsgBox('Não foi possível remover a proxy permanente. Use a aba Discord e tente novamente.', mbError, MB_OK);
      exit;
    end;
  end;
  if CheckForMutexes('Global\PKAproxy-TUN') then begin
    MsgBox('Pare o proxy antes de desinstalar.', mbError, MB_OK);
    exit;
  end;
  if FileExists(ExpandConstant('{commonappdata}\PKAproxyV2\guard.keys')) then begin
    MsgBox('Antes de desinstalar, abra o Litfix e use Liberar rede direta, ou execute Recuperar-rede.cmd como administrador. O bloqueio de rede ainda está registrado.', mbError, MB_OK);
    exit;
  end;
  Result := True;
end;
