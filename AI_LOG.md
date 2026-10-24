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
