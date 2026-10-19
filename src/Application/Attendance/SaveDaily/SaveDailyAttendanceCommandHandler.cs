using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Attendance;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Attendance.SaveDaily;

internal sealed class SaveDailyAttendanceCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider,
    IDbExceptionClassifier dbExceptionClassifier)
    : ICommandHandler<SaveDailyAttendanceCommand>
{
    public async Task<Result> Handle(SaveDailyAttendanceCommand command, CancellationToken cancellationToken)
    {
        int[] studentIds = [.. command.Records.Select(r => r.StudentId)];

        int[] knownStudentIds = await context.Students
            .Where(s => studentIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToArrayAsync(cancellationToken);

        int[] unknownStudentIds = [.. studentIds.Except(knownStudentIds)];

        // The whole request is rejected; nothing is written (ADR 0004, ADR 0009).
        if (unknownStudentIds.Length > 0)
        {
            return Result.Failure(AttendanceErrors.StudentsNotFound(unknownStudentIds));
        }

        // One record per student per date, whatever the school (ADR 0004, ADR 0008).
        Dictionary<int, AttendanceRecord> existingRecords = await context.AttendanceRecords
            .Where(r => r.Date == command.Date && studentIds.Contains(r.StudentId))
            .ToDictionaryAsync(r => r.StudentId, cancellationToken);

        string userEmail = userContext.Email;
        DateTime utcNow = dateTimeProvider.UtcNow;

        foreach (StudentAttendanceEntry entry in command.Records)
        {
            AttendanceStatus status = Enum.Parse<AttendanceStatus>(entry.Status, ignoreCase: true);
            string? notes = string.IsNullOrWhiteSpace(entry.Notes) ? null : entry.Notes.Trim();

            if (existingRecords.TryGetValue(entry.StudentId, out AttendanceRecord? record))
            {
                record.SchoolId = command.SchoolId;
                record.Status = status;
                record.MinutesLate = entry.MinutesLate;
                record.Notes = notes;
                record.ModifiedBy = userEmail;
                record.ModifiedAtUtc = utcNow;
            }
            else
            {
                context.AttendanceRecords.Add(new AttendanceRecord
                {
                    StudentId = entry.StudentId,
                    SchoolId = command.SchoolId,
                    Date = command.Date,
                    Status = status,
                    MinutesLate = entry.MinutesLate,
                    Notes = notes,
                    CreatedBy = userEmail,
                    CreatedAtUtc = utcNow
                });
            }
        }

        // A single SaveChanges runs in one transaction: all records are written or none (ADR 0004).
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (dbExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            // Another request inserted a record for the same student and date first (unique index);
            // any other database failure is a real fault and reaches the global exception handler.
            return Result.Failure(AttendanceErrors.ConcurrentSave);
        }

        return Result.Success();
    }
}
