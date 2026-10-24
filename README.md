# FocusBar — A Smart Taskbar for Students

> A full replacement for the Windows taskbar that keeps every deadline scrolling in view, reminds you before they sneak up, and blocks distracting apps and websites when it's crunch time.

**Congressional App Challenge 2026 — Project Concept**
**Developer:** Suhaas
**Platform:** Windows 10 / 11
**Audience:** Middle and high school students
**Status:** 🟠 In development: Phase 0 (safety net) and Phase 1 (docked bar) built, awaiting testing on Windows

---

## Table of Contents

1. [The Problem](#the-problem)
2. [The Solution](#the-solution)
3. [Features](#features)
4. [How It Works](#how-it-works)
5. [Tech Stack](#tech-stack)
6. [Building and Running](#building-and-running)
7. [If Your Taskbar Disappears](#if-your-taskbar-disappears)
8. [User Walkthrough](#user-walkthrough)
9. [Screenshots & Demo](#screenshots--demo)
10. [Technical Challenges](#technical-challenges)
11. [Development Plan](#development-plan)
12. [Future Ideas](#future-ideas)
13. [AI Usage Disclosure](#ai-usage-disclosure)
14. [Acknowledgments](#acknowledgments)

---

## The Problem

Middle and high schoolers juggle deadlines spread across their school portal, Google Calendar, planners, and sticky notes — but none of those are visible while they're actually working on their computer. Assignments get buried under open windows, and distractions like games and YouTube are always one click away. By the time a student remembers an assignment, it's often too late to do their best work.

## The Solution

FocusBar replaces the Windows taskbar entirely — the one part of the screen students look at dozens of times an hour. It keeps everything a normal taskbar does (Start menu, open windows, pinned apps, clock, Wi-Fi, volume, battery) and adds a scrolling ticker of every upcoming deadline, pulled automatically from the school's website and the student's calendar. When a deadline gets close, it sends reminders and can lock out distracting apps and websites. Students can make it their own with custom images, so it feels like a personal tool rather than another nagging app.

---

## Features

### 🖥️ Full Taskbar Replacement
FocusBar hides the default Windows taskbar and replaces every core feature:
- **Start button / app launcher** — search and open installed apps
- **Open-windows switcher** — see and switch between running apps
- **Pinned apps** — one-click shortcuts to favorite programs
- **Clock and system tray** — time, date, Wi-Fi, volume, battery, and tray icons
- One click restores the standard Windows taskbar at any time

### 📅 Deadline Ticker
- A **scrolling ticker** cycles through all upcoming deadlines with live countdowns
- **Color-coded urgency** — green → yellow → red as each deadline approaches
- Hover or click a deadline to pause the ticker and see details (class, description, due time)
- Shows the student's **current/next class** from their schedule

### 🔄 Deadline Sources
Deadlines flow in from three places and merge into one list:
| Source | How it works |
|---|---|
| **School website API** | Automatically imports assignments, due dates, and the class schedule |
| **Calendar (.ics)** | Import or subscribe to a calendar file (Google Calendar, Outlook, etc.) |
| **Manual entry** | Quick-add a deadline right from the bar |

Duplicate deadlines from different sources are detected and merged.

### 🔔 Smart Reminders
- Native Windows notifications
- Configurable timing (e.g., 1 week, 1 day, 3 hours, 1 hour before)
- Snooze or mark complete straight from the notification

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

### 🎨 Customization with Images
Students can upload their own images for:
- **Bar background**
- **Start button icon**
- **Focus Mode screen** (shown when something is blocked)

Plus colors, fonts, transparency, and bar height — saved as switchable themes.

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
| App blocking | Process-start events (WMI), with polling fallback | Detects blocked apps without constant polling |
| Website blocking | Hosts-file rules, via a small helper that asks for admin only when needed | Blocks sites across browsers |
| Calendar import | Ical.Net | Parses .ics files |
| School data | School website API (HTTP/JSON) | Assignments, due dates, schedule |
| Storage | JSON in `%AppData%\FocusBar` | Simple, local, private |
| Tests | xUnit | Covers all non-UI logic in `FocusBar.Core` |

### Project layout

```
FocusBar.sln
├── src/FocusBar.App/         WPF app: the bar, safety net, (later) ticker, Focus Mode, settings
├── src/FocusBar.Core/        Models and logic with no UI code, so it can be unit-tested
└── tests/FocusBar.Core.Tests xUnit tests for FocusBar.Core
```

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

FocusBar's files live in `%AppData%\FocusBar\`. Error logs are in the `logs` folder there (right-click the bar → **Open log folder**).

---

## If Your Taskbar Disappears

FocusBar is built to always give the Windows taskbar back: on normal exit, on a crash, when Windows signs out, and when you press **Ctrl+Alt+Shift+F**. If FocusBar is killed from Task Manager, a small helper process (a second `FocusBar` entry in Task Manager) restores the taskbar for it.

If the taskbar is still missing (for example, both FocusBar processes were ended with "End process tree"), try these in order:

1. **Start FocusBar again.** On launch it notices it didn't clean up last time and restores the taskbar first. Then exit it normally.
2. Run `FocusBar.exe --restore-taskbar` (from the folder FocusBar.exe is in). It restores the taskbar and your original auto-hide setting without starting the bar.
3. Press **Ctrl+Shift+Esc** to open Task Manager, find **Windows Explorer**, and choose **Restart**. If the taskbar now hides itself until you point at the bottom of the screen, turn off **Settings → Personalization → Taskbar → Automatically hide the taskbar**.

---

## User Walkthrough

1. **First launch** — FocusBar replaces the taskbar; a setup screen walks through the rest.
2. **Connect deadlines** — Sign in to the school website, import a calendar, or add deadlines by hand.
3. **Set reminders** — Pick when to be notified (or keep the defaults).
4. **Set up Focus Mode** — Choose apps and websites to block, how early blocking starts, and the strictness level.
5. **Customize** — Upload a background, Start button icon, and Focus screen image.
6. **Work** — Deadlines scroll by, reminders pop up on time, and distractions lock when it counts.

---

## Screenshots & Demo

> *Add mockups or screenshots and your demo video link here.*

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

| Phase | Goal |
|---|---|
| 0 | Safety net: the Windows taskbar always comes back (exit, crash, kill, hotkey) |
| 1 | Bar that docks at the bottom of the screen and hides the Windows taskbar |
| 2 | Clock, system tray, pinned apps, open-windows switcher, Start launcher |
| 3 | Manual deadlines + scrolling ticker with urgency colors |
| 4 | Reminders |
| 5 | .ics import and school API sync |
| 6 | Focus Mode for apps, then websites, with strictness levels |
| 7 | Image customization and themes |
| 8 | Testing with classmates, polish, demo video |

---

## Future Ideas

- [ ] Google Classroom and Canvas/Schoology integration
- [ ] Recurring deadlines (weekly homework)
- [ ] Study stats (time in Focus Mode, deadlines met on time)
- [ ] Multi-monitor support
- [ ] Shareable theme packs
- [ ] Parent/teacher view (opt-in)

---

## AI Usage Disclosure

The Congressional App Challenge permits AI tools as long as all use is fully disclosed.

A detailed, phase-by-phase record of what AI generated and what the developer wrote, changed, or decided is kept in [AI_LOG.md](AI_LOG.md).

> *Fill in honestly, e.g.:*
> - AI tools used: [tool names]
> - Used for: [e.g., drafting this README, planning features, debugging]
> - Not used for: [e.g., core logic, UI design]

---

## Acknowledgments

- [ManagedShell](https://github.com/cairoshell/ManagedShell) (Apache License 2.0): the AppBar, task list, and tray services FocusBar is built on
- [RetroBar](https://github.com/dremin/RetroBar) (Apache License 2.0): an open-source taskbar built on ManagedShell, used as a reference for how to use it
- See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for the licenses of libraries FocusBar uses
- [Teachers, mentors, testers, or tutorials]

---

## License

[MIT](LICENSE) — *or whatever license you choose*
