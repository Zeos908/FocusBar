namespace FocusBar.Core.Safety;

/// <summary>
/// Written to disk while FocusBar has the Windows taskbar hidden, and deleted once it is restored.
/// If this file still exists when nothing owns it, FocusBar went away without cleaning up
/// and the taskbar must be restored using <see cref="OriginalAppBarState"/>.
/// </summary>
/// <param name="OriginalAppBarState">
/// The Windows taskbar's <c>ABM_GETSTATE</c> value before FocusBar touched it
/// (bit 0x1 = auto-hide, bit 0x2 = always on top). Restored so the user's own auto-hide setting comes back.
/// </param>
/// <param name="OwnerProcessId">The FocusBar process that hid the taskbar.</param>
/// <param name="OwnerStartTime">That process's start time, so a reused process ID isn't mistaken for it.</param>
/// <param name="HiddenAt">When the taskbar was hidden, for the log.</param>
public record TaskbarSafetyState(
    int OriginalAppBarState,
    int OwnerProcessId,
    DateTimeOffset OwnerStartTime,
    DateTimeOffset HiddenAt);
