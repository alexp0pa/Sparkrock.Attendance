using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Attendance.SaveDaily;
using Application.UnitTests.Abstractions;
using Domain.Attendance;
using Domain.Students;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Attendance;

public sealed class SaveDailyAttendanceCommandHandlerTests : BaseHandlerTest
{
    private const string TeacherEmail = "teacher@example.com";
    private const string OtherTeacherEmail = "other.teacher@example.com";
    private const int SchoolId = 7;
    private static readonly DateOnly Date = new(2026, 3, 16);
    private static readonly DateTime UtcNow = new(2026, 3, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_InsertRecordsWithCreatedAudit_WhenNoneExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context, "Ava");
        Student liam = await AddStudentAsync(context, "Liam");
        SaveDailyAttendanceCommandHandler handler = CreateHandler(context, TeacherEmail, UtcNow);

        var command = new SaveDailyAttendanceCommand(Date, SchoolId,
        [
            new StudentAttendanceEntry(ava.Id, "Late", 10, "Bus"),
            new StudentAttendanceEntry(liam.Id, "absentexcused", null, null)
        ]);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        AttendanceRecord avaRecord = await context.AttendanceRecords.SingleAsync(r => r.StudentId == ava.Id);
        avaRecord.Date.ShouldBe(Date);
        avaRecord.SchoolId.ShouldBe(SchoolId);
        avaRecord.Status.ShouldBe(AttendanceStatus.Late);
        avaRecord.MinutesLate.ShouldBe(10);
        avaRecord.Notes.ShouldBe("Bus");
        avaRecord.CreatedBy.ShouldBe(TeacherEmail);
        avaRecord.CreatedAtUtc.ShouldBe(UtcNow);
        avaRecord.ModifiedBy.ShouldBeNull();
        avaRecord.ModifiedAtUtc.ShouldBeNull();

        AttendanceRecord liamRecord = await context.AttendanceRecords.SingleAsync(r => r.StudentId == liam.Id);
        liamRecord.Status.ShouldBe(AttendanceStatus.AbsentExcused);
    }

    [Fact]
    public async Task Handle_Should_OverwriteExistingRecordAndSetModifiedAudit_WhenRecordExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context, "Ava");
        await AddRecordAsync(context, ava.Id, SchoolId);
        DateTime later = UtcNow.AddHours(2);
        SaveDailyAttendanceCommandHandler handler = CreateHandler(context, OtherTeacherEmail, later);

        var command = new SaveDailyAttendanceCommand(Date, SchoolId,
            [new StudentAttendanceEntry(ava.Id, "AbsentUnexcused", null, "No call")]);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        AttendanceRecord record = await context.AttendanceRecords.SingleAsync(r => r.StudentId == ava.Id);
        record.Status.ShouldBe(AttendanceStatus.AbsentUnexcused);
        record.Notes.ShouldBe("No call");
        record.CreatedBy.ShouldBe(TeacherEmail);
        record.CreatedAtUtc.ShouldBe(UtcNow);
        record.ModifiedBy.ShouldBe(OtherTeacherEmail);
        record.ModifiedAtUtc.ShouldBe(later);
    }

    // Regression for legacy bug B1 (sp_SaveDailyAttendance.sql:54-57): a new student following an existing one
    // overwrote the existing student's row instead of getting a row of their own.
    [Fact]
    public async Task Handle_Should_UpdateExistingAndInsertNew_WhenRequestMixesBoth()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student existing = await AddStudentAsync(context, "Existing");
        Student newcomer = await AddStudentAsync(context, "Newcomer");
        await AddRecordAsync(context, existing.Id, SchoolId);
        SaveDailyAttendanceCommandHandler handler = CreateHandler(context, TeacherEmail, UtcNow);

        var command = new SaveDailyAttendanceCommand(Date, SchoolId,
        [
            new StudentAttendanceEntry(existing.Id, "Late", 5, null),
            new StudentAttendanceEntry(newcomer.Id, "AbsentExcused", null, null)
        ]);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        List<AttendanceRecord> records = await context.AttendanceRecords.ToListAsync();
        records.Count.ShouldBe(2);
        records.Single(r => r.StudentId == existing.Id).Status.ShouldBe(AttendanceStatus.Late);
        records.Single(r => r.StudentId == newcomer.Id).Status.ShouldBe(AttendanceStatus.AbsentExcused);
    }

    [Fact]
    public async Task Handle_Should_MoveRecordToNewSchool_WhenSavedFromAnotherSchool()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context, "Ava");
        await AddRecordAsync(context, ava.Id, SchoolId);
        SaveDailyAttendanceCommandHandler handler = CreateHandler(context, TeacherEmail, UtcNow);

        var command = new SaveDailyAttendanceCommand(Date, SchoolId + 1,
            [new StudentAttendanceEntry(ava.Id, "Present", null, null)]);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        AttendanceRecord record = await context.AttendanceRecords.SingleAsync();
        record.SchoolId.ShouldBe(SchoolId + 1);
    }

    [Fact]
    public async Task Handle_Should_StoreTrimmedNotesAndNullForBlankNotes()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context, "Ava");
        Student liam = await AddStudentAsync(context, "Liam");
        SaveDailyAttendanceCommandHandler handler = CreateHandler(context, TeacherEmail, UtcNow);

        var command = new SaveDailyAttendanceCommand(Date, SchoolId,
        [
            new StudentAttendanceEntry(ava.Id, "Present", null, "   "),
            new StudentAttendanceEntry(liam.Id, "Present", null, "  Bus  ")
        ]);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.AttendanceRecords.SingleAsync(r => r.StudentId == ava.Id)).Notes.ShouldBeNull();
        (await context.AttendanceRecords.SingleAsync(r => r.StudentId == liam.Id)).Notes.ShouldBe("Bus");
    }

    [Fact]
    public async Task Handle_Should_ReturnStudentsNotFoundAndSaveNothing_WhenAStudentIsUnknown()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context, "Ava");
        SaveDailyAttendanceCommandHandler handler = CreateHandler(context, TeacherEmail, UtcNow);
        const int unknownStudentId = 999;

        var command = new SaveDailyAttendanceCommand(Date, SchoolId,
        [
            new StudentAttendanceEntry(ava.Id, "Present", null, null),
            new StudentAttendanceEntry(unknownStudentId, "Present", null, null)
        ]);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AttendanceErrors.StudentsNotFound([unknownStudentId]));
        (await context.AttendanceRecords.AnyAsync()).ShouldBeFalse();
    }

    private static SaveDailyAttendanceCommandHandler CreateHandler(TestDbContext context, string email, DateTime utcNow)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.Email.Returns(email);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(utcNow);

        return new SaveDailyAttendanceCommandHandler(
            context, userContext, dateTimeProvider, Substitute.For<IDbExceptionClassifier>());
    }

    private static async Task<Student> AddStudentAsync(TestDbContext context, string firstName)
    {
        var student = new Student { FirstName = firstName, LastName = "Test" };
        context.Students.Add(student);
        await context.SaveChangesAsync();

        return student;
    }

    private static async Task AddRecordAsync(TestDbContext context, int studentId, int schoolId)
    {
        context.AttendanceRecords.Add(new AttendanceRecord
        {
            StudentId = studentId,
            SchoolId = schoolId,
            Date = Date,
            Status = AttendanceStatus.Present,
            CreatedBy = TeacherEmail,
            CreatedAtUtc = UtcNow
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }
}
