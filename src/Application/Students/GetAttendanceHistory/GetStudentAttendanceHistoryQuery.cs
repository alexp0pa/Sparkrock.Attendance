using Application.Abstractions.Messaging;

namespace Application.Students.GetAttendanceHistory;

// SchoolYear is 'YYYY-YYYY'; null means the current school year.
public sealed record GetStudentAttendanceHistoryQuery(int StudentId, string? SchoolYear)
    : IQuery<StudentAttendanceHistoryResponse>;
