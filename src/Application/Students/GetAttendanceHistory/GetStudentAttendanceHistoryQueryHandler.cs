using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Attendance;
using Domain.Attendance;
using Domain.Students;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Students.GetAttendanceHistory;

internal sealed class GetStudentAttendanceHistoryQueryHandler(
    IApplicationDbContext context,
    AttendanceOptions options,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetStudentAttendanceHistoryQuery, StudentAttendanceHistoryResponse>
{
    public async Task<Result<StudentAttendanceHistoryResponse>> Handle(
        GetStudentAttendanceHistoryQuery query,
        CancellationToken cancellationToken)
    {
        Result<SchoolYear> schoolYearResult = SchoolYearResolver.Resolve(query.SchoolYear, options, dateTimeProvider);

        if (schoolYearResult.IsFailure)
        {
            return Result.Failure<StudentAttendanceHistoryResponse>(schoolYearResult.Error);
        }

        SchoolYear schoolYear = schoolYearResult.Value;

        StudentAttendanceHistoryResponse? history = await context.Students
            .Where(s => s.Id == query.StudentId)
            .Select(s => new StudentAttendanceHistoryResponse
            {
                StudentId = s.Id,
                FirstName = s.FirstName,
                LastName = s.LastName,
                SchoolYear = schoolYear.Label
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (history is null)
        {
            return Result.Failure<StudentAttendanceHistoryResponse>(StudentErrors.NotFound(query.StudentId));
        }

        // A date range on the record's own date (ADR 0005), records from every school (ADR 0008), newest first.
        List<AttendanceHistoryRecordResponse> records = await context.AttendanceRecords
            .Where(r => r.StudentId == query.StudentId &&
                        r.Date >= schoolYear.Start &&
                        r.Date < schoolYear.EndExclusive)
            .OrderByDescending(r => r.Date)
            .Select(r => new AttendanceHistoryRecordResponse
            {
                Date = r.Date,
                SchoolId = r.SchoolId,
                Status = r.Status,
                MinutesLate = r.MinutesLate,
                Notes = r.Notes
            })
            .ToListAsync(cancellationToken);

        return history with { Records = records };
    }
}
