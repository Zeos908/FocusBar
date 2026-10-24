# Build Instructions: FocusBar

You are building **FocusBar**, a Windows desktop app that replaces the Windows taskbar for middle and high school students. It shows a scrolling ticker of upcoming deadlines, sends reminders, blocks distracting apps and websites near deadlines, and can be customized with uploaded images.

This app is a student's entry for the **Congressional App Challenge**. Judges will run it, so a stable app with fewer features beats an ambitious app that crashes. Build in the phases below, and make sure each phase runs before starting the next.

---

## 1. Ground Rules

1. **Never leave the user without a taskbar.** If FocusBar exits, crashes, or is killed, the Windows taskbar must come back. Handle this before building anything else (see §5).
2. **Work in phases.** Finish a phase, confirm it builds and runs, commit, then move on. At the end of each phase, give the human a short summary: what works, what doesn't, how to test it.
3. **Ask, don't guess, about the school API.** Its details are unknown (see §8). Build against an interface with a mock until the human provides real details.
4. **Keep everything local.** No accounts, cloud services, analytics, or telemetry. All data lives on the user's computer.
5. **Never store secrets in plain text.** Encrypt any school login token with Windows DPAPI (`System.Security.Cryptography.ProtectedData`, CurrentUser scope). Never commit credentials.
6. **Don't build anti-tamper features.** Focus Mode is a self-control tool the student opts into. Never hide the process, make it unkillable, add itself to startup without asking, or resist uninstallation.
7. **Keep a log of your work** in `AI_LOG.md` (see §11). The competition requires full disclosure of AI use.

---

## 2. Tech Stack (decided)

| Area | Choice |
|---|---|
| Language / framework | C#, .NET 8, WPF |
| Shell foundation | [ManagedShell](https://github.com/cairoshell/ManagedShell) (NuGet: `ManagedShell`): provides the AppBar window, task list, and notification-area (tray) services |
| Pattern | MVVM (`CommunityToolkit.Mvvm`) |
| Notifications | `Microsoft.Toolkit.Uwp.Notifications` (toast notifications from an unpackaged WPF app) |
| Calendar parsing | `Ical.Net` |
| Storage | JSON files in `%AppData%\FocusBar\` via `System.Text.Json` |
| Tests | xUnit for non-UI logic |
| Target OS | Windows 10 (1809+) and Windows 11, x64 |

Use ManagedShell instead of writing raw Win32 code for docking, the window list, and the tray. Read its wiki and study RetroBar (an open-source taskbar built on it) as a reference implementation. Check RetroBar's license before copying any code, and credit anything you adapt.

---

## 3. Project Structure

```
FocusBar/
├── FocusBar.sln
├── src/
│   ├── FocusBar.App/            # WPF app: windows, views, view models
│   │   ├── Shell/               # AppBar, Start launcher, task list, tray, clock
│   │   ├── Ticker/              # Scrolling deadline ticker
│   │   ├── Focus/               # Focus Mode UI + blocked screen
│   │   ├── Settings/            # Settings window, themes, image upload
│   │   └── App.xaml(.cs)        # Startup, crash handling, taskbar restore
│   ├── FocusBar.Core/           # No WPF references: models + logic
│   │   ├── Models/              # Deadline, ClassPeriod, Theme, FocusSettings
│   │   ├── Deadlines/           # IDeadlineSource, merge/dedupe, scheduler
│   │   ├── Focus/               # Blocking rules, strictness logic
│   │   └── Storage/             # JSON persistence
│   └── FocusBar.Blocker/        # Small elevated helper for hosts-file edits (Phase 6)
├── tests/
│   └── FocusBar.Core.Tests/
├── README.md                    # Already exists; update as features land
└── AI_LOG.md
```

Put all testable logic (merging, deduplication, urgency, reminder timing, strictness rules) in `FocusBar.Core` so it can be unit-tested without UI.

---

## 4. Data Models

```csharp
public enum DeadlineSource { Manual, Calendar, SchoolApi }
public enum Urgency { Low, Medium, High, Overdue }
public enum Strictness { Gentle, Soft, Strict }

public record Deadline(
    Guid Id,
    string Title,
    string? ClassName,
    string? Description,
    DateTimeOffset DueAt,
    DeadlineSource Source,
    string? ExternalId,      // ID from the school API or the .ics UID
    bool IsComplete);

public record ClassPeriod(string ClassName, DayOfWeek Day, TimeOnly Start, TimeOnly End, string? Room);

public record FocusSettings(
    bool AutoEnable,
    TimeSpan StartBeforeDeadline,  // default 2 hours
    Strictness Strictness,         // default Soft
    TimeSpan SoftOverrideDelay,    // default 60 seconds
    List<string> BlockedApps,      // process names, e.g. "Discord"
    List<string> BlockedSites);    // domains, e.g. "youtube.com"

public record Theme(
    string Name,
    string? BackgroundImagePath,
    string? StartIconPath,
    string? FocusScreenImagePath,
    string AccentColor,
    double Opacity,
    int BarHeight);
```

Urgency, by time remaining:
- **Low** (green): more than 3 days
- **Medium** (yellow): 1 to 3 days
- **High** (red): under 24 hours
- **Overdue** (dark red): past due and not complete

Put these thresholds in one place so they're easy to change.

---

## 5. Phase 0: Safety Net (do first)

- On startup, record the Windows taskbar's visibility state, then hide it (ManagedShell can do this).
- Restore the taskbar on **every** exit path:
  - normal exit
  - `AppDomain.CurrentDomain.UnhandledException`
  - `Application.DispatcherUnhandledException`
  - `TaskScheduler.UnobservedTaskException`
  - Windows shutdown or logoff (`SessionEnding`)
- Register a global hotkey, **Ctrl+Alt+Shift+F**, that immediately restores the Windows taskbar and exits FocusBar.
- Add a tray or right-click menu item: "Restore Windows taskbar and exit."
- Write unhandled errors to `%AppData%\FocusBar\logs\`.

**Done when:** killing FocusBar from Task Manager, throwing a test exception, and pressing the hotkey all leave a working Windows taskbar. If a killed process can't restore it, document how the user gets it back, such as relaunching FocusBar or restarting Explorer.

---

## 6. Build Phases

### Phase 1: The Bar
- AppBar docked to the bottom of the primary monitor that reserves screen space, so maximized windows stop above it.
- Windows taskbar hidden while FocusBar runs.
- Layout, left to right: **Start button | pinned apps | open windows | ticker (fills remaining space) | tray | clock**. Use placeholders for anything not built yet.
- Respect DPI scaling.

**Done when:** the bar docks correctly, maximized windows don't overlap it, and Phase 0 still works.

### Phase 2: Taskbar Features
- **Clock:** time and date; clicking it opens a simple calendar flyout.
- **Tray:** use ManagedShell's tray service for other apps' icons, plus Wi-Fi, volume, and battery indicators. Clicking each one opens the matching Windows settings page (`ms-settings:` URIs).
- **Open windows:** list running windows with icons via ManagedShell's tasks service. Click to switch, click the active one to minimize, right-click to close.
- **Pinned apps:** the user can pin and unpin apps (from the Start launcher or by right-clicking a running window). Save pins to JSON.
- **Start launcher:** a popup with a search box listing installed apps (scan the Start Menu `.lnk` folders in both `%ProgramData%` and `%AppData%`). Type to filter, Enter to launch. Include an option to open the real Windows Start menu.

**Done when:** a user can do their normal daily work with only FocusBar.

### Phase 3: Manual Deadlines + Ticker
- Quick-add button (➕) on the bar opens a small form: title, class (optional), due date and time, description (optional).
- Deadline list window: view, edit, delete, mark complete.
- **Ticker:** smoothly scrolls through incomplete deadlines, each showing title, class, and a live countdown ("Bio lab, 2d 4h"), colored by urgency.
  - Pause on hover; click an item for details and a "Mark complete" button.
  - Show the current or next class from the schedule when one is available.
  - Animate with a WPF `Storyboard` or `CompositionTarget.Rendering`, and stay under ~2% CPU.
- Save deadlines to `deadlines.json`.

**Done when:** deadlines persist across restarts and the ticker scrolls smoothly at low CPU.

### Phase 4: Reminders
- Default reminder offsets: 1 day, 3 hours, and 1 hour before due. Configurable in Settings.
- Toast notifications with **Snooze 15 min** and **Mark complete** buttons.
- Don't send duplicates: track which reminders have already fired, and survive restarts.
- If the app was off when a reminder was due, show at most one catch-up reminder per deadline.

**Done when:** unit tests cover reminder timing, and toasts appear with working buttons.

### Phase 5: Deadline Sources
Define:
```csharp
public interface IDeadlineSource
{
    string Name { get; }
    Task<IReadOnlyList<Deadline>> FetchDeadlinesAsync(CancellationToken ct);
    Task<IReadOnlyList<ClassPeriod>> FetchScheduleAsync(CancellationToken ct); // may return empty
}
```
- **Calendar source:** import a local `.ics` file or subscribe to an `.ics` URL (refresh every 30 min). Map `VEVENT`/`VTODO` to `Deadline`, using the UID as `ExternalId`.
- **School API source:** see §8. Implement `MockSchoolApiSource` first, with realistic fake data.
- **Merge and deduplicate:** match on `(Source, ExternalId)` first. Across sources, treat two deadlines as duplicates if their normalized titles match and their due times are within 1 hour of each other. Keep the manual entry if one exists, otherwise the school API version. Unit-test this thoroughly.
- Sync on startup and every 30 minutes, plus a "Sync now" button. A failed source shows a small warning icon and never crashes the app.

**Done when:** deadlines from all three sources appear in one deduplicated ticker, and merge logic has passing tests.

### Phase 6: Focus Mode
- **Activation:** automatic when an incomplete deadline is within `StartBeforeDeadline`, or manual ("Focus for 25 / 50 / 90 min").
- **App blocking:** watch process starts (WMI `Win32_ProcessStartTrace`, or a 2-second polling fallback if WMI needs admin). When a blocked app starts:
  - **Gentle:** show a warning; the user may continue.
  - **Soft:** close the app and show the Focus screen, with an "Override" button that unlocks after `SoftOverrideDelay`.
  - **Strict:** close the app and show the Focus screen with no override until the deadline passes or is marked complete.
- **Website blocking** (hosts-file approach):
  - `FocusBar.Blocker` is a tiny separate exe that requests admin rights (UAC) only when Focus Mode starts or stops. It adds or removes entries pointing blocked domains (and their `www.` versions) to `0.0.0.0`.
  - Wrap FocusBar's entries between `# FocusBar BEGIN` and `# FocusBar END` markers, and never touch other lines.
  - Back up the hosts file before the first edit.
  - On startup, if Focus Mode isn't active but markers exist (from a crash), remove them.
  - Flush DNS after changes (`ipconfig /flushdns`).
  - Note in the README that browsers using DNS-over-HTTPS may bypass hosts rules; a browser extension is a future option.
- **Focus screen:** full-screen overlay with the user's Focus Mode image (or a default), the deadline causing the block, and a countdown.
- **Settings:** pick apps from the running-process list or installed apps; type domains manually; choose strictness.

**Done when:** each strictness level behaves as described, hosts entries are always cleaned up, and the app never edits hosts without the UAC prompt.

### Phase 7: Customization
- Settings → Appearance:
  - Upload images for **bar background**, **Start button icon**, and **Focus Mode screen**. Accept PNG, JPG, and GIF. Copy them into `%AppData%\FocusBar\images\` so the originals can move.
  - Background image: offer Fill / Fit / Center, and scale it for a thin, wide bar so it stays sharp at high DPI.
  - Accent color, opacity, bar height, font.
  - Save, switch, and delete named themes. Include 3 built-in themes.
- Changes preview live.

**Done when:** a custom theme survives restart and looks right at 100%, 125%, and 150% scaling.

### Phase 8: Polish
- First-run setup wizard: welcome → add deadlines (manual, calendar, or school) → reminders → Focus Mode → appearance.
- Settings option "Start with Windows" (off by default; uses the user's Startup registry key).
- Installer (Inno Setup or MSIX).
- Update `README.md`: final feature list, accurate tech stack, install steps, screenshots.
- Run through the test checklist in §9.

---

## 7. UI Guidelines

- Default bar height of 48px at 100% scaling; text must be readable on any background image (add a subtle dark scrim behind text).
- Clean, modern look; follow Windows 11 styling when possible (rounded flyouts, Segoe UI Variable / Segoe Fluent Icons).
- Every flyout closes on outside click or Esc.
- Support light and dark mode based on the Windows setting.
- Keyboard accessible: Tab through bar items, Enter to activate.

---

## 8. School API (needs human input)

The student has an API for their school's website that provides **assignments with due dates** and the **class schedule**. Before implementing the real client, **ask the human for**:

1. The base URL and the endpoints for assignments and schedule
2. How authentication works (API key, username/password login, token, cookie)
3. A sample JSON response for each endpoint (with personal info removed)
4. Any rate limits or terms of use

Then implement `SchoolApiSource : IDeadlineSource`, keeping these choices in a config file (`school-api.json`) rather than hardcoded. Store any credentials or tokens encrypted with DPAPI. Map the response fields to `Deadline` and `ClassPeriod`. If the API fails, keep showing the last successful data and display a warning icon.

Until then, use `MockSchoolApiSource` so every other feature can be built and tested.

---

## 9. Testing Checklist

- [ ] Killing the process, crashing, and the panic hotkey all restore the Windows taskbar
- [ ] Maximized windows never overlap the bar
- [ ] Start launcher finds and launches apps; pinned apps persist
- [ ] Window switcher handles 20+ open windows
- [ ] Tray icons from other apps appear and respond to clicks
- [ ] Ticker CPU usage stays under ~2%; memory stays under ~150 MB
- [ ] Deadlines persist; manual, .ics, and mock school data merge with no duplicates
- [ ] Reminders fire once, snooze works, and catch-up works after restart
- [ ] Each strictness level behaves correctly
- [ ] Hosts file is cleaned up after normal exit and after a crash
- [ ] Custom images look right at 100%, 125%, and 150% scaling
- [ ] Works on both Windows 10 and Windows 11
- [ ] All `FocusBar.Core` unit tests pass

---

## 10. Out of Scope (for now)

Multi-monitor bars, Google Classroom / Canvas / Schoology, recurring deadlines, study stats, parent/teacher views, and a browser extension. List them as future work in the README; don't build them.

---

## 11. AI Log

Maintain `AI_LOG.md` throughout. For each phase, record:
- What you (the AI) generated
- What the student wrote, changed, or decided
- Significant bugs and how they were fixed (good material for the competition's "technical challenge" question, which is limited to 1,500 characters)

The student will use this log to write their AI usage disclosure and technical-challenge answer, so keep it accurate and specific.
