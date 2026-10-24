# Testing on Windows

Run these checks on a real Windows PC after each phase. Use a **Debug** build so the right-click menu has the **Developer** submenu.

Before you start, it helps to know where things are:
- **Log files:** `%AppData%\FocusBar\logs\` (right-click the bar → **Open log folder**)
- **Crash-recovery file:** `%AppData%\FocusBar\taskbar-state.json`. It exists only while FocusBar has the taskbar hidden.
- **If you get stuck without a taskbar:** see "If Your Taskbar Disappears" in the README.

Write down your Windows version (Settings → System → About) and display scaling for each test run.

---

## Phase 0: Safety net

Before each test, start FocusBar and check that the Windows taskbar is hidden and FocusBar's bar is showing.

| # | Test | Expected |
|---|---|---|
| 0.1 | Right-click the bar → **Restore Windows taskbar and exit** | Bar disappears, Windows taskbar is back and works. `taskbar-state.json` is gone. |
| 0.2 | Press **Ctrl+Alt+Shift+F** | Same as 0.1. |
| 0.3 | Right-click → **Developer → Throw test exception (UI thread)** | A message says FocusBar hit a problem and the taskbar is back. Choose **No**: FocusBar exits and the taskbar works. The log has the exception. |
| 0.4 | Same as 0.3, but choose **Yes** | FocusBar restarts after a moment and hides the taskbar again. |
| 0.5 | Right-click → **Developer → Throw test exception (background thread)** | FocusBar closes and the taskbar comes back (from the crash handler or the watchdog). The log has the exception. |
| 0.6 | Open Task Manager's **Details** tab. Right-click a column header → **Select columns** → check **Command line**. There are two `FocusBar.exe` entries; the watchdog's command line ends in `--watchdog <number>`. End the *other* one (the bar). | Within a second the Windows taskbar is back. The watchdog exits too. |
| 0.7 | Task Manager → right-click FocusBar → **End process tree** (kills the bar and watchdog) | Taskbar may stay hidden. Start FocusBar again: it restores the taskbar first (check the log for "exited without restoring the taskbar"), then takes over normally. |
| 0.8 | After 0.7, instead of relaunching: run `FocusBar.exe --restore-taskbar` | Message says the taskbar was restored, and it is. |
| 0.9 | Turn **on** "Automatically hide the taskbar" in Windows settings. Start FocusBar, then exit it. | Windows taskbar comes back still set to auto-hide (your setting was kept). Repeat 0.6 with auto-hide on: still kept. Turn it back off afterward. |
| 0.10 | Start FocusBar, then start it a second time | A message says FocusBar is already running. Only one bar. |
| 0.11 | Sign out of Windows (or restart) while FocusBar is running | After signing back in, the normal Windows taskbar is there and not set to auto-hide (unless it was before). |
| 0.12 | Start another app that uses Ctrl+Alt+Shift+F first, then FocusBar | FocusBar still starts. The log warns that the hotkey couldn't be registered. The right-click menu still works. |

## Phase 1: The bar

| # | Test | Expected |
|---|---|---|
| 1.1 | Start FocusBar | Bar is docked at the bottom of the primary monitor, full width, 48 px tall at 100% scaling. Windows taskbar is hidden. |
| 1.2 | Maximize a window (for example File Explorer or a browser) | The window stops right above the bar and doesn't cover it. |
| 1.3 | Layout | Left to right: Start icon, "Pinned apps", "Open windows", deadline area filling the middle, "Tray", clock with time and date. |
| 1.4 | Clock | Shows the right time and date in your region's format, and changes at the start of each minute. |
| 1.5 | Display scaling at 125%, then 150% (Settings → System → Display → Scale), with FocusBar running and after restarting it | Bar stays docked, text is sharp, and the bar gets taller in proportion. Maximized windows still don't overlap it. |
| 1.6 | Restart Explorer (Task Manager → Windows Explorer → Restart) while FocusBar is running | Windows taskbar flickers back for a moment, then hides again. FocusBar's bar is still docked and maximized windows still stop above it. |
| 1.7 | Play a full-screen video or game | The bar gets out of the way of the full-screen app, and comes back when you leave full screen. |
| 1.8 | Leave FocusBar running for 10 minutes and look at Task Manager | CPU near 0%. Memory well under 150 MB for the bar process. |
| 1.9 | Repeat all Phase 0 tests | All still pass. |

Known Phase 1 limits (expected, not bugs): the Start button does nothing yet, and other apps' tray icons aren't reachable while FocusBar runs. Both arrive in Phase 2.
