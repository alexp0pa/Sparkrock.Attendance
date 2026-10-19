using Domain.Attendance;
using SharedKernel;

namespace Application.Attendance;

// Turns an optional 'YYYY-YYYY' query value into a SchoolYear: the current year when omitted,
// using the configured start (docs/adr/0005-school-year.md).
internal static class SchoolYearResolver
{
    public static Result<SchoolYear> Resolve(
        string? schoolYear,
        AttendanceOptions options,
        IDateTimeProvider dateTimeProvider)
    {
        if (string.IsNullOrWhiteSpace(schoolYear))
        {
            return SchoolYear.Containing(
                DateOnly.FromDateTime(dateTimeProvider.UtcNow),
                options.SchoolYearStartMonth,
                options.SchoolYearStartDay);
        }

        return SchoolYear.TryParse(schoolYear, options.SchoolYearStartMonth, options.SchoolYearStartDay, out SchoolYear? parsed)
            ? parsed!
            : Result.Failure<SchoolYear>(AttendanceErrors.InvalidSchoolYear(schoolYear));
    }
}
