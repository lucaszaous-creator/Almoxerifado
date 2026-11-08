; Instalador do ALMOX PRO (Inno Setup 6)
; Compilado pelo workflow de release (.github/workflows/release.yml) ou
; manualmente: iscc /DAppVersion=1.0.0 /DPublishDir=..\publish installer\ALMOXPRO.iss

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\ALMOXPRO.UI\bin\Release\net8.0-windows\win-x64\publish"
#endif

[Setup]
AppId={{8F4A7C21-93B5-4E1D-A6F0-2C5D8B9E3A47}
AppName=ALMOX PRO
AppVersion={#AppVersion}
AppPublisher=ALMOX PRO
DefaultDirName={autopf}\ALMOX PRO
DefaultGroupName=ALMOX PRO
UninstallDisplayIcon={app}\ALMOXPRO.exe
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
OutputBaseFilename=ALMOXPRO-Setup-{#AppVersion}
WizardStyle=modern
PrivilegesRequired=admin

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos:"

[Files]
; A configuração do banco nunca é sobrescrita em atualizações e não é
; removida na desinstalação (preserva a connection string do cliente).
Source: "{#PublishDir}\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "appsettings.json"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\ALMOX PRO"; Filename: "{app}\ALMOXPRO.exe"
Name: "{group}\Configurar banco de dados (appsettings.json)"; Filename: "notepad.exe"; Parameters: """{app}\appsettings.json"""
Name: "{autodesktop}\ALMOX PRO"; Filename: "{app}\ALMOXPRO.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ALMOXPRO.exe"; Description: "Abrir o ALMOX PRO"; Flags: nowait postinstall skipifsilent
