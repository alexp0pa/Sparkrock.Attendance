using Domain.Attendance;

namespace Application.Attendance;

// The single implementation of the chronic absenteeism rule (docs/adr/0006-chronic-absenteeism.md),
// shared by the school list and the per-student status.
internal static class AbsenceCounting
{
    // Absent days per student at one school (ADR 0008) in one school year (ADR 0005), computed from the daily
    // records on every call. Excused and unexcused both count; Late and Present don't (ADR 0003).
    public static IQueryable<StudentAbsenceCount> CountAbsences(
        this IQueryable<AttendanceRecord> records,
        int schoolId,
        SchoolYear schoolYear) =>
        records
            .Where(r => r.SchoolId == schoolId &&
                        r.Date >= schoolYear.Start &&
                        r.Date < schoolYear.EndExclusive &&
                        (r.Status == AttendanceStatus.AbsentExcused || r.Status == AttendanceStatus.AbsentUnexcused))
            .GroupBy(r => r.StudentId)
            .Select(g => new StudentAbsenceCount
            {
                StudentId = g.Key,
                Excused = g.Count(r => r.Status == AttendanceStatus.AbsentExcused),
                Unexcused = g.Count(r => r.Status == AttendanceStatus.AbsentUnexcused),
                Total = g.Count()
            });

    // A student is chronically absent once their absent days reach the configured threshold.
    // The two forms below must stay identical: one runs in SQL (lists), the other in memory (single student).
    public static IQueryable<StudentAbsenceCount> WhereChronicallyAbsent(
        this IQueryable<StudentAbsenceCount> counts,
        int threshold) =>
        counts.Where(c => c.Total >= threshold);

    public static bool IsChronicallyAbsent(int totalAbsences, int threshold) => totalAbsences >= threshold;
}

internal sealed class StudentAbsenceCount
{
    public int StudentId { get; init; }
    public int Excused { get; init; }
    public int Unexcused { get; init; }
    public int Total { get; init; }
}
