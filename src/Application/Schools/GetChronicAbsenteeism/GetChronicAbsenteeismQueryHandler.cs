using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Attendance;
using Domain.Attendance;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Schools.GetChronicAbsenteeism;

internal sealed class GetChronicAbsenteeismQueryHandler(
    IApplicationDbContext context,
    AttendanceOptions options,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetChronicAbsenteeismQuery, ChronicAbsenteeismResponse>
{
    public async Task<Result<ChronicAbsenteeismResponse>> Handle(
        GetChronicAbsenteeismQuery query,
        CancellationToken cancellationToken)
    {
        Result<SchoolYear> schoolYearResult = SchoolYearResolver.Resolve(query.SchoolYear, options, dateTimeProvider);

        if (schoolYearResult.IsFailure)
        {
            return Result.Failure<ChronicAbsenteeismResponse>(schoolYearResult.Error);
        }

        SchoolYear schoolYear = schoolYearResult.Value;
        int threshold = options.ChronicAbsenceThreshold;

        // Computed from the daily records on every request; nothing is stored (ADR 0006).
        // Counted per school (ADR 0008) and per school year (ADR 0005); excused and unexcused both count,
        // Late and Present don't (ADR 0003, ADR 0006).
        var absenceCounts = context.AttendanceRecords
            .Where(r => r.SchoolId == query.SchoolId &&
                        r.Date >= schoolYear.Start &&
                        r.Date < schoolYear.EndExclusive &&
                        (r.Status == AttendanceStatus.AbsentExcused || r.Status == AttendanceStatus.AbsentUnexcused))
            .GroupBy(r => r.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                Excused = g.Count(r => r.Status == AttendanceStatus.AbsentExcused),
                Unexcused = g.Count(r => r.Status == AttendanceStatus.AbsentUnexcused),
                Total = g.Count()
            })
            .Where(c => c.Total >= threshold);

        List<ChronicallyAbsentStudentResponse> students = await absenceCounts
            .Join(
                context.Students,
                c => c.StudentId,
                s => s.Id,
                (c, s) => new ChronicallyAbsentStudentResponse
                {
                    StudentId = s.Id,
                    FirstName = s.FirstName,
                    LastName = s.LastName,
                    ExcusedAbsences = c.Excused,
                    UnexcusedAbsences = c.Unexcused,
                    TotalAbsences = c.Total
                })
            .OrderByDescending(s => s.TotalAbsences)
            .ThenBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ToListAsync(cancellationToken);

        return new ChronicAbsenteeismResponse
        {
            SchoolId = query.SchoolId,
            SchoolYear = schoolYear.Label,
            Threshold = threshold,
            Students = students
        };
    }
}
