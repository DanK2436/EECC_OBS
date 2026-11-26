; Script Inno Setup Officiel pour EECC Studio (C# / C++ Natif)
; Packaging d'installation haute performance 64-bit pour Windows 10 et 11

#define MyAppName "EECC Studio"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "EECC — Église Évangélique les Cohéritiers du Christ"
#define MyAppURL "https://eecc.cd"
#define MyAppExeName "EeccStudio.exe"

[Setup]
AppId={{5E7C4B18-12D3-48FA-97E5-C004F34C8561}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=license.txt
SetupIconFile=..\src\EeccStudio.App\Assets\eecc.ico
OutputDir=..\dist_installer
OutputBaseFilename=EECC_Studio_Setup_x64
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "firewall"; Description: "Autoriser EECC Studio dans le pare-feu Windows (pour la réception des caméras mobiles)"; GroupDescription: "Réseau et Sécurité:"

[Files]
; Application et runtime .NET 8 Windows (publication autonome win-x64)
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "license.txt"; DestDir: "{app}"; Flags: ignoreversion
; ffmpeg complet, téléchargé par GitHub Actions avant la compilation du setup.
; Les deux lignes explicites font échouer le setup si le téléchargement a été oublié.
Source: "..\third_party\ffmpeg\ffmpeg.exe"; DestDir: "{app}\ffmpeg"; Flags: ignoreversion
Source: "..\third_party\ffmpeg\ffprobe.exe"; DestDir: "{app}\ffmpeg"; Flags: ignoreversion
Source: "..\third_party\ffmpeg\*"; DestDir: "{app}\ffmpeg"; Excludes: ".gitkeep"; Flags: ignoreversion recursesubdirs createallsubdirs
; Même secret que la base EECC, pour que chaque régie installée puisse publier
Source: "studio.secret"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Désinstaller {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Règle pare-feu pour le serveur de réception de caméra mobile (Port 8089)
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""EECC Studio Camera Receiver"" dir=in action=allow program=""{app}\{#MyAppExeName}"" enable=yes"; Flags: runhidden; Tasks: firewall
; Option de lancement à la fin de l'installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""EECC Studio Camera Receiver"""; Flags: runhidden

[Code]
const
  WM_SETTINGCHANGE = $001A;
  HWND_BROADCAST = $FFFF;
  SMTO_ABORTIFHUNG = $0002;

function SendMessageTimeout(hWnd: Integer; Msg: Cardinal; wParam: Integer; lParam: String; fuFlags: Cardinal; uTimeout: Cardinal; var lpdwResult: Cardinal): Integer;
  external 'SendMessageTimeoutW@user32.dll stdcall';

function FfmpegDir: String;
begin
  Result := ExpandConstant('{app}\ffmpeg');
end;

function PathContains(PathValue, Directory: String): Boolean;
var
  Item, Rest: String;
begin
  Result := False;
  Rest := PathValue;
  while Rest <> '' do
  begin
    if Pos(';', Rest) > 0 then
    begin
      Item := Copy(Rest, 1, Pos(';', Rest) - 1);
      Rest := Copy(Rest, Pos(';', Rest) + 1, MaxInt);
    end
    else
    begin
      Item := Rest;
      Rest := '';
    end;
    if CompareText(Trim(Item), Directory) = 0 then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

procedure AddDirToUserPath(Directory: String);
var
  PathValue: String;
begin
  if not RegQueryStringValue(HKCU, 'Environment', 'Path', PathValue) then
    PathValue := '';
  if PathContains(PathValue, Directory) then
    Exit;
  if PathValue = '' then
    PathValue := Directory
  else if PathValue[Length(PathValue)] = ';' then
    PathValue := PathValue + Directory
  else
    PathValue := PathValue + ';' + Directory;
  RegWriteStringValue(HKCU, 'Environment', 'Path', PathValue);
end;

function RemoveDirFromPath(PathValue, Directory: String): String;
var
  Item, Rest, Built: String;
begin
  Rest := PathValue;
  Built := '';
  while Rest <> '' do
  begin
    if Pos(';', Rest) > 0 then
    begin
      Item := Copy(Rest, 1, Pos(';', Rest) - 1);
      Rest := Copy(Rest, Pos(';', Rest) + 1, MaxInt);
    end
    else
    begin
      Item := Rest;
      Rest := '';
    end;
    if CompareText(Trim(Item), Directory) <> 0 then
    begin
      if Built = '' then
        Built := Item
      else
        Built := Built + ';' + Item;
    end;
  end;
  Result := Built;
end;

procedure RemoveDirFromUserPath(Directory: String);
var
  PathValue: String;
begin
  if not RegQueryStringValue(HKCU, 'Environment', 'Path', PathValue) then
    Exit;
  RegWriteStringValue(HKCU, 'Environment', 'Path', RemoveDirFromPath(PathValue, Directory));
end;

procedure BroadcastEnvironmentChange;
var
  Unused: Cardinal;
begin
  SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, 0, 'Environment', SMTO_ABORTIFHUNG, 5000, Unused);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    AddDirToUserPath(FfmpegDir);
    BroadcastEnvironmentChange;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RemoveDirFromUserPath(FfmpegDir);
    BroadcastEnvironmentChange;
  end;
end;
