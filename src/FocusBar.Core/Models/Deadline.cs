namespace FocusBar.Core.Models;

public record Deadline(
    Guid Id,
    string Title,
    string? ClassName,
    string? Description,
    DateTimeOffset DueAt,
    DeadlineSource Source,
    string? ExternalId,      // ID from the school API or the .ics UID
    bool IsComplete);
