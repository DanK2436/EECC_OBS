; Script Inno Setup Officiel pour EECC Studio (C# / C++ Natif)
; Packaging d'installation haute performance 64-bit pour Windows 10 et 11

#define MyAppName "EECC Studio"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "EECC — Église Évangélique du Congo"
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
Name: "firewall"; Description: "Autoriser seulement EECC Studio sur les réseaux privés (réception des caméras du réseau local). Ne désactive pas le pare-feu."; GroupDescription: "Réseau et Sécurité:"

[Files]
; Fichiers publiés de l'application C# .NET 8 (Self-contained win-x64)
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "license.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\Désinstaller {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Exception limitée au programme EECC Studio et au profil privé (LAN). Pas de désactivation globale du pare-feu.
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""EECC Studio Camera Receiver"" dir=in action=allow program=""{app}\{#MyAppExeName}"" enable=yes profile=private"; Flags: runhidden; Tasks: firewall
; Option de lancement à la fin de l'installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""EECC Studio Camera Receiver"""; Flags: runhidden
