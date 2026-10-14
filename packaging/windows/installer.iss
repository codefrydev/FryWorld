; Inno Setup 6 Script for FryWorld by Code Fry Dev
#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif

; Four-part numeric version for the setup.exe version resource.
#ifndef MyAppVersionNumeric
#define MyAppVersionNumeric "1.0.0.0"
#endif

#ifndef MyPublishDir
#define MyPublishDir "..\..\publish\win-x64"
#endif

#define MyAppName "FryWorld"
#define MyAppPublisher "Code Fry Dev"
#define MyAppCopyright "Copyright (C) 2026 Code Fry Dev"
#define MyAppURL "https://codefrydev.in"
#define MyAppSupportURL "mailto:codefrydev@gmail.com"
#define MyAppExeName "FryWorld.exe"

[Setup]
; Basic Application Info
AppId={{E58F9A1B-3C4D-4E5F-8A9B-0C1D2E3F4A5B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppSupportURL}
AppUpdatesURL={#MyAppURL}
AppCopyright={#MyAppCopyright}

; Add/Remove Programs
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName},0

; Destination Directories
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Output Configuration
OutputDir=.
OutputBaseFilename=FryWorld-Setup-{#MyAppVersion}
SetupIconFile=..\..\src\FryWorld\Assets\app-logo.ico
LicenseFile=..\..\LICENSE

; Wizard Branding
WizardStyle=modern
WizardImageFile=branding\wizard-large.png
WizardSmallImageFile=branding\wizard-small.png

; setup.exe's own file properties
VersionInfoVersion={#MyAppVersionNumeric}
VersionInfoProductVersion={#MyAppVersionNumeric}
VersionInfoTextVersion={#MyAppVersion}
VersionInfoProductTextVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoCopyright={#MyAppCopyright}
VersionInfoDescription={#MyAppName} Setup
VersionInfoProductName={#MyAppName}
VersionInfoOriginalFileName=FryWorld-Setup.exe

; Compression & Behaviour
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
SetupMutex=FryWorldSetupMutex
CloseApplications=yes
RestartApplications=no

; File Associations
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
SetupAppTitle={#MyAppName} Setup
SetupWindowTitle={#MyAppName} Setup
UninstallAppTitle={#MyAppName} Uninstall
UninstallAppFullTitle={#MyAppName} Uninstall

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "fileassoc_plugin"; Description: "Associate with FryWorld Plugin Packages (.fryplugin)"; GroupDescription: "File Associations:"

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Open-with list
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#MyAppName}"; Flags: uninsdeletekey

; .fryplugin Association
Root: HKA; Subkey: "Software\Classes\.fryplugin"; ValueType: string; ValueName: ""; ValueData: "FryWorld.Plugin"; Flags: uninsdeletevalue; Tasks: fileassoc_plugin
Root: HKA; Subkey: "Software\Classes\FryWorld.Plugin"; ValueType: string; ValueName: ""; ValueData: "FryWorld Plugin Package"; Flags: uninsdeletekey; Tasks: fileassoc_plugin
Root: HKA; Subkey: "Software\Classes\FryWorld.Plugin\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey; Tasks: fileassoc_plugin
Root: HKA; Subkey: "Software\Classes\FryWorld.Plugin\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: fileassoc_plugin

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
