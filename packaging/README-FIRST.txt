FocusBar - test build
=====================

FocusBar replaces the Windows taskbar with one made for students: a scrolling ticker of
deadlines, reminders, Focus Mode (blocks distracting apps and websites near a deadline),
and your own pictures and colors.

This is a test build. It has not been tested much on real PCs yet, so expect rough edges.
Please note anything odd in docs/testing.md (in the GitHub repo) or tell the developer.


INSTALL OR UPDATE
-----------------
1. Extract the zip (right-click it, "Extract All..."). Anywhere is fine, like Downloads.
   Don't run it from inside the zip.
2. Double-click FocusBar.exe in the extracted folder.
   Windows may say "Windows protected your PC" because this test build isn't signed.
   Click "More info", then "Run anyway".
3. FocusBar asks to install (the first time) or update (every time after that). Click Yes.
   It closes the FocusBar that's running, puts the new version in
   %LocalAppData%\Programs\FocusBar, and starts it. There's always just one FocusBar,
   with a Start menu shortcut, and your deadlines and settings stay as they are.
4. You can then delete the folder you extracted (and the zip).

The first time, the Windows taskbar hides, FocusBar takes its place, and a setup guide opens.
Choose "No" in step 3 if you'd rather just run FocusBar from the extracted folder.

Nothing needs to be installed: .NET is included. The school sign-in window uses Microsoft
Edge WebView2, which comes with Windows 11 and most Windows 10 PCs.


GET THE NORMAL TASKBAR BACK
---------------------------
- Press Ctrl+Alt+Shift+F (works any time), or
- right-click FocusBar and choose "Restore Windows taskbar and exit".

FocusBar also gives the taskbar back when it closes, crashes, or when Windows signs out.
If the taskbar is ever still missing:
- start FocusBar again (it fixes things on startup, then exit it normally), or
- Start menu > "Restore Windows taskbar (FocusBar)", or
- press Ctrl+Shift+Esc, find "Windows Explorer", and choose Restart.


WEBSITE BLOCKING (FOCUS MODE)
-----------------------------
Blocking websites edits the Windows "hosts" file, so Windows asks for permission when Focus
Mode starts and ends. FocusBar only touches its own lines and always removes them. If sites
ever stay blocked: start FocusBar again, or run  FocusBar.Blocker.exe clear  from
%LocalAppData%\Programs\FocusBar.


REMOVE IT
---------
1. If you turned on "Start with Windows", turn it off first (Settings > General).
2. Exit FocusBar (Ctrl+Alt+Shift+F).
3. Delete %LocalAppData%\Programs\FocusBar and the "FocusBar" and "Restore Windows taskbar (FocusBar)"
   shortcuts in the Start menu (right-click > Open file location).
4. Optional: delete your data folder, %AppData%\FocusBar (deadlines, settings, pictures).


PRIVACY
-------
Everything stays on your PC: no accounts, no ads, no tracking. Your school sign-in and
private calendar links are encrypted for your Windows account.
