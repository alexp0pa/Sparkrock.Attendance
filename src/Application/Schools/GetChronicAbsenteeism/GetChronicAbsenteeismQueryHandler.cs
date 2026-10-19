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

        // Shared ADR 0006 calculation and threshold rule, filtered in SQL.
        List<ChronicallyAbsentStudentResponse> students = await context.AttendanceRecords
            .CountAbsences(query.SchoolId, schoolYear)
            .WhereChronicallyAbsent(threshold)
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
