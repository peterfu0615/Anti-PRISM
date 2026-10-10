; Anti-PRISM Inno Setup Script
; Targets Windows 10/11 x64

#define MyAppName "Anti-PRISM"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Anti-PRISM Security"
#define MyAppExeName "BrightnessCtl.exe"
#define MySvcExeName "AntiPrism.Svc.exe"

[Setup]
AppId={{629B4F4A-3BE7-4D6F-9C1D-894E2BA70F32}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Anti-PRISM
DefaultGroupName={#MyAppName}
DisableDirPage=no
DisableProgramGroupPage=yes
OutputDir=..\dist\installer
OutputBaseFilename=AntiPrism_Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible

[Dirs]
Name: "{commonappdata}\Anti-PRISM"; Permissions: users-modify
Name: "{commonappdata}\Anti-PRISM\logs"; Permissions: users-modify

[Files]
; Binaries from published output
Source: "..\dist\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Run]
; 1. Create and start Windows Service under SYSTEM
Filename: "sc.exe"; Parameters: "create AntiPrismSvc binPath= ""{app}\{#MySvcExeName}"" start= auto obj= ""NT AUTHORITY\SYSTEM"" DisplayName= ""Display Brightness Calibration Service"""; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "failure AntiPrismSvc reset= 0 actions= restart/5000/restart/5000/restart/5000"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "start AntiPrismSvc"; Flags: runhidden waituntilterminated

; 2. Register Task Scheduler for disguised Tray on user logon
Filename: "schtasks.exe"; Parameters: "/create /tn ""AntiPrismTray"" /tr ""\""{app}\{#MyAppExeName}\"" --hidden"" /sc onlogon /rl highest /f"; Flags: runhidden waituntilterminated

; 3. Launch Tray immediately for current installer session
Filename: "{app}\{#MyAppExeName}"; Parameters: "--hidden"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; EMERGENCY RESTORE: Always re-enable all cameras before deleting files to prevent bricking
Filename: "{app}\{#MySvcExeName}"; Parameters: "--unbrick"; Flags: runhidden waituntilterminated

; Terminate running processes
Filename: "taskkill.exe"; Parameters: "/f /im {#MyAppExeName} /im {#MySvcExeName}"; Flags: runhidden waituntilterminated

; Stop and remove Windows Service
Filename: "sc.exe"; Parameters: "stop AntiPrismSvc"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "delete AntiPrismSvc"; Flags: runhidden waituntilterminated

; Remove scheduled task
Filename: "schtasks.exe"; Parameters: "/delete /tn ""AntiPrismTray"" /f"; Flags: runhidden waituntilterminated

[Code]
// Helper function to check if Windows version is supported
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
