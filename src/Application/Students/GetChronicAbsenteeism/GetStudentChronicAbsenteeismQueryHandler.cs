using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Attendance;
using Domain.Attendance;
using Domain.Students;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Students.GetChronicAbsenteeism;

internal sealed class GetStudentChronicAbsenteeismQueryHandler(
    IApplicationDbContext context,
    AttendanceOptions options,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetStudentChronicAbsenteeismQuery, StudentChronicAbsenteeismResponse>
{
    public async Task<Result<StudentChronicAbsenteeismResponse>> Handle(
        GetStudentChronicAbsenteeismQuery query,
        CancellationToken cancellationToken)
    {
        Result<SchoolYear> schoolYearResult = SchoolYearResolver.Resolve(query.SchoolYear, options, dateTimeProvider);

        if (schoolYearResult.IsFailure)
        {
            return Result.Failure<StudentChronicAbsenteeismResponse>(schoolYearResult.Error);
        }

        SchoolYear schoolYear = schoolYearResult.Value;

        int? schoolId = await context.Students
            .Where(s => s.Id == query.StudentId)
            .Select(s => (int?)s.SchoolId)
            .SingleOrDefaultAsync(cancellationToken);

        if (schoolId is null)
        {
            return Result.Failure<StudentChronicAbsenteeismResponse>(StudentErrors.NotFound(query.StudentId));
        }

        // Counted at the student's own school (ADR 0008, ADR 0011) with the shared ADR 0006 calculation;
        // no row means no absences.
        StudentAbsenceCount? count = await context.AttendanceRecords
            .Where(r => r.StudentId == query.StudentId)
            .CountAbsences(schoolId.Value, schoolYear)
            .SingleOrDefaultAsync(cancellationToken);

        int total = count?.Total ?? 0;
        int threshold = options.ChronicAbsenceThreshold;

        return new StudentChronicAbsenteeismResponse
        {
            StudentId = query.StudentId,
            SchoolId = schoolId.Value,
            SchoolYear = schoolYear.Label,
            Threshold = threshold,
            ExcusedAbsences = count?.Excused ?? 0,
            UnexcusedAbsences = count?.Unexcused ?? 0,
            TotalAbsences = total,
            IsChronicallyAbsent = AbsenceCounting.IsChronicallyAbsent(total, threshold)
        };
    }
}
