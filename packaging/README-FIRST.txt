FocusBar - test build
=====================

FocusBar replaces the Windows taskbar with one made for students: a scrolling ticker of
deadlines, reminders, Focus Mode (blocks distracting apps and websites near a deadline),
and your own pictures and colors.

This is a test build. It has not been tested much on real PCs yet, so expect rough edges.
Please note anything odd in docs/testing.md (in the GitHub repo) or tell the developer.


START IT
--------
1. Unzip this whole folder somewhere you can keep it (for example Documents\FocusBar).
   Don't run it from inside the zip.
2. Double-click FocusBar.exe.
   Windows may say "Windows protected your PC" because this test build isn't signed.
   Click "More info", then "Run anyway".
3. The Windows taskbar hides and FocusBar takes its place. A setup guide opens the first time.

Nothing needs to be installed: .NET is included. The school sign-in window uses Microsoft
Edge WebView2, which comes with Windows 11 and most Windows 10 PCs.


GET THE NORMAL TASKBAR BACK
---------------------------
- Press Ctrl+Alt+Shift+F (works any time), or
- right-click FocusBar and choose "Restore Windows taskbar and exit".

FocusBar also gives the taskbar back when it closes, crashes, or when Windows signs out.
If the taskbar is ever still missing:
- start FocusBar again (it fixes things on startup, then exit it normally), or
- run:  FocusBar.exe --restore-taskbar   (from this folder), or
- press Ctrl+Shift+Esc, find "Windows Explorer", and choose Restart.


WEBSITE BLOCKING (FOCUS MODE)
-----------------------------
Blocking websites edits the Windows "hosts" file, so Windows asks for permission when Focus
Mode starts and ends. FocusBar only touches its own lines and always removes them. If sites
ever stay blocked: start FocusBar again, or run  FocusBar.Blocker.exe clear  from this folder.


REMOVE IT
---------
1. Exit FocusBar (Ctrl+Alt+Shift+F).
2. If you turned on "Start with Windows", turn it off first (Settings > General).
3. Delete this folder.
4. Optional: delete your data folder, %AppData%\FocusBar (deadlines, settings, pictures).


PRIVACY
-------
Everything stays on your PC: no accounts, no ads, no tracking. Your school sign-in and
private calendar links are encrypted for your Windows account.
