[Setup]
AppName=ACE Optimizer
AppVersion=1.2.0
AppPublisher=Ninthless
AppPublisherURL=https://github.com/Ninthless
AppSupportURL=https://github.com/Ninthless
AppUpdatesURL=https://github.com/Ninthless
DefaultDirName={autopf}\ACEOptimizer
DefaultGroupName=ACE Optimizer
AllowNoIcons=yes
OutputDir=e:\Dev\Project\ACEOptimizer\installer
OutputBaseFilename=ACEOptimizer_Setup_v1.2.0
SetupIconFile=e:\Dev\Project\ACEOptimizer\app.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\ACEOptimizer.exe
UninstallDisplayName=ACE Optimizer

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "开机自动启动"; GroupDescription: "启动选项:"; Flags: unchecked

[Files]
Source: "e:\Dev\Project\ACEOptimizer\publish\ACEOptimizer.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "e:\Dev\Project\ACEOptimizer\app.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\ACE Optimizer"; Filename: "{app}\ACEOptimizer.exe"
Name: "{group}\Uninstall ACE Optimizer"; Filename: "{uninstallexe}"
Name: "{autodesktop}\ACE Optimizer"; Filename: "{app}\ACEOptimizer.exe"; Tasks: desktopicon

[Registry]
; (Auto-start is handled by Task Scheduler, not registry, see [Run] section below)

[Run]
; Launch after install - use ShellExec so the UAC elevation prompt appears correctly
Filename: "{app}\ACEOptimizer.exe"; Description: "{cm:LaunchProgram,ACE Optimizer}"; Flags: nowait postinstall skipifsilent shellexec

; Create scheduled task for optional auto-start at logon with highest privileges
Filename: "powershell.exe"; Parameters: "-NonInteractive -NoProfile -Command ""$a=New-ScheduledTaskAction -Execute '{app}\ACEOptimizer.exe'; $t=New-ScheduledTaskTrigger -AtLogon; Register-ScheduledTask -TaskName 'ACEOptimizer' -Action $a -Trigger $t -RunLevel Highest -Force"""; Flags: runhidden; Tasks: startupicon

[UninstallRun]
Filename: "taskkill"; Parameters: "/F /IM ACEOptimizer.exe"; Flags: runhidden skipifdoesntexist
Filename: "powershell.exe"; Parameters: "-NonInteractive -NoProfile -Command ""Unregister-ScheduledTask -TaskName 'ACEOptimizer' -Confirm:$false -ErrorAction SilentlyContinue"""; Flags: runhidden skipifdoesntexist

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
