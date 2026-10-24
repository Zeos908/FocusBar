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

## Phase 2: Taskbar features

Goal: you can do a normal day's work using only FocusBar. Try it in both light and dark mode (Settings → Personalization → Colors → "Choose your mode").

**Start launcher**

| # | Test | Expected |
|---|---|---|
| 2.1 | Click the Start button (four tiles) | A panel opens above it with a search box (already focused) and a list of installed apps with icons. |
| 2.2 | Type `chr` (or part of any app's name) | List narrows; best match is highlighted at the top. Press **Enter**: that app opens and the panel closes. |
| 2.3 | Type a name, use **Up/Down** to pick another match, press **Enter** | The picked app opens. |
| 2.4 | Click an app in the list | It opens. |
| 2.5 | Type nonsense (`zzzz`) | "No apps match" message. |
| 2.6 | Press **Esc**; open again and click on the desktop; open again and click Start again | Panel closes each time (clicking Start while it's open closes it, it doesn't reopen). |
| 2.7 | Click **Windows Start menu** | The normal Windows Start menu opens. |
| 2.8 | Right-click Start | Windows' power-user menu (same as Win+X) opens. |
| 2.9 | Install or add a shortcut to the Start Menu, then reopen the launcher | New app appears within a second or two. |

**Pinned apps**

| # | Test | Expected |
|---|---|---|
| 2.10 | In the launcher, right-click an app → **Pin to FocusBar** | Its icon appears next to Start; the launcher row shows a pin mark. |
| 2.11 | Open an app that isn't pinned, right-click its button in the open-windows area → **Pin to FocusBar** | Its icon appears in the pinned area. |
| 2.12 | Pin Chrome (or any app) from the launcher, then right-click Chrome's open window | Menu says **Unpin from FocusBar** (it knows it's already pinned). |
| 2.13 | Click a pinned icon | The app opens. |
| 2.14 | Right-click a pinned icon → **Move left / Move right**, then **Unpin from FocusBar** | Order changes; unpin removes it. |
| 2.15 | Exit FocusBar and start it again | Pins and their order are kept (`%AppData%\FocusBar\pins.json`). |
| 2.16 | Pin an app, uninstall it (or delete its shortcut), click the pin | Message says it couldn't be opened and offers to unpin it. |

**Open windows**

| # | Test | Expected |
|---|---|---|
| 2.17 | Open a few apps | Each window gets a button with its icon; hover shows the window title. The active window has a wide accent line under it. |
| 2.18 | Click a background window's button | That window comes to the front. |
| 2.19 | Click the active window's button | It minimizes. Click again: it comes back. |
| 2.20 | Right-click a window button → **Close window**; middle-click another | Both windows close (apps may ask to save first). |
| 2.21 | Open 20+ windows (for example 20 Notepad windows) | Buttons shrink to fit; the deadline area keeps some room; nothing overlaps the tray. |
| 2.22 | Start something that flashes for attention (for example a finished download in some apps, or a message in Discord) | Its line turns orange until you switch to it. |

**Tray**

| # | Test | Expected |
|---|---|---|
| 2.23 | Look at the right side of the bar | `^`, any pinned tray icons, then Wi-Fi (or Ethernet), volume, and battery (laptops only). Hovering each shows a tooltip like "HomeNet: Internet access", "Volume: 40%", "Battery: 76% (3 hr 10 min left)". |
| 2.24 | Click `^` | A list of every app's tray icon with names (Discord, Steam, OneDrive, antivirus...). |
| 2.25 | In that list, left-click and right-click an app's row | The app reacts exactly as it would on the normal taskbar (opens its window or menu, near the bar). |
| 2.26 | Click the pin button on a row | That icon now shows on the bar. Right-click it there: the app's own menu. Restart FocusBar: it's still pinned (`tray.json`). |
| 2.27 | Click Wi-Fi / volume / battery | Opens the matching Windows Settings page (Wi-Fi, Sound, Battery/Power). |
| 2.28 | Scroll the mouse wheel over the volume icon; middle-click it; press the keyboard volume keys | Volume changes 2% per notch; middle-click mutes/unmutes; the icon and tooltip follow all of these. |
| 2.29 | Plug in headphones or switch the output device | Volume icon reflects the new device. |
| 2.30 | Turn Wi-Fi off and on; on a laptop, unplug and plug in power | Icons change within a few seconds (battery may take up to a minute). |

**Clock and calendar**

| # | Test | Expected |
|---|---|---|
| 2.31 | Click the clock | Panel with today's full date, the time, and this month with today highlighted. Weeks start on the day your region uses. |
| 2.32 | Use the arrow buttons or **Up/Down** keys; click the month name | Changes month; clicking the month name goes back to this month. |
| 2.33 | Click **Date & time settings** | Windows Settings opens to Date & time. |

**General**

| # | Test | Expected |
|---|---|---|
| 2.34 | Switch Windows between light and dark mode while FocusBar runs | Bar and panels switch colors right away. |
| 2.35 | Click the bar, then press **Tab** repeatedly and **Enter** | Focus ring moves through Start, pins, windows, tray buttons, and clock; Enter activates. |
| 2.36 | Kill FocusBar from Task Manager (like test 0.6) | Taskbar comes back **and** other apps' tray icons reappear on it within a few seconds. |
| 2.37 | Exit FocusBar normally | Taskbar comes back with all tray icons. |
| 2.38 | Leave it running for 10 minutes | CPU near 0%; memory well under 150 MB. |
| 2.39 | Repeat all Phase 0 and Phase 1 tests | All still pass. |

Known Phase 2 limits (expected, not bugs):
- The launcher lists apps that have Start Menu shortcuts. Microsoft Store apps without one (such as Calculator or Settings) are reached through the **Windows Start menu** button.
- Old-style tray "balloon" pop-ups from apps aren't shown. Modern Windows notifications (toasts) work normally.
- Multiple windows from the same app each get their own button (not grouped).

## Phase 3: Manual deadlines and the ticker

**Adding deadlines**

| # | Test | Expected |
|---|---|---|
| 3.1 | With no deadlines, look at the middle of the bar | "No upcoming deadlines · click to add one". Clicking it opens the add form. |
| 3.2 | Click **+** on the bar | Form opens above it with the cursor in "What's due?". Due date is tomorrow, time is 11:59 PM. |
| 3.3 | Type `Bio lab`, class `Biology`, press **Enter** | Form closes; "● Bio lab · Biology  1d …" appears on the bar. |
| 3.4 | Add another and pick the class from the chip under the Class box | Chip fills in the class. |
| 3.5 | In Due date try `fri`, `10/31`, `Oct 31`, `tomorrow`, `1/15` | The line under the box shows the full date each time (`1/15` means next January). |
| 3.6 | In Due date type `t`, or `xyz`, and press Enter | Red hint "Try a date like 10/31, Fri, or tomorrow."; nothing saved. |
| 3.7 | In Due time try `3pm`, `3:30 pm`, `15:30`, `noon`, `midnight` | All accepted (`midnight` = 11:59 PM). `25:00` shows a red hint on Enter. |
| 3.8 | Click the calendar icon next to the date and pick a day | Date box fills in and the calendar closes. |
| 3.9 | Set a date/time in the past | Note says it will show as overdue; it still saves and shows as a dark red "… overdue" pill. |
| 3.10 | Leave the title empty and press Enter | Red hint under the title; nothing saved. |
| 3.11 | In Notes, press Enter, then Ctrl+Enter | Enter adds a new line; Ctrl+Enter saves. |
| 3.12 | Open the form and press Esc, or click elsewhere | Form closes without saving. |

**Ticker**

| # | Test | Expected |
|---|---|---|
| 3.13 | Add deadlines due in 5 days, 2 days, 5 hours, and one overdue | Colors: green (more than 3 days), yellow (1–3 days), red (under 24 hours), dark red pill (overdue). Soonest/most overdue first. |
| 3.14 | Add enough deadlines that they don't fit | The ticker scrolls slowly and smoothly from right to left in a seamless loop, with faded edges. With only one or two short ones, it stays still. |
| 3.15 | Hover over the ticker | Scrolling pauses; moving away resumes it from the same spot. |
| 3.16 | Watch a deadline due in under an hour across a minute change | Countdown ticks down each minute ("45m" → "44m") without the ticker jumping. |
| 3.17 | Click a ticker entry | Details open: countdown, title, class, full due date and time, notes, "Added by you". |
| 3.18 | In details click **Mark complete** | Entry disappears from the ticker. |
| 3.19 | In details click **Edit**, change the title, press Enter | Ticker shows the new title. |
| 3.20 | Play a full-screen video or game for a minute, then check Task Manager | FocusBar uses ~0% CPU while hidden behind the full-screen app; ticker resumes after. |
| 3.21 | Turn off **Animation effects** (Windows 11: Settings → Accessibility → Visual effects; Windows 10: Settings → Ease of Access → Display → "Show animations in Windows") | Within a minute the ticker stops scrolling and shows the first deadlines with a fade on the right. Turning it back on resumes scrolling. |
| 3.22 | With the ticker scrolling, watch Task Manager for a few minutes | FocusBar's CPU stays under about 2%. |

**Deadline list**

| # | Test | Expected |
|---|---|---|
| 3.23 | Click the list button (next to +) | "FocusBar Deadlines" window opens with Upcoming (n) and Completed (n) tabs. Clicking the list button again brings the same window to the front. |
| 3.24 | Click ✓ on a row; switch to Completed; click undo on it | Moves to Completed, then back to Upcoming. The ticker updates each time. |
| 3.25 | Click the pencil on a row, change the due date, Save | Row and ticker update; order follows the new date. |
| 3.26 | Click the trash can on a row, answer Yes | Deleted from the list and the ticker. Answering No keeps it. |
| 3.27 | Press **Ctrl+N** in the list window | Add form opens as a small dialog. |
| 3.28 | Exit FocusBar and start it again | All deadlines (and which ones are complete) are still there (`%AppData%\FocusBar\deadlines.json`). |
| 3.29 | Check the list window and forms in both light and dark mode | Readable colors; the window's title bar is dark in dark mode. |
| 3.30 | Repeat the Phase 0 tests | All still pass. |

Known Phase 3 limits (expected, not bugs):
- The current/next class only appears once a class schedule exists, which comes from the school API sync in Phase 5.
- Reminders arrive in Phase 4.
