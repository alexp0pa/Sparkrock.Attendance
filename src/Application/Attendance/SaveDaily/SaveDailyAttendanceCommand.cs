using Application.Abstractions.Messaging;

namespace Application.Attendance.SaveDaily;

// Saves one date for many students, all or nothing (docs/adr/0004-saving-daily-attendance.md).
public sealed record SaveDailyAttendanceCommand(
    DateOnly Date,
    int SchoolId,
    IReadOnlyList<StudentAttendanceEntry> Records) : ICommand;

// Status arrives as a string and is checked by the validator (docs/adr/0010-enums-as-strings-in-api.md).
public sealed record StudentAttendanceEntry(int StudentId, string Status, int? MinutesLate, string? Notes);
