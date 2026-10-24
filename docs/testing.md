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

## Phase 4: Reminders

Tip: to test without waiting a day, add a deadline due a few minutes after one of the reminder times. For example, with the default "1 hour before" reminder, add one due 1 hour 2 minutes from now.

| # | Test | Expected |
|---|---|---|
| 4.1 | Right-click the bar → **FocusBar settings** → Reminders → **Send a test reminder** | A Windows notification "Test reminder is due in 1 hour" from FocusBar appears. If not, check Windows notification settings and Do Not Disturb / Focus Assist. |
| 4.2 | Add a deadline due 1 hour 2 minutes from now; wait 2 minutes | Notification: "*Title* is due in 1 hour", class and due time underneath, with **Snooze 15 min** and **Mark complete** buttons. |
| 4.3 | Click **Snooze 15 min** | Notification goes away and comes back 15 minutes later ("…is due in 45 minutes"). |
| 4.4 | On a reminder, click **Mark complete** | The deadline leaves the ticker and moves to Completed in the deadline list. |
| 4.5 | Click the body of a reminder (not a button) | The deadline list opens. |
| 4.6 | Add a deadline due in 2 hours | No reminder pops up right away (its "1 day" and "3 hours" reminders were already past when you added it). The "1 hour" reminder comes on time. |
| 4.7 | After a reminder shows, exit FocusBar and start it again | The same reminder does not show again. |
| 4.8 | Add a deadline due in 3 h 30 min. Exit FocusBar for 3 hours (or put the PC to sleep), then start it | Exactly one catch-up reminder ("…is due in 30 minutes", noting FocusBar was off), not one for each missed time. |
| 4.9 | Settings → Reminders: untick "3 hours before", tick "30 minutes before" | New choices take effect for upcoming reminders and are kept after a restart (`settings.json`). |
| 4.10 | Untick **Show reminders** | No reminders appear. Turning it back on doesn't flood you with the ones you missed. |
| 4.11 | Change a deadline's due date after one of its reminders showed | Reminders are set up again for the new due date. |
| 4.12 | Mark a deadline complete in FocusBar while its reminder is still in the notification center | The reminder disappears from the notification center. |
| 4.13 | Snooze a reminder, then exit FocusBar before 15 minutes pass, and start it again after | The snoozed reminder still comes back (shown once). |
| 4.14 | With FocusBar closed, click **Snooze** or **Mark complete** on a reminder still in the notification center | FocusBar starts and carries out the button's action. |
| 4.15 | Repeat the Phase 0 tests | All still pass. |

## Phase 5: Deadline sources

Everything here is under right-click the bar → **FocusBar settings** → **Deadline sources**.

**Demo data (anyone, including judges, can do this)**

| # | Test | Expected |
|---|---|---|
| 5.1 | Click **Use demo data** | Within a few seconds about 7 made-up assignments (Physics, English 10, Biology...) appear on the ticker; one is overdue. Status says "Showing demo data". |
| 5.2 | Wait a moment, then look at the left of the ticker on a weekday during school hours | A class chip like "Now: Biology (204) · until 8:50 AM" or "Next: Algebra II · 8:55 AM". |
| 5.3 | Click a demo deadline on the ticker | Details say "From your school"; there's **Mark complete** but no **Edit**. |
| 5.4 | In the deadline list, hover the pencil and trash can on a demo row | Both are disabled, with a tooltip explaining they come from the school. ✓ (complete) still works. |
| 5.5 | Add your own deadline with the same title and due time as a demo one (e.g. "Lab report: Enzyme activity", same date, 11:59 PM) | It shows only once, as yours (your copy wins). Mark it complete: the demo copy doesn't reappear. |
| 5.6 | Click **Disconnect** and confirm | Demo deadlines and the class chip disappear. Your own deadlines stay. |

**Real school account (Cranbrook)**

| # | Test | Expected |
|---|---|---|
| 5.7 | Check the school site box says `cranbrook.myschoolapp.com`, click **Sign in…** | A window shows the real school sign-in page. |
| 5.8 | Sign in as you normally do | The window closes by itself within a few seconds of reaching the site. Status: "Connected as *your name* to cranbrook.myschoolapp.com. Synced just now." Your real assignments appear on the ticker with class names; the class chip shows today's schedule. |
| 5.9 | If the window doesn't close after signing in | Click **I'm signed in**. If it still says not signed in, use test 5.10 instead and note it in the AI log. |
| 5.10 | **Paste session cookie…**: follow the on-screen steps to copy your browser's cookie and paste it | Connects the same as 5.8. Pasting junk says it doesn't look like a cookie; an old cookie says it didn't work. |
| 5.11 | Compare with the Assignment Center on the website | Same assignments and due times, last 7 days through the next 60. Completed/graded ones show as completed in FocusBar. |
| 5.12 | Complete an assignment on the website, then click **Sync now** | It moves to Completed in FocusBar. |
| 5.13 | Leave FocusBar running for a day (or restart it the next day) | Still connected (the site's rotating session is saved after each sync). If the sign-in expires, the bar shows a yellow ⚠ icon. Hovering it explains; clicking opens this page with "Sign-in expired". |
| 5.14 | Open `%AppData%\FocusBar\school-session.json` in Notepad | Only scrambled encrypted text; no readable cookie or name. |
| 5.15 | Click **Disconnect** | School deadlines and schedule are removed; signing in again starts fresh. |

**Calendars**

| # | Test | Expected |
|---|---|---|
| 5.16 | Add a calendar: name "Sample", **Browse…** to `docs/samples/sample-assignments.ics` | Within seconds: Chemistry worksheet 5, Book report, Geometry quiz, and Turn in field trip form appear (all November 2026). The weekly Robotics club meeting does not. |
| 5.17 | Add a calendar link (for example a Google Calendar "secret address in iCal format", or a Google Classroom class calendar) | Its upcoming events appear as deadlines. The list on this page shows only "Link on calendar.google.com", not the full private link. |
| 5.18 | Add the same calendar twice | "That calendar is already added." |
| 5.19 | Rename/move the sample .ics file, then **Sync now** | ⚠ on the bar; the calendar's row says the file can't be found. Its deadlines stay until it's fixed or removed. |
| 5.20 | Remove a calendar | Its deadlines disappear. |
| 5.21 | Turn off Wi-Fi, click **Sync now** | ⚠ icon with "Couldn't connect"; deadlines stay. Turn Wi-Fi back on and sync: ⚠ goes away. |
| 5.22 | Repeat the Phase 0 tests | All still pass. |

## Phase 6: Focus Mode

Set up first: right-click the bar → **FocusBar settings** → **Focus Mode**. Add an app you can open and close easily
(Notepad works: **Choose an app…**, open Notepad first and pick it under "Open now", or type `notepad`), and add
`example.com` as a website. Write down how your hosts file looks before you start: open
`C:\Windows\System32\drivers\etc\hosts` in Notepad (it opens read-only without admin rights, which is fine).

**Settings**

| # | Test | Expected |
|---|---|---|
| 6.1 | Type `https://www.YouTube.com/watch?v=1` under websites and click **Add** | Added as `youtube.com`. Adding it again says it's already on the list. `localhost`, `1.2.3.4`, and `not a site` are refused with a message. |
| 6.2 | Type `explorer` (or `FocusBar`, `taskmgr`) under apps | Refused: "part of Windows or FocusBar and can't be blocked". |
| 6.3 | **Choose an app…**, search for a Start Menu app (e.g. "Paint"), pick it | Its program name (e.g. `mspaint`) is added. A Store app that FocusBar can't resolve says to open it and pick it under "Open now". |
| 6.4 | Click a **Quick add** chip | The site is added and its chip disappears. Removing the site brings the chip back. |
| 6.5 | Pick **Gentle** or **Strict** | The "wait before override" chips grey out (they only apply to Soft). |
| 6.6 | Close and reopen Settings | Everything you chose is still there. |

**Soft (the default)**

| # | Test | Expected |
|---|---|---|
| 6.7 | Click the moon on the bar → **25 min** | A toast says Focus Mode is on. Windows asks for permission (UAC) **once**. The moon turns accent-colored and shows "25m". |
| 6.8 | Say **Yes**, then open the hosts file again | Your original lines are untouched. At the end there's a block between `# FocusBar BEGIN …` and `# FocusBar END` with `0.0.0.0` and `::` lines for example.com and www.example.com. `hosts.focusbar-backup` exists next to it. |
| 6.9 | Visit `example.com` in a browser (restart the browser if a tab was already open) | It doesn't load. |
| 6.10 | Open Notepad | Within about 2 seconds Notepad closes and the full-screen Focus screen appears with "Time to focus", the countdown, and **Override in 60 s** (greyed out). |
| 6.11 | Press **Esc** or **Back to work** | The screen closes. |
| 6.12 | Open Notepad again and wait for the override | After the delay **Override** unlocks. Click it, open Notepad: it stays open for the rest of the session. |
| 6.13 | Moon → **End focus** | Windows asks for permission once. Say Yes: the FocusBar block is gone from the hosts file and every other line is exactly as before. `example.com` loads again. |

**Gentle**

| # | Test | Expected |
|---|---|---|
| 6.14 | Choose Gentle, start a 25-min session | **No** permission prompt (Gentle doesn't block websites); the moon flyout says so. |
| 6.15 | Open Notepad | It stays open; the Focus screen asks "Are you sure?". **Back to work** closes Notepad; **Continue anyway** keeps it open for the rest of the session. |

**Strict**

| # | Test | Expected |
|---|---|---|
| 6.16 | Choose Strict, start a 25-min session | Moon flyout has no **End focus**, only a note that it stays on until the session ends. The Focus Mode settings page is greyed out with a banner. |
| 6.17 | Open Notepad | Closed; the Focus screen has no override, just the unlock note. |
| 6.18 | Add a deadline due in 1 hour with Strict and "Turn on by itself" (1 hr or more) on, while no manual session is running | Focus Mode turns on by itself for that deadline. Moon flyout → **I finished it: mark complete** ends it, and the website block is removed (one prompt). |

**Auto, cleanup, and "never without a way out"**

| # | Test | Expected |
|---|---|---|
| 6.19 | Soft, auto on (2 hr), add a deadline due in 90 minutes | Focus Mode turns on within ~15 seconds. **End focus** ends it and it doesn't turn back on for that deadline. |
| 6.20 | Start a session, say **No** to the permission prompt | Apps are still blocked. The moon flyout says websites aren't blocked and shows **Try again**. FocusBar doesn't keep re-asking. |
| 6.21 | Start a session (say Yes), then right-click the bar → **Restore Windows taskbar and exit** | The Windows taskbar comes back first, **then** one permission prompt removes the website block. |
| 6.22 | Start a session (say Yes), then end FocusBar.exe in Task Manager | The watchdog brings the taskbar back and asks to remove the website block. |
| 6.23 | Start a session, say **No** at exit, then start FocusBar again | On startup it asks once to remove the leftover block (Focus Mode isn't on). |
| 6.24 | Something went wrong and sites stay blocked | Run `FocusBar.Blocker.exe clear` from FocusBar's folder (asks for permission), or `FocusBar.exe --restore-taskbar`. Both remove only FocusBar's block. |
| 6.25 | Check `%ProgramData%\FocusBar\blocker.log` | One line each time sites were blocked or unblocked (how many, not which). |
| 6.26 | Repeat the Phase 0 tests | All still pass. |

## Phase 7: Customization

Everything here is under right-click the bar → **FocusBar settings** → **Appearance**. Have three pictures ready: a wide
one (a landscape photo or a banner), a square one (a logo), and any photo taken with a phone (to check rotation).

| # | Test | Expected |
|---|---|---|
| 7.1 | Under Themes, click **Use** on **Midnight**, then **Sunrise**, then **Default** | The bar changes at once each time: Midnight is dark purple with a purple accent and you can faintly see the wallpaper through it; Sunrise is light cream with dark text, an orange accent, and slightly shorter; Default follows Windows' light/dark mode. Accent-colored things (Start tiles, Focus button when on, buttons in flyouts and Settings) follow. |
| 7.2 | Pictures → Bar background → **Choose…** the wide picture | It appears behind the bar with a slight dark tint; all text and icons are white and readable. The thumbnail shows in Settings. |
| 7.3 | Try **Fill**, **Fit**, and **Center** | Fill covers the whole bar (top and bottom cropped). Fit shows the whole picture with plain bar color at the sides. Center shows it at real size in the middle. |
| 7.4 | Choose a picture that's very light (mostly white) | Text is still readable (the tint is there). |
| 7.5 | Choose the phone photo | It's the right way up (not sideways). |
| 7.6 | Start button → **Choose…** the square picture | It replaces the four tiles; Start still opens the launcher. **Remove** brings the tiles back. |
| 7.7 | Focus Mode screen → **Choose…** a picture, then **Preview** | Full-screen preview with your picture (darkened a little) and readable text. Esc closes it. Then start a Focus session and open a blocked app: same picture. |
| 7.8 | Choose a file that isn't a picture (rename a .txt to .png), and a picture over 25 MB if you have one | A clear message; nothing changes. |
| 7.9 | Move or delete the original picture files, then restart FocusBar | The pictures are still there (FocusBar uses its own copies in `%AppData%\FocusBar\images`). |
| 7.10 | Accent color: click a few swatches; type `#ff00aa` and press Enter; type `banana` | Changes show at once; `banana` shows "Type a color code like #4F8EF7." |
| 7.11 | Bar color: pick **Plum**, then the light **Sky**, then **Match Windows** | Text switches between white (dark colors) and black (light colors) so it's always readable. Match Windows follows light/dark mode again (try switching Windows' mode in Settings → Personalization → Colors). |
| 7.12 | Drag **Opacity** from 100% to 70% | The bar flickers once as it reopens see-through, then changes smoothly as you drag. The wallpaper shows through. Back to 100%: it reopens solid. |
| 7.13 | Drag **Bar height** to 32, let go; then 72 | The bar changes height when you let go; maximized windows move to stay above it. At 32 the clock shows just the time. Window icons, tray, and ticker still fit. Arrow keys on the slider also work. |
| 7.14 | Search fonts for "Comic" (or "Bahnschrift"), pick one | The bar's clock, window titles, and ticker switch font. |
| 7.15 | Type a name like "Space" and **Save** | A "Space" card appears and is marked In use. Change something: the status says "with your changes" and the card button says **Undo changes** (which goes back to the saved version). |
| 7.16 | Save as "Default" or "midnight" | "is a built-in theme. Pick another name." Saving as "Space" again asks before replacing. |
| 7.17 | **Delete** a saved theme | Asks first. The bar keeps its current look; built-in themes have no Delete. |
| 7.18 | Set up a custom theme (picture, colors, height, font), then exit FocusBar and start it again | Everything comes back exactly the same. |
| 7.19 | **Scaling:** with a background picture and Start picture set, change Windows Settings → System → Display → Scale to **100%**, **125%**, and **150%** (no restart needed) | At each level the bar keeps its proportions, the pictures stay sharp (not blurry or jagged), text is crisp, and the Fill crop looks the same. If anything looks wrong, take a screenshot and note it in the AI log. |
| 7.20 | Watch Task Manager for a minute with the ticker scrolling, at 100% opacity and then at 70% | CPU stays under about 2% at 100%. Note what it is at 70% (see-through windows cost a little more). |
| 7.21 | **Reset to the Default theme** | Back to the original look; your saved themes are still listed. |
| 7.22 | Repeat the Phase 0 tests | All still pass. |
