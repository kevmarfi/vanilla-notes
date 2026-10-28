; The installer of Vanilla Notes, for Inno Setup 7. It puts the one executable published by the
; profile of one processor (win-x64, or win-arm64 for Windows on Arm) into the folder of the
; application, with its shortcuts and its uninstaller. The processor is chosen when it is compiled:
; ISCC /DArch=arm64 builds the one for Arm; with nothing given, it is the one for x64.
; The data is not touched: it lives in %APPDATA%\Vanilla Notes and stays there on uninstall.
; How it is built is in docs/05-operations/installer.md.

#define AppName "Vanilla Notes"
#define AppExe "VanillaNotes.exe"
#ifndef Arch
  #define Arch "x64"
#endif
#if Arch != "x64" && Arch != "arm64"
  #error Arch must be x64 or arm64
#endif
#define PublishDir AddBackslash(SourcePath) + "..\publish\win-" + Arch

; The version is the one of the executable, so that it is written in one place only.
#define VersionMajor
#define VersionMinor
#define VersionPatch
#define VersionBuild
#expr GetVersionComponents(PublishDir + "\" + AppExe, VersionMajor, VersionMinor, VersionPatch, VersionBuild)
#define AppVersion Str(VersionMajor) + "." + Str(VersionMinor) + "." + Str(VersionPatch)

[Setup]
; The identity of the application for Windows. Never changed: a new release with the same one
; installs over the older one instead of beside it.
AppId={{307A6667-BF18-49E4-AE1C-BEB044FF999B}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Kev Martínez
AppCopyright=Copyright (c) 2026 Kev Martínez
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; For every user, in Program Files, by default; the person may choose to install for themselves only.
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog commandline
#if Arch == "arm64"
; Inno Setup makes no Setup for Arm: a 32-bit one runs on every Windows on Arm, Windows 10 among
; them, which cannot run x64, and installs the Arm executable in 64-bit mode.
SetupArchitecture=x86
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
; x64-compatible takes in Windows 11 on Arm too, where the x64 executable runs emulated.
SetupArchitecture=x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
; Windows 10 version 2004 (build 19041) or later, and Windows 11: the oldest that has the Windows Hello the application uses.
MinVersion=10.0.19041
LicenseFile=..\LICENSE.md
; The lock the application holds while it runs: the installer asks for it to be closed first.
AppMutex=VanillaNotes.Desktop.SingleCopy
CloseApplications=yes
RestartApplications=no
SetupIconFile=..\src\VanillaNotes.App\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern dynamic
WizardImageFile=wizard-image.png
WizardSmallImageFile=wizard-small.png
OutputDir=..\publish
OutputBaseFilename=VanillaNotesSetup-{#AppVersion}-{#Arch}
Compression=lzma2/max
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
