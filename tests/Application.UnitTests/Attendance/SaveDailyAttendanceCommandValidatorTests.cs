using Application.Attendance;
using Application.Attendance.SaveDaily;
using FluentValidation.TestHelper;
using SharedKernel;

namespace Application.UnitTests.Attendance;

public sealed class SaveDailyAttendanceCommandValidatorTests
{
    // Today is 2026-03-16, so the current school year (default start September 1) began on 2025-09-01.
    private static readonly DateOnly Today = new(2026, 3, 16);
    private static readonly DateOnly SchoolYearStart = new(2025, 9, 1);

    private readonly SaveDailyAttendanceCommandValidator _validator;

    public SaveDailyAttendanceCommandValidatorTests()
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(new DateTime(2026, 3, 16, 12, 0, 0, DateTimeKind.Utc));

        _validator = new SaveDailyAttendanceCommandValidator(dateTimeProvider, new AttendanceOptions());
    }

    [Fact]
    public void Validator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        SaveDailyAttendanceCommand command = Command(Today,
            new StudentAttendanceEntry(1, "Present", null, null),
            new StudentAttendanceEntry(2, "late", 10, "Bus"),
            new StudentAttendanceEntry(3, "AbsentExcused", null, "Dentist"),
            new StudentAttendanceEntry(4, "AbsentUnexcused", null, null));

        TestValidationResult<SaveDailyAttendanceCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("P")]
    [InlineData("1")]
    [InlineData("")]
    public void Validator_Should_HaveError_WhenStatusIsNotOneOfTheFixedSet(string status)
    {
        SaveDailyAttendanceCommand command = Command(Today, new StudentAttendanceEntry(1, status, null, null));

        TestValidationResult<SaveDailyAttendanceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor("Records[0].Status");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1441)]
    public void Validator_Should_HaveError_WhenLateHasNoValidMinutes(int? minutesLate)
    {
        SaveDailyAttendanceCommand command = Command(Today, new StudentAttendanceEntry(1, "Late", minutesLate, null));

        TestValidationResult<SaveDailyAttendanceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor("Records[0].MinutesLate");
    }

    [Theory]
    [InlineData("Present")]
    [InlineData("AbsentExcused")]
    [InlineData("AbsentUnexcused")]
    public void Validator_Should_HaveError_WhenMinutesGivenForStatusOtherThanLate(string status)
    {
        SaveDailyAttendanceCommand command = Command(Today, new StudentAttendanceEntry(1, status, 5, null));

        TestValidationResult<SaveDailyAttendanceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor("Records[0].MinutesLate");
    }

    [Fact]
    public void Validator_Should_HaveError_WhenStudentAppearsTwice()
    {
        SaveDailyAttendanceCommand command = Command(Today,
            new StudentAttendanceEntry(1, "Present", null, null),
            new StudentAttendanceEntry(1, "AbsentExcused", null, null));

        TestValidationResult<SaveDailyAttendanceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Records).WithErrorCode("Attendance.DuplicateStudent");
    }

    [Fact]
    public void Validator_Should_HaveError_WhenRecordsAreEmpty()
    {
        SaveDailyAttendanceCommand command = Command(Today);

        TestValidationResult<SaveDailyAttendanceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Records);
    }

    [Fact]
    public void Validator_Should_HaveError_WhenTooManyRecords()
    {
        StudentAttendanceEntry[] records = [.. Enumerable.Range(1, SaveDailyAttendanceCommandValidator.MaxRecords + 1)
            .Select(id => new StudentAttendanceEntry(id, "Present", null, null))];

        TestValidationResult<SaveDailyAttendanceCommand> result = _validator.TestValidate(Command(Today, records));

        result.ShouldHaveValidationErrorFor(c => c.Records).WithErrorCode("Attendance.TooManyRecords");
    }

    [Fact]
    public void Validator_Should_HaveError_WhenDateIsInTheFuture()
    {
        SaveDailyAttendanceCommand command = Command(
            Today.AddDays(1),
            new StudentAttendanceEntry(1, "Present", null, null));

        TestValidationResult<SaveDailyAttendanceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Date).WithErrorCode("Attendance.DateInFuture");
    }

    [Fact]
    public void Validator_Should_HaveError_WhenDateIsBeforeCurrentSchoolYear()
    {
        SaveDailyAttendanceCommand command = Command(
            SchoolYearStart.AddDays(-1),
            new StudentAttendanceEntry(1, "Present", null, null));

        TestValidationResult<SaveDailyAttendanceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Date).WithErrorCode("Attendance.DateBeforeSchoolYear");
    }

    [Fact]
    public void Validator_Should_AcceptFirstDayOfSchoolYear()
    {
        SaveDailyAttendanceCommand command = Command(
            SchoolYearStart,
            new StudentAttendanceEntry(1, "Present", null, null));

        TestValidationResult<SaveDailyAttendanceCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(c => c.Date);
    }

    [Fact]
    public void Validator_Should_HaveError_WhenNotesAreTooLong()
    {
        string notes = new('x', SaveDailyAttendanceCommandValidator.MaxNotesLength + 1);
        SaveDailyAttendanceCommand command = Command(Today, new StudentAttendanceEntry(1, "Present", null, notes));

        TestValidationResult<SaveDailyAttendanceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor("Records[0].Notes").WithErrorCode("Attendance.NotesTooLong");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validator_Should_HaveError_WhenSchoolOrStudentIdIsNotPositive(int id)
    {
        var command = new SaveDailyAttendanceCommand(Today, id, [new StudentAttendanceEntry(id, "Present", null, null)]);

        TestValidationResult<SaveDailyAttendanceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.SchoolId);
        result.ShouldHaveValidationErrorFor("Records[0].StudentId");
    }

    private static SaveDailyAttendanceCommand Command(DateOnly date, params StudentAttendanceEntry[] records) =>
        new(date, 7, records);
}
