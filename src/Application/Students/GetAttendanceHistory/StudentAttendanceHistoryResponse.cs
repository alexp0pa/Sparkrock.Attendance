using Domain.Attendance;

namespace Application.Students.GetAttendanceHistory;

public sealed record StudentAttendanceHistoryResponse
{
    public int StudentId { get; init; }
    public string FirstName { get; init; }
    public string LastName { get; init; }
    public string SchoolYear { get; init; }
    public List<AttendanceHistoryRecordResponse> Records { get; init; } = [];
}

public sealed record AttendanceHistoryRecordResponse
{
    public DateOnly Date { get; init; }
    public int SchoolId { get; init; }
    public AttendanceStatus Status { get; init; }
    public int? MinutesLate { get; init; }
    public string? Notes { get; init; }
}
