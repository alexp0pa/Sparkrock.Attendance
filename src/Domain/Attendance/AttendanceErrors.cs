using SharedKernel;

namespace Domain.Attendance;

public static class AttendanceErrors
{
    public static Error StudentsNotFound(IEnumerable<int> studentIds) => Error.Problem(
        "Attendance.StudentsNotFound",
        $"The students with the Ids = '{string.Join(", ", studentIds)}' were not found");

    public static Error InvalidSchoolYear(string schoolYear) => Error.Problem(
        "Attendance.InvalidSchoolYear",
        $"The school year '{schoolYear}' is invalid; expected the format 'YYYY-YYYY', e.g. '2025-2026'");

    public static readonly Error ConcurrentSave = Error.Conflict(
        "Attendance.ConcurrentSave",
        "Attendance for this date was saved concurrently by another request; retry the request");
}
