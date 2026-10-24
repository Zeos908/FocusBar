namespace FocusBar.Core.Models;

public record ClassPeriod(string ClassName, DayOfWeek Day, TimeOnly Start, TimeOnly End, string? Room);
