using Domain.Attendance;
using FluentValidation;
using SharedKernel;

namespace Application.Attendance.SaveDaily;

internal sealed class SaveDailyAttendanceCommandValidator : AbstractValidator<SaveDailyAttendanceCommand>
{
    internal const int MaxRecords = 1000;
    internal const int MaxMinutesLate = 1440;
    internal const int MaxNotesLength = 500;

    public SaveDailyAttendanceCommandValidator(IDateTimeProvider dateTimeProvider, AttendanceOptions options)
    {
        RuleFor(c => c.SchoolId).GreaterThan(0);

        // Any date in the current school year up to and including today (ADR 0004, ADR 0005).
        RuleFor(c => c.Date)
            .Must(date => date <= Today(dateTimeProvider))
            .WithErrorCode("Attendance.DateInFuture")
            .WithMessage("Attendance can't be recorded for a future date.")
            .Must(date => date >= CurrentSchoolYear(dateTimeProvider, options).Start)
            .WithErrorCode("Attendance.DateBeforeSchoolYear")
            .WithMessage("Attendance can only be recorded for dates in the current school year.");

        RuleFor(c => c.Records)
            .NotEmpty()
            .Must(records => records.Count <= MaxRecords)
            .WithErrorCode("Attendance.TooManyRecords")
            .WithMessage($"At most {MaxRecords} records can be saved in one request.")
            .Must(records => records.Select(r => r.StudentId).Distinct().Count() == records.Count)
            .WithErrorCode("Attendance.DuplicateStudent")
            .WithMessage("Each student can appear only once per request.");

        RuleForEach(c => c.Records).ChildRules(record =>
        {
            record.RuleFor(r => r.StudentId).GreaterThan(0);

            // Fixed set of statuses (ADR 0003), case-insensitive names (ADR 0010).
            record.RuleFor(r => r.Status)
                .NotEmpty()
                .IsEnumName(typeof(AttendanceStatus), caseSensitive: false);

            // MinutesLate only with Late, and then required (ADR 0003).
            record.RuleFor(r => r.MinutesLate)
                .NotNull()
                .InclusiveBetween(1, MaxMinutesLate)
                .When(r => IsLate(r.Status));

            record.RuleFor(r => r.MinutesLate)
                .Null()
                .WithMessage("Minutes late can only be given for the Late status.")
                .When(r => !IsLate(r.Status));

            // Checked after trimming, as the handler stores trimmed notes.
            record.RuleFor(r => r.Notes)
                .Must(notes => notes is null || notes.Trim().Length <= MaxNotesLength)
                .WithErrorCode("Attendance.NotesTooLong")
                .WithMessage($"Notes can be at most {MaxNotesLength} characters.");
        });
    }

    private static DateOnly Today(IDateTimeProvider dateTimeProvider) =>
        DateOnly.FromDateTime(dateTimeProvider.UtcNow);

    private static SchoolYear CurrentSchoolYear(IDateTimeProvider dateTimeProvider, AttendanceOptions options) =>
        SchoolYear.Containing(Today(dateTimeProvider), options.SchoolYearStartMonth, options.SchoolYearStartDay);

    private static bool IsLate(string? status) =>
        Enum.TryParse(status, ignoreCase: true, out AttendanceStatus parsed) && parsed == AttendanceStatus.Late;
}
