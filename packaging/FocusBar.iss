; FocusBar installer (Inno Setup 6, https://jrsoftware.org/isinfo.php).
;
; Build it after publishing the app (see "Building the zip and installer" in README.md):
;   dotnet publish src\FocusBar.App -c Release -r win-x64 --self-contained -o publish
;   iscc packaging\FocusBar.iss /DAppVersion=0.9.2
; The installer lands in dist\. GitHub Actions does both steps on every push.
;
; What it does:
;  - Installs for all users (Program Files, asks for admin once) or, if you choose, just for you (no admin).
;    Program Files is the safer choice: the website blocker runs with admin rights, and only admins can change it there.
;  - Adds a Start Menu shortcut, plus "Restore Windows taskbar (FocusBar)" as an emergency way back.
;  - Never sets FocusBar to start with Windows; that's a choice in FocusBar's own settings.
;  - Uninstalling closes FocusBar (which gives the Windows taskbar back), removes the website block if one is left,
;    removes the "Start with Windows" entry, and asks before deleting your deadlines, settings, and pictures.

#ifndef AppVersion
  #define AppVersion "0.9.2"
#endif
#ifndef PublishDir
  #define PublishDir "..\publish"
#endif

[Setup]
AppId={{8F4C2B71-3E5D-4A8B-9C1F-6D2E7A9B0C14}
AppName=FocusBar
AppVersion={#AppVersion}
AppVerName=FocusBar {#AppVersion}
AppPublisher=Suhaas (Congressional App Challenge 2026)
AppComments=A Windows taskbar for students: deadlines, reminders, and Focus Mode.
DefaultDirName={autopf}\FocusBar
DefaultGroupName=FocusBar
DisableProgramGroupPage=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\dist
OutputBaseFilename=FocusBar-Setup-{#AppVersion}
SetupIconFile=..\src\FocusBar.App\Assets\FocusBar.ico
UninstallDisplayIcon={app}\FocusBar.exe
UninstallDisplayName=FocusBar
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Closes a running FocusBar before installing over it or uninstalling. FocusBar treats that like Windows signing out
; and gives the taskbar back first.
CloseApplications=yes
RestartApplications=no
; The uninstaller tidies up the current user's FocusBar settings (see [Code]).
UsedUserAreasWarning=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\FocusBar"; Filename: "{app}\FocusBar.exe"; Comment: "Start FocusBar"
Name: "{autoprograms}\Restore Windows taskbar (FocusBar)"; Filename: "{app}\FocusBar.exe"; Parameters: "--restore-taskbar"; \
    Comment: "Brings the normal Windows taskbar back if it ever goes missing"
Name: "{autodesktop}\FocusBar"; Filename: "{app}\FocusBar.exe"; Tasks: desktopicon

[Run]
; As the signed-in user, never as administrator (FocusBar itself never runs elevated).
Filename: "{app}\FocusBar.exe"; Description: "Start FocusBar now"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  ApprovedKey = 'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run';

{ True if the hosts file still has FocusBar's website block (for example after a crash). }
function HostsHasFocusBarBlock(): Boolean;
var
  Lines: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if LoadStringsFromFile(ExpandConstant('{sys}\drivers\etc\hosts'), Lines) then
    for I := 0 to GetArrayLength(Lines) - 1 do
      if Pos('# FocusBar BEGIN', Lines[I]) = 1 then
      begin
        Result := True;
        Exit;
      end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    { Before the files go: lift a leftover website block with FocusBar's own blocker (Windows may ask permission). }
    if HostsHasFocusBarBlock() and FileExists(ExpandConstant('{app}\FocusBar.Blocker.exe')) then
      ShellExec('runas', ExpandConstant('{app}\FocusBar.Blocker.exe'), 'clear', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    { "Start with Windows", if it was turned on. }
    RegDeleteValue(HKEY_CURRENT_USER, RunKey, 'FocusBar');
    RegDeleteValue(HKEY_CURRENT_USER, ApprovedKey, 'FocusBar');
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    if DirExists(ExpandConstant('{userappdata}\FocusBar')) and not UninstallSilent() then
      if MsgBox('Also delete your FocusBar data (deadlines, settings, school sign-in, and pictures)?' #13#10#13#10 +
                'Choose No to keep it for next time.', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      begin
        DelTree(ExpandConstant('{userappdata}\FocusBar'), True, True, True);
        DelTree(ExpandConstant('{commonappdata}\FocusBar'), True, True, True);
      end;
  end;
end;
