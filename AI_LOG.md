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
