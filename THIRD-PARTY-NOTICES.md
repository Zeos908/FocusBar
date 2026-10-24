# Third-Party Notices

FocusBar uses the following open-source libraries. Their licenses require these notices to be kept with copies of FocusBar.

| Library | Used for | License |
|---|---|---|
| [ManagedShell](https://github.com/cairoshell/ManagedShell) | AppBar docking, task list, notification area | [Apache License 2.0](https://github.com/cairoshell/ManagedShell/blob/master/LICENSE) |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MVVM helpers | [MIT](https://github.com/CommunityToolkit/dotnet/blob/main/License.md) |
| Microsoft.Windows.CsWinRT, Microsoft.Windows.SDK.NET (pulled in by ManagedShell) | Windows Runtime interop | MIT |

Used as a reference:

| Project | How it was used | License |
|---|---|---|
| [RetroBar](https://github.com/dremin/RetroBar) | Read as an example of using ManagedShell. FocusBar's own code follows RetroBar's approach in these places (no files copied): starting ManagedShell and hiding the Windows taskbar; reopening the bar and restarting the window list after Explorer restarts (`App.xaml.cs`); passing mouse input to other apps' tray icons and reporting the tray's position (`Tray/TrayIconView.xaml.cs`, `Shell/MainBar.xaml.cs`); and remembering a window's state on mouse-down to decide between switching and minimizing (`Shell/TaskStrip.xaml.cs`). | [Apache License 2.0](https://github.com/dremin/RetroBar/blob/master/LICENSE) |

Anything adapted from RetroBar is credited here and in a comment next to that code, as the Apache License requires.

The installer (Phase 8) must include the full license texts.
