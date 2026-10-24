# AI Log

The Congressional App Challenge requires full disclosure of AI use. This log records, phase by phase, what an AI assistant generated, what the developer (Suhaas) wrote, changed, or decided, and significant problems and how they were solved. Keep it accurate. It's the source for the AI usage disclosure and the technical-challenge answer.

**AI tool used:** Claude Code (Anthropic), working from `BUILD_INSTRUCTIONS.md` written by the developer.

---

## Planning (before Phase 0)

**Developer decided:**
- The app concept, features, audience, and README (`README.md` was written before any code).
- The build instructions: tech stack (C#/.NET 8/WPF, ManagedShell, MVVM, xUnit), project structure, data models, urgency thresholds, phase order, safety rules, and the school-API process.
- To keep FocusBar in its own GitHub repository, separate from other projects.

---

## Phase 0: Safety net + Phase 1: The bar

These two phases were built together, since the safety net needs a bar to hide the taskbar for, and the bar can't be tested safely without the safety net.

**Status:** Compiles (Debug and Release) and all 29 `FocusBar.Core` unit tests pass. **Not yet run on Windows.** The AI worked in a Linux environment that can compile a Windows app but can't run it, so every item in the Phase 0/1 test list in `docs/testing.md` still needs a real Windows test.

### What the AI generated
- Solution and project files: `FocusBar.Core` (no UI code), `FocusBar.App` (WPF), `FocusBar.Core.Tests` (xUnit).
- `FocusBar.Core`:
  - Data models from the build instructions (`Deadline`, `ClassPeriod`, `FocusSettings`, `Theme`, enums).
  - `UrgencyRules`: the urgency thresholds in one place.
  - `JsonFileStore`: saves JSON safely (write to a temp file, then swap) and sets aside a corrupt file instead of crashing.
  - `ErrorLog`: daily log files in `%AppData%\FocusBar\logs\`.
  - `TaskbarSafetyStore`: the "taskbar is hidden" record used for crash recovery.
- `FocusBar.App`:
  - `TaskbarGuard`, `TaskbarRestorer`: record the taskbar's original state before hiding it, and restore it on every exit path.
  - `Watchdog`: a second, window-less FocusBar process that restores the taskbar if the main one is killed.
  - `PanicHotkey`: Ctrl+Alt+Shift+F restores the taskbar and exits.
  - `Program`: single-instance check, crash handlers, and the `--restore-taskbar` command.
  - `MainBar`: the docked bar with the left-to-right layout (Start | pinned | windows | ticker | tray | clock), placeholders for later phases, a working clock, and the right-click menu.
- Unit tests for all of `FocusBar.Core` (29 tests).
- README sections: Tech Stack, Building and Running, If Your Taskbar Disappears. Also `THIRD-PARTY-NOTICES.md` and a CI workflow.

### What the developer wrote, changed, or decided
- Approved installing the .NET 8 SDK in the AI's environment so the code could be compiled and tested before being shared.
- *(Add anything you change after testing on Windows.)*

### Problems found and how they were solved
These are good candidates for the technical-challenge answer.

1. **A killed app can't clean up after itself.** The Windows taskbar is hidden by turning on its "auto-hide" setting and hiding its window. Windows saves that setting, so if FocusBar were killed from Task Manager, the taskbar would stay hidden (and set to auto-hide) even after a restart. A killed process runs no code, so the fix had to live outside it:
   - Before hiding anything, FocusBar writes `taskbar-state.json` with the taskbar's original setting and FocusBar's process ID and start time. The file is deleted only after a clean exit has restored the taskbar.
   - A small watchdog process waits for FocusBar to exit. If the file is still there, FocusBar didn't exit cleanly, so the watchdog restores the taskbar.
   - If the watchdog was killed too, the next launch of FocusBar sees the leftover file and restores the taskbar first.
   - The process start time is saved as well as the ID, because Windows reuses process IDs. Without it, a new, unrelated process could be mistaken for FocusBar.
2. **The restore code was finding FocusBar's own window.** ManagedShell creates its own hidden window with the same class name as the real taskbar (`Shell_TrayWnd`) so other apps send it their tray icons. The restore code skips windows owned by FocusBar's own process, so it always finds Explorer's real taskbar.
3. **The taskbar was re-hidden right after being restored.** ManagedShell re-hides the Windows taskbar every 100 ms while hiding is turned on. The crash handlers turn hiding off before restoring, or the restore would be undone within a tenth of a second.
4. **Don't overwrite the user's own auto-hide setting.** If FocusBar crashed before it had recorded the taskbar's original state, "restoring" would have applied Windows' default instead of the user's setting. The guard only restores once it has recorded the original state.
5. **Caught in review, before testing:**
   - A corrupt settings file was being moved while it was still open, which fails on Windows. It is now read fully and closed before being moved.
   - The panic hotkey's handler closed the app, which disposed the hidden window that was in the middle of handling the hotkey. The exit now runs right after that handler returns.
   - If another app cancelled a Windows sign-out, FocusBar would have restored the taskbar and then kept running with no bar. It now exits completely.
6. **Build issue:** WPF generates its own `Main` method from `App.xaml`, which conflicted with FocusBar's `Program.Main`. FocusBar needs its own `Main` because the watchdog and `--restore-taskbar` modes have to run without starting WPF. Fixed by compiling `App.xaml` as a regular page.

### Decisions the AI made that the developer should review
- **Unobserved task exceptions don't close FocusBar.** The build instructions list `TaskScheduler.UnobservedTaskException` as an exit path. In .NET 8 these don't end the process, so FocusBar logs them and keeps running instead of restoring the taskbar while the bar is still on screen.
- **After a crash, FocusBar shows a message offering to restart.** It doesn't restart automatically, and the watchdog never restarts FocusBar (no anti-tamper behavior).
- **Phase 1 leaves tray icons with Windows.** ManagedShell's tray service isn't started until Phase 2 adds a tray area to show icons in. Until then, other apps' tray icons aren't reachable while FocusBar runs.
- **The clock is real, but the calendar flyout isn't.** The time and date show now; the flyout is Phase 2.

---

## Phase 2: Taskbar features

**Status:** Compiles (Debug and Release, no warnings) and all 88 `FocusBar.Core` unit tests pass. The GitHub Actions build on a Windows runner passed for Phases 0–1. **Not yet run on Windows**: the Phase 2 checklist in `docs/testing.md` still needs a real test.

### What the developer wrote, changed, or decided
- Created the GitHub repository and asked to move on to Phase 2 without first testing Phases 0–1 on Windows.
- *(Add anything you change after testing on Windows.)*

### What the AI generated
- `FocusBar.Core` (all unit-tested):
  - `PinnedApp`, `PinnedAppList`, `PinStore`: pinned apps in order, no duplicates, saved to `pins.json`.
  - `StartMenuScanner`: finds installed apps from the Start Menu shortcut folders (yours and the all-users one).
  - `AppSearch`: ranks matches as you type (exact name, then starts-with, then word start, then initials like "vsc", then anywhere).
  - `StatusGlyphs`: picks the Wi-Fi, volume, and battery icons and tooltip text.
  - `MonthGrid`: the dates shown in the calendar, starting on your region's first day of the week.
  - `TraySettings`: which tray icons are shown on the bar (`tray.json`).
- `FocusBar.App`:
  - **Start launcher** (`Flyouts/StartLauncher`): search box, keyboard navigation, icons, right-click to pin, button for the real Windows Start menu. Right-clicking Start opens Windows' Win+X menu.
  - **Pinned apps** (`Shell/PinnedStrip`, `Services/PinService`): click to open, right-click to reorder or unpin; offers to unpin an app that can't be opened anymore.
  - **Open windows** (`Shell/TaskStrip`): uses ManagedShell's window list; click to switch, click the active window to minimize, right-click menu to pin or close, middle-click to close; active and "needs attention" indicators; buttons shrink when there are many windows (`Controls/ShrinkingStackPanel`) while the deadline ticker keeps at least 240 px (`Controls/BarMiddlePanel`).
  - **Tray** (`Tray/`): other apps' tray icons through ManagedShell (mouse clicks passed on to the app); a `^` list of all icons with pin buttons; FocusBar's own Wi-Fi, volume, and battery indicators that open Windows Settings pages. Volume reacts to the mouse wheel and middle-click and updates when changed elsewhere.
  - **Calendar** (`Flyouts/CalendarFlyout`): opens from the clock.
  - **Flyouts** (`Flyouts/FlyoutWindow`): shared behavior for the panels: open above their button, close on outside click or Esc, clicking the button again closes rather than reopens.
  - **Light and dark mode** (`Themes/`): follows the Windows setting and switches live.

### Problems found and how they were solved
1. **Tray icons vanish if FocusBar is killed.** To show other apps' tray icons, FocusBar has to pretend to be the Windows tray, so apps send their icons to FocusBar instead of Explorer. If FocusBar is killed, those icons are lost and don't come back to the Windows taskbar until each app restarts. Fix: when the watchdog (or the next launch, or `--restore-taskbar`) restores the taskbar after FocusBar died, it broadcasts the same "TaskbarCreated" message Explorer sends when it restarts, which tells every app to add its icon again.
2. **Telling a click from a minimize.** Clicking the bar makes the bar the active window, so by the time a click is processed, the window you clicked is never "active" anymore, and "click the active window to minimize" would never trigger. Fix: remember the window's state when the mouse button goes down, before the bar takes focus. (RetroBar handles it the same way.)
3. **Knowing an open app is already pinned.** A pin from the Start launcher is a shortcut (`Google Chrome.lnk`), but a running window only knows its program (`chrome.exe`). Pins now store the shortcut's target too, so "Pin" changes to "Unpin" on Chrome's window and the same app can't be pinned twice.
4. **Flyouts reopening instantly.** Clicking the Start button while the launcher is open closes it as the mouse goes down (the launcher loses focus), and then the click opened it again. Fix: ignore a reopen within 300 ms of closing.
5. **Icons missing on scrolled rows.** The app list reuses rows as you scroll, so icons loaded "when a row appears" were skipped for reused rows. Fix: each icon loads the first time anything displays it, in the background, and is cached.
6. **Search box needs keyboard focus.** WPF pop-ups only receive typing while the window they belong to is active, and the bar is a toolbar-style window. To make sure typing reaches the search box, the flyouts are small borderless windows that become active when opened.

### Decisions the AI made that the developer should review
- **"Right-click to close"** (from the build instructions) is a right-click menu with **Close window**, not closing on right-click directly, to avoid closing windows by accident. Middle-click closes directly, like Windows.
- **Windows' own network, volume, and battery tray icons are hidden**, since FocusBar draws its own.
- **New tray icons start hidden under `^`**, like Windows 11; you pin the ones you want on the bar.
- **Each window gets its own button**; windows from the same app aren't grouped.
- **Store apps** without Start Menu shortcuts aren't in the launcher (the instructions say to scan shortcut folders); the **Windows Start menu** button covers them.
- **Old-style tray balloon pop-ups** aren't shown yet.
- **Wi-Fi click opens Wi-Fi settings** (or network status when not on Wi-Fi), **battery opens Battery saver / Power & battery**, **volume opens Sound**. These are the `ms-settings:` pages the build instructions call for. Opening the Windows quick-settings panel instead is a possible change.

---

## Phase 3: Manual deadlines and the ticker

**Status:** Compiles (Debug and Release, no warnings) and all 169 `FocusBar.Core` unit tests pass. The GitHub Actions Windows build passed for Phase 2. **Not yet run on Windows**: see the Phase 3 checklist in `docs/testing.md`.

### What the developer wrote, changed, or decided
- Asked to keep building (Phase 3) before testing earlier phases on Windows.
- *(Add anything you change after testing on Windows.)*

### What the AI generated
- `FocusBar.Core` (all unit-tested):
  - `DeadlineList` / `DeadlineStore`: add, edit, complete, delete; soonest-first "upcoming" order; recent class names for quick picking; saved to `deadlines.json`.
  - `Countdown`: short ticker text ("2d 4h", "45m", "3h overdue") and longer wording for tooltips.
  - `DueInput`: understands typed dates ("fri", "10/31", "Oct 31", "tomorrow") and times ("3pm", "15:30", "noon", "midnight"), using the computer's region settings.
  - `ScheduleLookup` / `ScheduleStore`: the class happening now or next, from the weekly schedule (`schedule.json`, filled by the school sync in Phase 5).
- `FocusBar.App`:
  - **Ticker** (`Ticker/TickerView`): scrolls the deadlines as a seamless loop at 30 frames per second, with live countdowns and urgency colors. It only scrolls when the deadlines don't fit, pauses on hover, while details are open, and behind full-screen apps, and follows the Windows "Animation effects" setting.
  - **Details** (`Ticker/DeadlineDetailsFlyout`): opens when an entry is clicked, with Mark complete, Edit, and All deadlines.
  - **Add/edit form** (`Deadlines/DeadlineForm`): shown above the bar from **+** or in a dialog from the list. It has class chips, date chips (Today, Tomorrow, In a week), a pop-out calendar, time chips, a preview of the date it understood, and inline error hints.
  - **Deadline list** (`Deadlines/DeadlineListWindow`): Upcoming and Completed tabs; complete/undo, edit, delete; Ctrl+N to add; dark title bar in dark mode.
  - `Controls/MonthCalendar`: the calendar from Phase 2 turned into a reusable control, used by both the clock flyout and the date picker.

### Problems found and how they were solved
1. **Making the ticker loop seamlessly.** A plain slide-to-the-left leaves a gap and then a jump when it restarts. The ticker draws three copies of the deadlines side by side and slides by exactly one copy's width, then repeats. Because the content repeats, the end of one loop looks identical to the start of the next.
2. **The ticker jumped every minute.** Countdowns change every minute, which rebuilds the strip and restarted the animation from the beginning. Fix: read where the strip currently is and start the new loop from that same spot.
3. **Keeping CPU low.** The animation runs at 30 frames per second instead of WPF's default 60, only runs when the deadlines don't fit, and stops while the mouse is over it, while details are open, and while a full-screen app covers the bar.
4. **Overdue text was hard to read.** Dark red text on the dark bar had poor contrast, so overdue countdowns are white text on a dark red pill.
5. **Pop-ups closing the form.** The Delete confirmation took focus, which the form treated as "clicked elsewhere" and closed itself. Flyouts now stay open while their own confirmation box is showing.
6. **Typed dates.** "1/15" typed in October means next January, so a date with no year that has already passed is moved to next year. A weekday ("fri") means the next one after today, and the form always shows the full date it understood so mistakes are easy to spot. Region settings decide whether "3/11" is March 11 (US) or 3 November (UK).
7. **Test failure:** a schedule test failed because .NET's US time format puts a special narrow space before "AM". The test now builds its expected text with the same formatting instead of hard-coding it.

### Decisions the AI made that the developer should review
- **Default due time is 11:59 PM** (common for online assignments). "midnight" also means 11:59 PM on the due date, so that "Friday at midnight" isn't read as the very start of Friday.
- **A weekday name means the next one after today** ("fri" typed on a Friday means next Friday).
- **Overdue deadlines stay in the ticker** until they're marked complete or deleted.
- **Ticker speed is 40 pixels per second.** Easy to change (`Speed` in `TickerView.xaml.cs`).
- **Deleting asks for confirmation.** There's no undo for delete, but completing can be undone from the list.
- **The current/next class chip** only appears once a schedule exists (Phase 5). There's no way to type in a schedule by hand yet.

---

## Phase 4: Reminders

**Status:** Compiles (no warnings) and all 188 `FocusBar.Core` unit tests pass. The GitHub Actions Windows build passed for Phase 3. **Not yet run on Windows**: see the Phase 4 checklist in `docs/testing.md`.

### What the developer wrote, changed, or decided
- Asked to keep going, and provided the school website's API details for Phase 5.
- *(Add anything you change after testing on Windows.)*

### What the AI generated
- `FocusBar.Core` (unit-tested):
  - `ReminderPlanner`: decides which reminders to show now (rules below).
  - `ReminderState` / `ReminderStateStore`: which reminders were shown, when each deadline was first seen, and snoozes, saved in `reminders.json`.
  - `ReminderSettings` and `AppSettings` / `SettingsStore`: reminder choices in `settings.json`, with missing parts filled in by defaults.
- `FocusBar.App`:
  - `ReminderService`: checks every 30 seconds and whenever deadlines change, and shows Windows notifications using the Windows Community Toolkit. Buttons are **Snooze 15 min** and **Mark complete**, and clicking the notification opens the deadline list. One notification per deadline (a newer reminder replaces the older), and it's removed when the deadline is completed.
  - **Settings window** (`Settings/`), opened from the bar's right-click menu. Its Reminders page has on/off, the reminder times, and a **Send a test reminder** button. Later phases add their pages here.

### Problems found and how they were solved
1. **Never showing a reminder twice.** Each reminder has a key made of the deadline, its due time, and how long before it is (for example "1 hour"). Shown keys are saved, so a restart doesn't repeat them. Because the due time is part of the key, moving a deadline sets up fresh reminders for the new time.
2. **Catch-up after the PC was off.** If FocusBar was closed while several reminders for one deadline came due, it shows only the latest one ("due in 30 minutes", noting FocusBar was off), not all three at once.
3. **Adding a deadline that's due soon.** Without a fix, adding something due in 2 hours would instantly fire its "1 day before" and "3 hours before" reminders, because those times had already passed. FocusBar now remembers when it first saw each deadline and skips reminders that were due before then.
4. **Turning reminders back on.** Reminders that came due while they were turned off are marked as skipped, so turning them back on doesn't flood the screen.
5. **Buttons when FocusBar isn't running.** Clicking a notification button can start FocusBar. The click is handled through the toolkit's activation event, which is set up as soon as FocusBar starts, so the snooze or completion still happens.

### Decisions the AI made that the developer should review
- **No reminders after a deadline is due.** Overdue deadlines show in red on the ticker instead. A snooze that ends after the due time still comes back.
- **Snooze is always 15 minutes**, as the build instructions say.
- **Windows registers FocusBar for notifications the first time it shows one.** This is done by the toolkit, in the current user's registry. The Phase 8 installer should undo it on uninstall (`ToastNotificationManagerCompat.Uninstall()`).

---

## Phase 5: Deadline sources (school website, calendars, merging)

**Status:** Compiles (no warnings) and all 268 `FocusBar.Core` unit tests pass, including tests against fake versions of the school website. **Not yet run on Windows, and never run against the real Cranbrook site**: the AI had no account and didn't contact the school's servers. See the Phase 5 checklist in `docs/testing.md`, especially 5.7–5.15.

### What the developer wrote, changed, or decided
- Provided the school website details: `cranbrook.myschoolapp.com`, plus a Python client (`cranbrook.py`), an example script, and an API guide describing the endpoints, the session-cookie sign-in, field names, statuses, and the .NET ticks date format.
- *(Add anything you change after testing with your account.)*

### What the AI generated
- `FocusBar.Core` (all unit-tested):
  - `IDeadlineSource`: the interface from the build instructions, plus `Key`, `Kind`, and `Covers` (the date range a fetch covered), which the merge step needs.
  - `DeadlineList.ApplySync` / `RemoveSource`: adds, updates, and removes a source's deadlines without touching other sources or manual ones.
  - `DeadlineMerger`: finds the same assignment from different sources and shows it once.
  - `IcsCalendarSource`: reads .ics files and links (https and webcal) with Ical.Net.
  - School website (`Sources/School/`), translated from the student's Python client and guide:
    - `MySchoolAppClient`: HTTP with the session cookie and the XMLHttpRequest header.
    - `SchoolApiParser`: turns the site's JSON into deadlines and classes.
    - `SchoolApiSource`: context, Assignment Center, academic groups (class names), and the daily schedule.
    - `SchoolApiConfig` (`school-api.json`).
    - `MockSchoolApiSource`: demo data.
  - `SchoolSessionStore` and `CalendarFeedStore`: the sign-in and calendar links, encrypted through an `ISecretProtector`.
- `FocusBar.App`:
  - `SyncService`: syncs 10 seconds after startup, every 30 minutes, and on Sync now. It keeps the last good data when a source fails, tracks each source's status, and saves the rotated session cookie.
  - `DpapiProtector`: Windows DPAPI encryption for the current user, as the build instructions require.
  - **Sign-in window** (`Sources/SchoolSignInWindow`): the real school site in Microsoft Edge WebView2. It notices when you're signed in, checks the session, and closes. An "I'm signed in" button is a backup.
  - **Paste session cookie** (`Sources/PasteCookieDialog`): the guide's DevTools method, as a fallback.
  - **Settings → Deadline sources**: school status, Sign in, Paste cookie, Use demo data, Disconnect, Sync now, and adding or removing calendars.
  - A ⚠ icon on the bar when a source fails (tooltip says why; clicking opens Settings). Synced deadlines can be completed but not edited or deleted. Sync now in the deadline list.

### Problems found and how they were solved
1. **Signing in without handling the password.** The guide's method (copy a cookie from developer tools) is fiddly for students and impossible for judges to follow. The sign-in window shows the real site, so FocusBar never sees the password, and it reads the session cookie the site sets after you sign in. The paste-cookie method stays as a fallback.
2. **The session token rotates.** The guide says the site changes its session token on responses. FocusBar keeps cookies itself instead of letting .NET manage them, updates them from every response, and saves the newest (encrypted) after each sync, so the sign-in keeps working between restarts. It also retries a 403 once, as the guide suggests.
3. **Expired sign-in looked like success.** When the session expires, the site can answer with its HTML sign-in page instead of an error. FocusBar treats an HTML answer as "signed out" and shows the ⚠ icon instead of quietly syncing nothing.
4. **Due times.** The guide says `date_dueTicks` is .NET ticks of the wall-clock time the portal shows. FocusBar reads those ticks directly as a local date and time, so 11:59 PM on the site is 11:59 PM in FocusBar, rather than being shifted by the time zone.
5. **Field names that vary.** The guide warns that some fields differ by school or role, so each field is looked up by several likely names (ignoring case). An assignment with something missing is skipped rather than failing the whole sync.
6. **Rotating class schedules.** Many schools use Day A/B or 6-day cycles, which a fixed weekly timetable can't represent. FocusBar fetches each actual day for the coming week and refreshes it at least daily.
7. **Deleted assignments.** If a teacher deletes an assignment, it should disappear. But a fetch only covers a date range (7 days back to 60 ahead), so FocusBar only removes a source's deadlines whose due date is inside the range that source just covered.
8. **Duplicates.** The same assignment can arrive from the school, a calendar, and the student's own entry. Duplicates are hidden, not deleted, so deleting your own copy brings back the school's. Completing one completes all copies, otherwise the hidden one would pop back up. Two hand-typed entries are never merged.
9. **Calendars without IDs.** Ical.Net makes up a random ID for an event that has none, which made the same event look new on every sync (caught by a unit test). FocusBar now gives such events an ID made from their contents before parsing.
10. **Private calendar links are passwords too.** A Google Calendar "secret address" lets anyone read the calendar, so calendar links are encrypted like the school sign-in, and the Settings list shows only the site name.

### Decisions the AI made that the developer should review
- **Read-only.** FocusBar never writes to the school site. Marking something complete in FocusBar doesn't change it on the website (the guide describes a way to do that; it was left out on purpose to keep FocusBar from changing school records).
- **Terms of use.** The build instructions asked about rate limits and terms of use, and none were provided. This uses unofficial, undocumented endpoints, so it could stop working if Blackbaud changes them. FocusBar keeps its traffic low: assignments every 30 minutes, the schedule a few times a day, one request at a time with a pause between them. **Check with Cranbrook's tech office that this is OK** before showing it publicly; the app says it's unofficial.
- **"Completed" at school** means status 1 (completed) or 4 (graded). Completing in FocusBar sticks even if the school still lists it as to-do.
- **Date range:** 7 days back to 60 days ahead (set in `school-api.json`).
- **Synced deadlines can't be edited or deleted in FocusBar** (the next sync would bring the old version back). They can be marked complete.
- **Repeating calendar events are skipped.** They're usually classes or practices, not deadlines.
- **The "current/next class" uses the school's real schedule for each of the next 7 days.** There's still no way to type a schedule in by hand.

## Phase 6: Focus Mode

**Status:** Compiles (no warnings) and all 312 `FocusBar.Core` unit tests pass (44 new: hosts file editing, block lists, and when Focus Mode is on). **Not yet run on Windows**, so the permission prompt, the hosts file edits, and closing apps haven't been seen working for real. See the Phase 6 checklist in `docs/testing.md`; tests 6.8, 6.13, and 6.21–6.24 check that the hosts file always ends up clean.

### What the developer wrote, changed, or decided
- *(Add anything you change after testing: which apps and sites you block, the strictness level you like, anything the Focus screen should say.)*

### What the AI generated
- `FocusBar.Core` (all unit-tested):
  - `Focus/HostsFile`: adds and removes FocusBar's block between `# FocusBar BEGIN` and `# FocusBar END`, leaving every other line exactly as it was. Also cleans up what users type ("https://www.YouTube.com/watch?v=1" → `youtube.com`).
  - `Focus/BlockList`: checks app names and settings, the list of Windows and FocusBar programs that can never be blocked, and the "Quick add" site suggestions.
  - `Focus/FocusPlanner`: decides whether Focus Mode is on right now (a manual session, or a deadline inside the window), and whether it can be ended early.
  - `FocusSettings` saved in `settings.json`; the running session in `focus-state.json`.
- `FocusBar.Blocker` (new, separate program): the only part that runs as administrator. It takes `apply <sites…>` or `clear`, backs up the hosts file once, writes it safely (temporary file, then replace), flushes the DNS cache, and logs to `%ProgramData%\FocusBar\blocker.log`. It re-checks every site name itself instead of trusting FocusBar.
- `FocusBar.App`:
  - `FocusController`: turns Focus Mode on and off, checks for blocked apps every 2 seconds while it's on, runs the blocker only when the site list actually changes, and shows toasts.
  - `AppBlocker`: finds blocked apps and closes them (politely first, then forcefully after 1.5 seconds).
  - `WebsiteBlocker`: runs the blocker with Windows' permission prompt, and notices a "No".
  - **Focus screen**: full-screen message with the deadline, a countdown, and per-strictness buttons. "Back to work" or Esc always closes it.
  - **Moon button** on the bar and its flyout: Focus for 25 / 50 / 90 min, what's blocked, website status with Try again, I finished it (mark complete), End focus.
  - **Settings → Focus Mode**: auto on/off and how early, strictness cards, override wait, app list (pick from open apps or Start Menu apps, or type a name), website list with Quick add.
  - Cleanup on every exit path: normal exit and the panic hotkey (after the taskbar is restored), the watchdog after a crash, the next startup, and `--restore-taskbar`.

### Problems found and how they were solved
1. **WMI needs admin.** The build instructions suggested WMI process-start events, but `Win32_ProcessStartTrace` only works for administrators, and FocusBar runs as a normal user. So it checks the running apps every 2 seconds, and only while Focus Mode is on (no cost the rest of the time). A blocked app can run for up to about 2 seconds before it closes.
2. **Never leave the hosts file dirty.** The block has to come off however FocusBar ends. It's removed when Focus Mode ends, on normal exit, by the watchdog if FocusBar is killed, on the next startup if it's still there, and by `--restore-taskbar` / `FocusBar.Blocker.exe clear`. Each of these only removes FocusBar's own lines. If the END marker was deleted by hand, it removes only lines that look like FocusBar's, so it can't eat the rest of the file.
3. **Exit order.** Removing the block shows a permission prompt, which can take a while. The taskbar is restored first, and the prompt comes after, so a slow answer never leaves anyone without a taskbar.
4. **Signing out.** A permission prompt while Windows is signing out or shutting down would hold it up, so FocusBar skips it then; the next start removes the block.
5. **Too many prompts.** Re-running the blocker every 15 seconds would ask for permission constantly. FocusBar reads the hosts file first (no admin needed for that) and only runs the blocker when the site list changes. After a "No" it waits until the student clicks Try again or changes the list.
6. **A helper still running at exit** could put the block back after FocusBar removed it, so exit waits up to 30 seconds for it first.
7. **App names on Windows paths.** Tests for typed app names ("C:\Games\Roblox.exe") failed on the Linux build machine because .NET's path functions follow the machine's rules, not Windows'. The name cleanup now uses Windows rules directly.
8. **Strict that wasn't.** If the settings page stayed editable during a Strict session, switching to Soft and clicking End would get around it. The Focus Mode settings page is locked (with a banner) until a Strict session ends.

### Decisions the AI made that the developer should review
- **No anti-tamper.** As the build instructions require, FocusBar doesn't hide itself, resist being closed, or restart itself. Exiting FocusBar ends Focus Mode, including Strict, and removes the website block. Strict is a commitment the student makes, not a lock.
- **Gentle doesn't block websites.** A hosts-file block can't show a "continue anyway?" choice, and it would mean permission prompts for a mode that's meant to be gentle. Gentle only warns about apps.
- **Overriding** in Soft (and "Continue anyway" in Gentle) allows that app for the rest of the session, not just once.
- **Protected programs.** Explorer, Task Manager, FocusBar itself, and other parts of Windows can't be added to the block list, so Focus Mode can't break the computer or the way out.
- **Sites are blocked for IPv4 and IPv6, with and without `www.`** Other subdomains (like `m.youtube.com`) need to be added separately.
- **Apps running as administrator can't be closed** by FocusBar, since it isn't an administrator.
- **The Focus screen uses a built-in gradient for now.** Phase 7 adds the student's own image.
- **Antivirus programs may flag hosts file changes.** That's expected for this kind of feature; the README mentions it.
- **Secure DNS (DNS over HTTPS)** in browsers can bypass the hosts file. FocusBar doesn't change browser settings; the README explains how to turn it off.

## Phase 7: Customization

**Status:** Compiles (no warnings) and all 376 `FocusBar.Core` unit tests pass (64 new: colors, reading image sizes, the pixel math that keeps pictures sharp, the image folder, and themes, including "a custom theme survives restart"). **Not yet run on Windows**, so nothing has been seen on screen at 100%, 125%, or 150% yet. That's the build instructions' "done when" for this phase, so tests 7.18 and 7.19 in `docs/testing.md` matter most.

### What the developer wrote, changed, or decided
- *(Add your own theme ideas, which pictures you used for the demo, and anything that looked wrong at 125% or 150%.)*

### What the AI generated
- `FocusBar.Core` (all unit-tested):
  - `Theme`: the record from the build instructions plus `BackgroundFit` (Fill / Fit / Center), `BarColor` (or follow Windows' light/dark mode), and `FontFamily`.
  - `Appearance/ThemeLibrary`: the 3 built-in themes (Default, Midnight, Sunrise), cleanup of out-of-range values, save as / apply / delete, and `themes.json`.
  - `Appearance/ThemeImages`: copies pictures into `%AppData%\FocusBar\images`, named after their contents, and removes pictures no theme uses any more.
  - `Appearance/ImageInfo`: reads a picture's real format and size from its first bytes.
  - `Appearance/ImageLayout`: works out exactly how many screen pixels a picture will cover at the current scaling, for Fill, Fit, and Center.
  - `Appearance/ColorMath`: color codes and readable text colors (the WCAG contrast formula).
- `FocusBar.App`:
  - `AppearanceService`: the current theme. Changes show at once; saving waits half a second.
  - `ThemeImageLoader`: decodes each picture once, straight to the planned pixel size, turns phone photos upright, and keeps only the visible part.
  - The bar (`MainBar.Appearance.cs`): background picture with a dark tint, bar color with matching text colors, opacity, height (re-reserving screen space), font, and the Start picture. It reloads pictures when the scaling or screen size changes.
  - The accent color applies to every window. The Focus screen shows the theme's picture.
  - **Settings → Appearance**: theme cards (Use / Undo changes / Delete), Save as, the three pictures with Choose / Remove / Fill-Fit-Center / Preview, color swatches plus color codes, opacity and height sliders, a font search, and Reset.

### Problems found and how they were solved
1. **Sharp at 125% and 150%.** WPF normally draws a picture at whatever size the layout asks and scales it on the fly, which blurs a thin, wide bar. FocusBar works out the bar's real size in screen pixels (for example 2880 × 72 at 150%), decodes the picture straight to that size with Windows' high-quality scaler, and draws it at exactly one picture pixel per screen pixel. It does this again whenever the scaling or screen size changes.
2. **Memory.** A 12-megapixel phone photo filling a 150%-scaled bar would keep about 25 MB in memory if loaded whole. Only the visible 2880 × 72 strip is kept (under 1 MB), and the full decode is thrown away. Pictures over 25 MB, over 10,000 pixels on a side, or over 40 megapixels are refused up front, using the size read from the file's first bytes.
3. **Fake pictures.** A file named `.png` might not be one. FocusBar checks the first bytes for real PNG, JPG, or GIF headers and names its copy after the real format.
4. **Sideways phone photos.** JPGs from phones often store "rotate 90°" as a tag instead of rotating the pixels, and WPF ignores the tag. FocusBar reads it and turns the picture upright.
5. **Readable text on any picture.** The build instructions ask for a subtle dark scrim. With a background picture, the bar always uses white text and a 35% dark tint (and a dark base color for the empty sides in Fit and Center).
6. **Readable text on any color.** A bar color picks white or black text by the WCAG contrast formula, and so does the accent color's button text. That's why yellow gets black text and blue gets white.
7. **See-through needs a different kind of window.** WPF can only make a window see-through when it's created, so switching between 100% and below reopens the bar once (the same way it already reopens when Explorer restarts).
8. **Height changes move other windows.** Each height change makes Windows resize maximized windows, so the height slider only applies when it's let go (or on each arrow-key step), not on every pixel while dragging.
9. **Clock at small heights.** Two lines of text don't fit in a 32-pixel bar, so below 40 pixels the clock shows only the time.
10. **Theme files edited by hand** could point at any file on the computer. Themes store only file names that FocusBar itself created in its images folder; anything else is ignored.

### Decisions the AI made that the developer should review
- **Added to the `Theme` record:** Fill/Fit/Center, a bar color (or "Match Windows"), and the font. Without a bar color, the three built-in themes would only differ in accent color.
- **Ranges:** height 32–72 px (48 is the default from the build instructions), opacity 40–100% (lower makes text hard to read), names up to 40 characters, up to 30 saved themes.
- **The font applies to the bar only.** Flyouts and Settings keep Windows' font so they stay readable whatever you pick.
- **Animated GIFs show their first frame.** Animating would cost CPU all day (the build instructions' limit is about 2%).
- **Changes save automatically**, like the other Settings pages. "Save as" is for keeping a named copy to switch back to; Reset goes back to Default.
- **Built-in themes can't be overwritten or deleted**, but you can use one, change it, and save it under a new name.
- **Opacity below 100% may use a little more CPU** while the ticker scrolls, because see-through windows are drawn differently. Test 7.20 measures it; if it's too much, we could pause the ticker sooner or limit transparency.

## Phase 8: Polish (setup guide, Start with Windows, installer)

**Status:** Compiles (no warnings) and all 392 `FocusBar.Core` unit tests pass (16 new: the Start with Windows entry and the setup-guide flag). A self-contained Windows build (version 0.9.0) was published from the build server. Its files were checked to contain the right Windows manifests (FocusBar.exe asks for per-monitor DPI awareness, FocusBar.Blocker.exe asks for administrator), and it was zipped for the developer to test. **Not yet run on Windows.** The installer script compiles only on Windows, so GitHub Actions builds it; it hasn't been run either. The §9 checklist status is at the end of `docs/testing.md`.

### What the developer wrote, changed, or decided
- Asked for a .zip of the finished build to test.
- *(Add your test results, what you changed, and whether you want your own icon.)*

### What the AI generated
- **Setup guide** (`Setup/SetupWizard`): Welcome → deadlines (add one yourself, or school/calendar/demo through the Deadline sources page) → reminders → Focus Mode → appearance → done. The middle steps reuse the Settings pages, so every choice saves as it's made. It opens by itself once (`SetupCompleted` in `settings.json`), Skip or closing counts as done, and Settings → General can run it again.
- **Start with Windows** (`Core/Settings/StartupCommand`, `Services/StartWithWindows`): the `FocusBar` value under the current user's `Run` key, written only when the student turns it on (in the guide's last step or Settings → General) and deleted when they turn it off. It notices when Task Manager's Startup apps page has it turned off.
- **Settings → General**: Start with Windows, run the setup guide, the ways to get the taskbar back, version, a privacy note, and buttons to open the data and log folders.
- **App icon** (`Assets/FocusBar.ico`): a blue rounded square with a moon and a bar, drawn by a short Python script at 9 sizes. Used for FocusBar.exe, the blocker (so Windows' permission prompt shows it), the installer, and shortcuts.
- **Packaging**:
  - `dotnet publish -r win-x64 --self-contained` makes a folder that runs without .NET installed; publishing also makes `FocusBar.Blocker.exe` a single trimmed 12 MB file.
  - `packaging/FocusBar.iss` is the Inno Setup installer.
  - `packaging/README-FIRST.txt` goes in the zip.
  - GitHub Actions now builds the portable zip and the installer on every push and attaches them to the run as artifacts.
- **README**: Install section, Easy Setup feature list, tech stack (pictures, Start with Windows, packaging), development plan status, future ideas (including the out-of-scope list from §10), and a draft AI disclosure based on this log.

### Problems found and how they were solved
1. **The blocker needed .NET installed.** In the self-contained build, FocusBar.exe carries its own .NET, but the separate blocker was still built to use a system-wide .NET, which test PCs may not have. Publishing now makes the blocker one self-contained, trimmed file (12 MB).
2. **Trimming didn't happen at first.** The blocker came out at 67 MB because the settings were passed in a way that skipped the trimming step's setup. Moving them into the blocker's project file (only for published builds) fixed it.
3. **Leftover files.** The normal build of the blocker left its `.deps.json` and `.runtimeconfig.json` in the published folder next to the single-file version, which could confuse Windows' .NET host. Publishing deletes them first.
4. **Building Windows files on Linux.** Windows checks an .exe's built-in manifest for "needs admin" and for DPI awareness. The published files were inspected to confirm both manifests are embedded, so they don't depend on a Windows build machine.
5. **Never running as administrator.** An installer that runs as admin would normally start "Start FocusBar now" as admin too. The installer starts it as the signed-in user instead (`runasoriginaluser`), so FocusBar never runs elevated and its settings land in the right account.

### Decisions the AI made that the developer should review
- **Installer: "for all users" by default, "just for me" allowed.** All-users installs to Program Files (admin once), where only admins can change files. That matters because the website blocker runs with admin rights. "Just for me" needs no admin, for school-managed laptops, but then website blocking still needs an admin to approve each time.
- **Uninstalling cleans up:**
  - It closes FocusBar (which gives the taskbar back).
  - It removes a leftover website block (Windows asks permission) and the Start with Windows entry.
  - It asks before deleting `%AppData%\FocusBar`; the default answer is No.
  - It keeps `hosts.focusbar-backup`, your original hosts file, as a safety copy.
- **The installer never turns on Start with Windows**; that choice lives in FocusBar's guide and settings, off by default, as the build instructions say.
- **Version 0.9.0** for this test build. Change `Version` in `Directory.Build.props` to 1.0.0 after testing.
- **Not code-signed.** Windows SmartScreen will warn on first run ("More info" → "Run anyway"). Signing needs a paid certificate; listed under Future Ideas.
- **Screenshots** for the README need a real Windows PC, so the Screenshots section explains how to add them.
- **The icon is a placeholder you may want to redesign.** It's generated by a script, so it's easy to change.
- **The setup guide doesn't open again after "Skip"**, so it never nags. Settings → General has "Run the setup guide".

## After Phase 8: Feedback from the first test

### What the developer wrote, changed, or decided
- After trying the test build, asked for two changes:
  1. **Move the app icons to the right**, with the other buttons, and keep the left side of the bar for the deadlines. This changes the layout from the build instructions (Start, pinned apps, open windows, ticker, tray, clock).
  2. **A picture editor for the bar background:** when a picture is chosen, show which part of it the bar will use, let the student drag it to line up the part they want, and zoom in and out.
- Also hit "Access to the compressed (zipped) folder is denied" when extracting the first test zip. The zip was rebuilt with Windows-style file attributes, and the installer from GitHub Actions was offered as another way in.

### What the AI generated
- **New layout:** Start | deadline ticker (fills the left side) | pinned apps | open windows | Focus Mode | tray | clock. `BarMiddlePanel` now puts the ticker first and the app icons on the right. The ticker still keeps at least 300 px; with many windows open, the window buttons shrink instead.
- **Bar picture editor** (`Settings/BackgroundEditor`): the picture dimmed behind a white-outlined strip shaped like the real bar (screen width × bar height), with the bar's dark tint inside the strip.
  - Drag with the mouse or arrow keys.
  - Zoom from 100% to 1000% with the slider, the − / + buttons and keys, or the mouse wheel (which zooms around the pointer).
  - Reset, Cancel, and "Use this picture".
  - It opens after choosing a bar picture (Cancel keeps the old picture); **Adjust…** reopens it.
- `FocusBar.Core`:
  - `ImageCrop`, saved in the theme as the spot on the picture at the middle of the bar plus a zoom level.
  - `CropMath`: which part of the picture fills the strip, dragging, zooming around a point, and keeping the picture covering the strip.
  - `ImageLayout` now uses the crop.
  - 13 new unit tests (405 total).

### Problems found and how they were solved
1. **Saving the crop so it survives changes.** Saving pixel coordinates would break when the bar's height, the screen, or the scaling changes. The crop is saved as "this spot on the picture goes in the middle, zoomed this much past just covering the bar". At any bar size the same spot stays centered, and the picture is kept inside its edges (unit-tested for a taller bar).
2. **Zooming in without huge memory use.** Zoomed to 1000%, the old approach would have decoded a photo at ten times its size (hundreds of megabytes). Now a picture is never decoded bigger than it really is: shrinking still happens during decoding (sharp), and enlarging happens when drawing, on just the small visible part.
3. **The strip is very thin** (a 1920 × 48 bar is 40:1). Everything outside it is darkened heavily and it has a white outline, so it's easy to see what will show even though it's thin.

### Decisions the AI made that the developer should review
- **The Start button stays at the far left**, before the deadlines. "Keep the left for the deadlines" could also mean moving Start to the right; it's a one-line change if you want that.
- **Pinned apps come before open windows** on the right, in the same order as before.
- **The editor sets the background to Fill**, since lining up a part only matters when the picture is cropped. Fit and Center are still there and ignore the crop.
- **Maximum zoom is 1000%.** Past "real size" a picture gets softer, like any enlarged photo.
- **The editor preview** loads the picture at up to 4096 pixels on its long side: sharp enough to line things up without holding a huge photo in memory. The bar itself always loads from the full-size file.
