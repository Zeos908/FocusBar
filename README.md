# FocusBar — A Smart Taskbar for Students

> A full replacement for the Windows taskbar that keeps every deadline scrolling in view, reminds you before they sneak up, and blocks distracting apps and websites when it's crunch time.

**Congressional App Challenge 2026 — Project Concept**
**Developer:** Suhaas
**Platform:** Windows 10 / 11
**Audience:** Middle and high school students
**Status:** 🟡 Version 0.9.1 test build: all eight build phases are done (safety net, docked bar, taskbar features, deadlines and the ticker, reminders, school and calendar sync, Focus Mode, customization, setup guide and installer). Next: testing on real Windows PCs with classmates, then the demo video.

---

## Table of Contents

1. [The Problem](#the-problem)
2. [The Solution](#the-solution)
3. [Features](#features)
4. [How It Works](#how-it-works)
5. [Tech Stack](#tech-stack)
6. [Install](#install)
7. [Building and Running](#building-and-running)
8. [If Your Taskbar Disappears](#if-your-taskbar-disappears)
9. [User Walkthrough](#user-walkthrough)
10. [Screenshots & Demo](#screenshots--demo)
11. [Technical Challenges](#technical-challenges)
12. [Development Plan](#development-plan)
13. [Future Ideas](#future-ideas)
14. [AI Usage Disclosure](#ai-usage-disclosure)
15. [Acknowledgments](#acknowledgments)

---

## The Problem

Middle and high schoolers juggle deadlines spread across their school portal, Google Calendar, planners, and sticky notes — but none of those are visible while they're actually working on their computer. Assignments get buried under open windows, and distractions like games and YouTube are always one click away. By the time a student remembers an assignment, it's often too late to do their best work.

## The Solution

FocusBar replaces the Windows taskbar entirely — the one part of the screen students look at dozens of times an hour. It keeps everything a normal taskbar does (Start menu, open windows, pinned apps, clock, Wi-Fi, volume, battery) and adds a scrolling ticker of every upcoming deadline, pulled automatically from the school's website and the student's calendar. When a deadline gets close, it sends reminders and can lock out distracting apps and websites. Students can make it their own with custom images, so it feels like a personal tool rather than another nagging app.

---

## Features

### 🖥️ Full Taskbar Replacement
FocusBar hides the default Windows taskbar and replaces every core feature. Deadlines get the left side of the bar; app icons sit on the right with the Focus Mode button, tray, and clock.
- **Start button / app launcher** — type to search installed apps, Enter to open, right-click to pin; a button opens the regular Windows Start menu
- **One button per app**, like the Windows taskbar — an app with several windows shows a count badge; click it to pick a window, or right-click to open a new one or close them all. Click a single window to switch to it or minimize it; middle-click closes it
- **Pinned apps** — starts with the apps pinned to your Windows taskbar; pin more from the launcher or from a running window; an open pinned app shares its pinned button
- **Clock and system tray** — time and date with a calendar, Wi-Fi, volume (scroll to adjust), battery, and other apps' tray icons
- Follows Windows light and dark mode
- One click (or Ctrl+Alt+Shift+F) restores the standard Windows taskbar at any time
- Gets out of the way when the computer is low on memory: if more than 90% of RAM stays in use for 30 seconds, FocusBar closes and gives the Windows taskbar back (Settings → General; 80–95%, or off)

### 📅 Deadline Ticker
- A **scrolling ticker** cycles through all upcoming deadlines with live countdowns ("Bio lab · Biology 2d 4h")
- **Color-coded urgency** — green (more than 3 days) → yellow (1–3 days) → red (under a day) → dark red when overdue
- Hover to pause; click a deadline for details (class, notes, due time) and **Mark complete**
- **Quick add** (➕): type "fri" or "10/31" for the date and "3pm" for the time, or pick on a calendar
- A **deadline list** window to edit, delete, and complete deadlines
- Shows the student's **current/next class** from their schedule
- Light on the computer: only scrolls when the deadlines don't fit, pauses behind full-screen games and videos, and stays still if Windows animations are turned off

### 🔄 Deadline Sources
Deadlines flow in from three places and merge into one list:
| Source | How it works |
|---|---|
| **School website** | Signs in to the school's Blackbaud site (cranbrook.myschoolapp.com) in a built-in browser window, then imports assignments, due dates, and the class schedule every 30 minutes |
| **Calendar (.ics)** | Add a calendar file or subscribe to a link (Google Calendar, Google Classroom, Outlook, etc.) |
| **Manual entry** | Quick-add a deadline right from the bar |
| **Demo data** | Realistic made-up assignments and schedule, for trying FocusBar without a school account |

Duplicate deadlines from different sources are detected and shown once (titles match and due within an hour; your own entry wins, then the school's). If a source can't be reached, FocusBar keeps its last deadlines and shows a ⚠ icon on the bar.

### 🔔 Smart Reminders
- Native Windows notifications
- Configurable timing in Settings (default: 1 day, 3 hours, and 1 hour before; also 1 week, 2 days, 30 and 15 minutes)
- **Snooze 15 min** or **Mark complete** straight from the notification
- Never repeats a reminder, even after a restart; if the PC was off, shows just one catch-up reminder per deadline

### 🚫 Focus Mode (App & Website Blocking)
- Choose which **apps** (games, Discord, etc.) and **websites** (YouTube, game sites, etc.) to block
- Turns on automatically within a set window before a deadline (e.g., 2 hours out), or manually for study sessions
- **Adjustable strictness** — the student picks the level:
  | Level | Behavior |
  |---|---|
  | **Gentle** | Warning pop-up, can continue anyway |
  | **Soft** | Blocked, with an override after a short wait |
  | **Strict** | Fully blocked until the deadline passes or is marked complete |
- Blocked apps and sites show a custom Focus screen with how long until they unlock

**How to use it:** click the moon 🌙 on the bar to focus for 25, 50, or 90 minutes, or let it turn on by itself before a deadline. Pick apps, websites, and the strictness level in right-click → **FocusBar settings** → **Focus Mode**.

**How website blocking works.** While Focus Mode is on, FocusBar adds the sites to the Windows *hosts* file, so they don't load in any browser. Editing that file needs administrator rights, so a tiny separate program, `FocusBar.Blocker.exe`, does it; Windows asks for your permission (UAC) when Focus Mode starts and when it ends. FocusBar itself never runs as administrator. The blocker only touches lines between `# FocusBar BEGIN` and `# FocusBar END`, makes a one-time backup (`hosts.focusbar-backup` next to the hosts file), and always removes its lines when Focus Mode ends, when FocusBar exits, or (after a crash) through the watchdog or the next start. Gentle mode doesn't block websites, so it never asks for permission.

**Limits worth knowing:**
- Browsers set to use **secure DNS (DNS over HTTPS)** can skip the hosts file, so blocked sites may still load. Turn it off in the browser's privacy/security settings (Chrome and Edge: "Use secure DNS"; Firefox: "DNS over HTTPS").
- Tabs that were already open may keep working until you reload them or restart the browser.
- Some antivirus programs warn when the hosts file changes. That warning is about FocusBar's blocker; you can allow it or skip website blocking.
- Apps that run as administrator can't be closed by FocusBar.
- FocusBar doesn't fight you: exiting FocusBar ends Focus Mode (even Strict) and removes the block. It's a study tool, not parental control.

**If websites stay blocked:** start FocusBar again (it removes a leftover block on startup), or run `FocusBar.Blocker.exe clear` from FocusBar's folder, or `FocusBar.exe --restore-taskbar`. As a last resort, `hosts.focusbar-backup` (in `C:\Windows\System32\drivers\etc`) is your hosts file from before FocusBar first changed it.

### 🎨 Customization with Images
Students can upload their own images for:
- **Bar background**
- **Start button icon**
- **Focus Mode screen** (shown when something is blocked)

Plus colors, fonts, transparency, and bar height — saved as switchable themes.

**How to use it:** right-click the bar → **FocusBar settings** → **Appearance**. Every change shows on the bar right away. Start from one of the three built-in themes (**Default**, which follows Windows' light/dark mode; **Midnight**; **Sunrise**), change what you like, and **Save** it under your own name to switch back to it later.

- Pictures can be PNG, JPG, or GIF (animated GIFs show their first frame). FocusBar keeps its own copy in `%AppData%\FocusBar\images`, so you can move or delete the original.
- Choosing a bar background opens a **picture editor**: the picture sits behind a bright, bar-shaped strip, and you drag it and zoom (slider, mouse wheel, or keys) until the part you want is in the strip. **Adjust…** reopens it later. The background can also **Fit** inside the bar or sit in the **Center** at real size. Pictures are resized once, with high quality, to the exact screen pixels they cover, so they stay sharp at 100%, 125%, and 150% scaling. Phone photos are turned the right way up.
- On a background picture, the bar adds a subtle dark tint and uses white text so it's always readable. With a plain bar color, the text turns white or black depending on which reads better.
- Opacity below 100% lets the wallpaper show through (the bar reopens once when you switch between see-through and solid).

### ⚙️ Easy Setup
- A **setup guide** on first launch: deadlines → reminders → Focus Mode → appearance
- **Start with Windows** is optional and off until you turn it on (Settings → General)
- An **installer** or a **portable zip**; uninstalling gives the taskbar back and cleans up after itself

---

## How It Works

```
┌───────────────────────────────────────────────────────────────┐
│                           FocusBar                            │
├───────────┬───────────┬────────────┬────────────┬─────────────┤
│ Taskbar   │ Deadline  │ Reminder   │ Focus Mode │ Theme &     │
│ Shell     │ Ticker    │ Scheduler  │ Blocker    │ Settings    │
│ (Start,   │           │            │ (apps +    │             │
│  windows, │           │            │  websites) │             │
│  tray)    │           │            │            │             │
└─────┬─────┴─────┬─────┴──────┬─────┴──────┬─────┴──────┬──────┘
      │           │            │            │            │
      └───────────┴──────┬─────┴────────────┴────────────┘
                         │
              ┌──────────▼──────────┐
              │   Deadline Sync     │
              │ School API │ .ics │ │
              │      Manual         │
              └──────────┬──────────┘
                         │
              ┌──────────▼──────────┐
              │   Local Storage     │
              │ (deadlines, themes, │
              │  images, settings)  │
              └─────────────────────┘
```

1. **Taskbar Shell** — Hides the Windows taskbar, registers FocusBar as an "AppBar" so it reserves screen space, and rebuilds the Start launcher, window switcher, pinned apps, and system tray.
2. **Deadline Sync** — Pulls assignments and the class schedule from the school API, reads .ics calendars, and merges them with manual entries.
3. **Deadline Ticker** — Renders the scrolling list with countdowns and urgency colors.
4. **Reminder Scheduler** — Fires Windows notifications at the chosen intervals.
5. **Focus Mode Blocker** — Watches for blocked apps launching and blocks listed websites while Focus Mode is on, at the strictness level the student chose.
6. **Local Storage** — Everything stays on the student's computer. No accounts, no cloud, no tracking.

---

## Tech Stack

| Component | Technology | Why |
|---|---|---|
| Language / UI | C# with WPF (.NET 8), MVVM via `CommunityToolkit.Mvvm` | Best access to the Windows APIs a taskbar replacement needs |
| Taskbar docking, window list, tray | [ManagedShell](https://github.com/cairoshell/ManagedShell) | Wraps the Win32 AppBar (`SHAppBarMessage`), task list, and notification-area APIs |
| Notifications | `Microsoft.Toolkit.Uwp.Notifications` | Toast notifications from a regular desktop app |
| App blocking | Checks running apps every 2 seconds, only while Focus Mode is on | WMI process-start events would need administrator rights |
| Website blocking | Hosts-file rules, via a small helper (`FocusBar.Blocker`) that asks for admin only when needed | Blocks sites across browsers |
| Calendar import | Ical.Net | Parses .ics files |
| School data | The school site's own JSON endpoints (Blackbaud onCampus), signed in through Microsoft Edge WebView2 | Assignments, due dates, schedule |
| Secrets | Windows DPAPI | School sign-in and private calendar links are stored encrypted for your Windows account |
| Pictures | WPF imaging (Windows Imaging Component), decoded to exact screen pixels | Sharp at 100%, 125%, and 150% scaling without wasting memory |
| Storage | JSON in `%AppData%\FocusBar` | Simple, local, private |
| Start with Windows | The current user's `Run` registry key, only if the student turns it on | No admin rights, easy to see and turn off in Task Manager |
| Packaging | Self-contained .NET publish, as a portable zip and an [Inno Setup](https://jrsoftware.org/isinfo.php) installer, built by GitHub Actions | Runs on PCs without .NET installed |
| Tests | xUnit, run on Windows by GitHub Actions on every push | Covers all non-UI logic in `FocusBar.Core` |

### Project layout

```
FocusBar.sln
├── src/FocusBar.App/         WPF app: the bar, safety net, ticker, reminders, sync, Focus Mode, settings
├── src/FocusBar.Blocker/     Tiny admin-only helper that adds/removes FocusBar's lines in the hosts file
├── src/FocusBar.Core/        Models and logic with no UI code, so it can be unit-tested
├── tests/FocusBar.Core.Tests xUnit tests for FocusBar.Core
├── packaging/                Installer script (Inno Setup) and the read-me that goes in the zip
└── docs/                     Windows test checklist and sample files
```

---

## Install

**You need:** Windows 10 (version 1809 or newer) or Windows 11, 64-bit. Nothing else: .NET is included. The school sign-in window uses Microsoft Edge WebView2, which comes with Windows 11 and most Windows 10 PCs.

Every push to GitHub builds both of these (open the repository's **Actions** tab → the latest **Build and test** run → **Artifacts**):

- **Installer** (`FocusBar-Setup-0.9.1.exe`): installs for everyone (asks for admin once, recommended) or just for you (no admin). Adds Start Menu shortcuts, including **Restore Windows taskbar (FocusBar)** as an emergency way back. Uninstall from Windows Settings → Apps; it closes FocusBar (giving the taskbar back), removes any leftover website block and the Start with Windows entry, and asks before deleting your data.
- **Portable zip** (`FocusBar-0.9.1-portable.zip`): extract it anywhere and run `FocusBar.exe`. It offers to install FocusBar in `%LocalAppData%\Programs\FocusBar` (with a Start Menu shortcut), and every newer zip run the same way **updates that copy** instead of making another one: it closes the running FocusBar, replaces the files, and starts the new version. Your deadlines and settings aren't touched, and the extracted folder can be deleted afterwards. See `README-FIRST.txt` inside.

This test build isn't code-signed, so Windows SmartScreen may say "Windows protected your PC". Click **More info** → **Run anyway**.

The first time FocusBar runs, a **setup guide** walks through deadlines, reminders, Focus Mode, and the look of the bar (you can run it again from Settings → General). FocusBar does **not** start with Windows unless you turn that on in the guide or in Settings → General.

---

## Building and Running

**You need:** Windows 10 (1809 or newer) or Windows 11, and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). Visual Studio 2022 (with the ".NET desktop development" workload) is the easiest way to work on it.

**Visual Studio:** open `FocusBar.sln`, set **FocusBar.App** as the startup project, and press F5.

**Command line:**

```powershell
dotnet build FocusBar.sln
dotnet test tests/FocusBar.Core.Tests
dotnet run --project src/FocusBar.App
```

While FocusBar runs, it hides the Windows taskbar and takes its place. To get the normal taskbar back, press **Ctrl+Alt+Shift+F**, or right-click FocusBar and choose **Restore Windows taskbar and exit**.

Debug builds add a **Developer** submenu to the right-click menu, with options that throw a test exception. Use them to check that a crash brings the Windows taskbar back.

FocusBar's files live in `%AppData%\FocusBar\`: `deadlines.json` (your deadlines), `settings.json` (settings), `reminders.json` (which reminders were already shown), `school-api.json` (school site settings), `school-session.json` and `calendars.json` (encrypted), `schedule.json` (this week's classes), `focus-state.json` (a running focus session), `themes.json` (your look and saved themes) and the `images` folder (your pictures), `pins.json` (pinned apps) and the `pins` folder (shortcuts brought over from the Windows taskbar), `tray.json` (tray icons shown on the bar), and error logs in the `logs` folder (right-click the bar → **Open log folder**).

The step-by-step Windows test checklist for each phase is in [docs/testing.md](docs/testing.md).

### Building the zip and installer

```powershell
dotnet publish src/FocusBar.App -c Release -r win-x64 --self-contained -o publish
iscc packaging\FocusBar.iss /DAppVersion=0.9.1      # Inno Setup 6; the installer lands in dist\
```

Publishing also makes `FocusBar.Blocker.exe` a single self-contained file next to `FocusBar.exe`. GitHub Actions runs the same steps (see `.github/workflows/build.yml`) and uploads the zip and installer as artifacts.

---

## If Your Taskbar Disappears

FocusBar is built to always give the Windows taskbar back: on normal exit, on a crash, when Windows signs out, and when you press **Ctrl+Alt+Shift+F**. If FocusBar is killed from Task Manager, a small helper process (a second `FocusBar` entry in Task Manager) restores the taskbar for it.

If the taskbar is still missing (for example, both FocusBar processes were ended with "End process tree"), try these in order. Each one also asks your apps to put their tray icons back on the Windows taskbar.

1. **Start FocusBar again.** On launch it notices it didn't clean up last time and restores the taskbar first. Then exit it normally.
2. Run `FocusBar.exe --restore-taskbar` (from the folder FocusBar.exe is in). It restores the taskbar and your original auto-hide setting without starting the bar.
3. Press **Ctrl+Shift+Esc** to open Task Manager, find **Windows Explorer**, and choose **Restart**. If the taskbar now hides itself until you point at the bottom of the screen, turn off **Settings → Personalization → Taskbar → Automatically hide the taskbar**.

---

## User Walkthrough

1. **First launch** — FocusBar replaces the taskbar, and the setup guide opens (with a reminder that Ctrl+Alt+Shift+F always brings the Windows taskbar back).
2. **Connect deadlines** — Sign in to the school website, add a calendar, type deadlines in, or try demo data.
3. **Set reminders** — Pick when to be notified (or keep the defaults: 1 day, 3 hours, 1 hour before).
4. **Set up Focus Mode** — Choose apps and websites to block, how early blocking starts, and the strictness level.
5. **Customize** — Pick a theme, or upload a background, Start button icon, and Focus screen image.
6. **Finish** — Optionally turn on "Start FocusBar when I sign in to Windows".
7. **Work** — Deadlines scroll by, reminders pop up on time, and distractions lock when it counts. Everything can be changed later: right-click the bar → **FocusBar settings**.

---

## Screenshots & Demo

> *Screenshots need a real Windows PC (FocusBar was built on a build server that can't show it). Take them while running the [test checklist](docs/testing.md), save them in a `screenshots/` folder with these names, and add the demo video link.*

| Ticker | Start Launcher | Focus Mode Screen | Custom Theme |
|---|---|---|---|
| ![ticker](screenshots/ticker.png) | ![start](screenshots/start.png) | ![focus](screenshots/focus.png) | ![theme](screenshots/theme.png) |

🎥 **Demo Video:** [YouTube / Vimeo link](#)

---

## Technical Challenges

> *The Congressional App Challenge asks about a technical difficulty you faced and how you solved it (1,500 characters max). Likely candidates for this project:*

- **Rebuilding the taskbar:** Hiding the Windows taskbar and recreating the Start launcher, window switcher, and system tray means working with low-level Win32 shell APIs rather than a normal app window.
- **Merging deadline sources:** The school API, .ics files, and manual entries use different formats; matching duplicates (the same assignment from two sources) takes careful comparison of titles, classes, and due times.
- **Blocking websites across browsers:** Websites can be opened in any browser, so blocking has to work at the system level (hosts file) or through an extension.
- **Adjustable strictness:** Making Strict mode hard to bypass while keeping Gentle mode non-intrusive.
- **Always-on performance:** A taskbar runs all day, so the ticker and blocker must use very little CPU and memory.

*Draft:*

> [Write your answer here]

---

## Development Plan

| Phase | Goal | Status |
|---|---|---|
| 0 | Safety net: the Windows taskbar always comes back (exit, crash, kill, hotkey) | Built |
| 1 | Bar that docks at the bottom of the screen and hides the Windows taskbar | Built |
| 2 | Clock, system tray, pinned apps, open-windows switcher, Start launcher | Built |
| 3 | Manual deadlines + scrolling ticker with urgency colors | Built |
| 4 | Reminders | Built |
| 5 | .ics import and school website sync | Built |
| 6 | Focus Mode for apps, then websites, with strictness levels | Built |
| 7 | Image customization and themes | Built |
| 8 | Setup guide, Start with Windows, installer, final README | Built |
| Next | Testing on Windows with classmates ([checklist](docs/testing.md)), fixes, screenshots, demo video | To do |

---

## Future Ideas

- [ ] Google Classroom and Canvas/Schoology integration (Google Classroom calendars already work through the .ics option)
- [ ] Recurring deadlines (weekly homework)
- [ ] Study stats (time in Focus Mode, deadlines met on time)
- [ ] A bar on every monitor (today FocusBar is on the main monitor only)
- [ ] A browser extension, so website blocking works even with secure DNS turned on
- [ ] Shareable theme packs
- [ ] Parent/teacher view (opt-in)
- [ ] A code-signed installer, so Windows doesn't show the SmartScreen warning

---

## AI Usage Disclosure

The Congressional App Challenge permits AI tools as long as all use is fully disclosed.

A detailed, phase-by-phase record of what AI generated and what the developer wrote, changed, or decided is kept in [AI_LOG.md](AI_LOG.md).

> *Draft, based on the AI log. Check it and make it yours:*
> - AI tools used: Claude Code (Anthropic's AI coding assistant).
> - Used for: generating the code, unit tests, installer script, and documentation from the developer's concept and build instructions, one phase at a time, and translating the developer's school-website API guide and Python client into C#.
> - The developer: wrote the concept and the build instructions (features, rules, phases), provided the school website details, made the decisions recorded in the AI log, and tests every phase on Windows. *(Add what you changed or wrote yourself.)*

---

## Acknowledgments

- [ManagedShell](https://github.com/cairoshell/ManagedShell) (Apache License 2.0): the AppBar, task list, and tray services FocusBar is built on
- [RetroBar](https://github.com/dremin/RetroBar) (Apache License 2.0): an open-source taskbar built on ManagedShell, used as a reference for how to use it
- See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for the licenses of libraries FocusBar uses
- [Teachers, mentors, testers, or tutorials]

---

## License

[MIT](LICENSE) — *or whatever license you choose*
