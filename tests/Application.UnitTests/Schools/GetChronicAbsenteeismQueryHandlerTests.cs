using Application.Attendance;
using Application.Schools.GetChronicAbsenteeism;
using Application.UnitTests.Abstractions;
using Domain.Attendance;
using Domain.Students;
using SharedKernel;

namespace Application.UnitTests.Schools;

public sealed class GetChronicAbsenteeismQueryHandlerTests : BaseHandlerTest
{
    private const int SchoolId = 7;
    private const string SchoolYear = "2025-2026";

    // Today is 2026-03-16: with the default start (September 1) the current school year is 2025-2026.
    private static readonly DateTime UtcNow = new(2026, 3, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly FirstDay = new(2025, 9, 1);

    [Fact]
    public async Task Handle_Should_ListStudentOnlyWhenAbsencesReachThreshold()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student atThreshold = await AddStudentAsync(context, "At");
        Student belowThreshold = await AddStudentAsync(context, "Below");
        await AddRecordsAsync(context, atThreshold.Id, SchoolId, AttendanceStatus.AbsentUnexcused, 3);
        await AddRecordsAsync(context, belowThreshold.Id, SchoolId, AttendanceStatus.AbsentUnexcused, 2);
        GetChronicAbsenteeismQueryHandler handler = CreateHandler(context, threshold: 3);

        // Act
        Result<ChronicAbsenteeismResponse> result = await handler.Handle(
            new GetChronicAbsenteeismQuery(SchoolId, SchoolYear),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Threshold.ShouldBe(3);
        result.Value.SchoolYear.ShouldBe(SchoolYear);
        result.Value.Students.ShouldHaveSingleItem().StudentId.ShouldBe(atThreshold.Id);
    }

    [Fact]
    public async Task Handle_Should_CountExcusedAndUnexcusedAndReportThemSeparately()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context, "Ava");
        await AddRecordsAsync(context, ava.Id, SchoolId, AttendanceStatus.AbsentExcused, 2);
        await AddRecordsAsync(context, ava.Id, SchoolId, AttendanceStatus.AbsentUnexcused, 1, startOffset: 10);
        GetChronicAbsenteeismQueryHandler handler = CreateHandler(context, threshold: 3);

        // Act
        Result<ChronicAbsenteeismResponse> result = await handler.Handle(
            new GetChronicAbsenteeismQuery(SchoolId, SchoolYear),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        ChronicallyAbsentStudentResponse student = result.Value.Students.ShouldHaveSingleItem();
        student.ExcusedAbsences.ShouldBe(2);
        student.UnexcusedAbsences.ShouldBe(1);
        student.TotalAbsences.ShouldBe(3);
        student.FirstName.ShouldBe("Ava");
    }

    [Fact]
    public async Task Handle_Should_NotCountLateOrPresent()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context, "Ava");
        await AddRecordsAsync(context, ava.Id, SchoolId, AttendanceStatus.AbsentUnexcused, 2);
        await AddRecordsAsync(context, ava.Id, SchoolId, AttendanceStatus.Late, 5, startOffset: 10);
        await AddRecordsAsync(context, ava.Id, SchoolId, AttendanceStatus.Present, 5, startOffset: 20);
        GetChronicAbsenteeismQueryHandler handler = CreateHandler(context, threshold: 3);

        // Act
        Result<ChronicAbsenteeismResponse> result = await handler.Handle(
            new GetChronicAbsenteeismQuery(SchoolId, SchoolYear),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Students.ShouldBeEmpty();
    }

    // Regression for legacy bug B15 (sp_SaveDailyAttendance.sql:83): the recount covered every school year.
    [Fact]
    public async Task Handle_Should_CountOnlyTheRequestedSchoolYear()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context, "Ava");
        await AddRecordsAsync(context, ava.Id, SchoolId, AttendanceStatus.AbsentUnexcused, 2);
        await AddRecordsAsync(context, ava.Id, SchoolId, AttendanceStatus.AbsentUnexcused, 5, startOffset: -30);
        GetChronicAbsenteeismQueryHandler handler = CreateHandler(context, threshold: 3);

        // Act
        Result<ChronicAbsenteeismResponse> result = await handler.Handle(
            new GetChronicAbsenteeismQuery(SchoolId, SchoolYear),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Students.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_Should_CountOnlyAbsencesAtTheRequestedSchool()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context, "Ava");
        await AddRecordsAsync(context, ava.Id, SchoolId, AttendanceStatus.AbsentUnexcused, 2);
        await AddRecordsAsync(context, ava.Id, SchoolId + 1, AttendanceStatus.AbsentUnexcused, 2, startOffset: 10);
        GetChronicAbsenteeismQueryHandler handler = CreateHandler(context, threshold: 3);

        // Act
        Result<ChronicAbsenteeismResponse> result = await handler.Handle(
            new GetChronicAbsenteeismQuery(SchoolId, SchoolYear),
            CancellationToken.None);

        // Assert: 4 absences in total, but only 2 at this school (ADR 0008).
        result.IsSuccess.ShouldBeTrue();
        result.Value.Students.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_Should_UseConfiguredThreshold()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context, "Ava");
        await AddRecordsAsync(context, ava.Id, SchoolId, AttendanceStatus.AbsentUnexcused, 9);
        GetChronicAbsenteeismQueryHandler defaultHandler = CreateHandler(context, new AttendanceOptions().ChronicAbsenceThreshold);
        GetChronicAbsenteeismQueryHandler lowerHandler = CreateHandler(context, threshold: 9);

        // Act
        Result<ChronicAbsenteeismResponse> withDefault = await defaultHandler.Handle(
            new GetChronicAbsenteeismQuery(SchoolId, SchoolYear),
            CancellationToken.None);
        Result<ChronicAbsenteeismResponse> withLower = await lowerHandler.Handle(
            new GetChronicAbsenteeismQuery(SchoolId, SchoolYear),
            CancellationToken.None);

        // Assert
        withDefault.IsSuccess.ShouldBeTrue();
        withDefault.Value.Threshold.ShouldBe(10);
        withDefault.Value.Students.ShouldBeEmpty();
        withLower.IsSuccess.ShouldBeTrue();
        withLower.Value.Students.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_Should_OrderByTotalDescendingThenName()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student most = await AddStudentAsync(context, "Most", "Zed");
        Student tieB = await AddStudentAsync(context, "Tie", "Brown");
        Student tieA = await AddStudentAsync(context, "Tie", "Adams");
        await AddRecordsAsync(context, most.Id, SchoolId, AttendanceStatus.AbsentUnexcused, 4);
        await AddRecordsAsync(context, tieB.Id, SchoolId, AttendanceStatus.AbsentUnexcused, 3);
        await AddRecordsAsync(context, tieA.Id, SchoolId, AttendanceStatus.AbsentUnexcused, 3);
        GetChronicAbsenteeismQueryHandler handler = CreateHandler(context, threshold: 3);

        // Act
        Result<ChronicAbsenteeismResponse> result = await handler.Handle(
            new GetChronicAbsenteeismQuery(SchoolId, SchoolYear),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Students.Select(s => s.StudentId).ShouldBe([most.Id, tieA.Id, tieB.Id]);
    }

    [Fact]
    public async Task Handle_Should_DefaultToCurrentSchoolYear_WhenNoneIsGiven()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context, "Ava");
        await AddRecordsAsync(context, ava.Id, SchoolId, AttendanceStatus.AbsentUnexcused, 3);
        GetChronicAbsenteeismQueryHandler handler = CreateHandler(context, threshold: 3);

        // Act
        Result<ChronicAbsenteeismResponse> result = await handler.Handle(
            new GetChronicAbsenteeismQuery(SchoolId, null),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.SchoolYear.ShouldBe(SchoolYear);
        result.Value.Students.ShouldHaveSingleItem().StudentId.ShouldBe(ava.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidSchoolYear_WhenLabelIsMalformed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        GetChronicAbsenteeismQueryHandler handler = CreateHandler(context, threshold: 3);

        // Act
        Result<ChronicAbsenteeismResponse> result = await handler.Handle(
            new GetChronicAbsenteeismQuery(SchoolId, "next-year"),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AttendanceErrors.InvalidSchoolYear("next-year"));
    }

    private static GetChronicAbsenteeismQueryHandler CreateHandler(TestDbContext context, int threshold)
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(UtcNow);

        return new GetChronicAbsenteeismQueryHandler(
            context,
            new AttendanceOptions { ChronicAbsenceThreshold = threshold },
            dateTimeProvider);
    }

    private static async Task<Student> AddStudentAsync(TestDbContext context, string firstName, string lastName = "Test")
    {
        var student = new Student { FirstName = firstName, LastName = lastName };
        context.Students.Add(student);
        await context.SaveChangesAsync();

        return student;
    }

    // Adds one record per day starting at FirstDay + startOffset (negative offsets land in the previous school year).
    private static async Task AddRecordsAsync(
        TestDbContext context,
        int studentId,
        int schoolId,
        AttendanceStatus status,
        int days,
        int startOffset = 0)
    {
        context.AttendanceRecords.AddRange(Enumerable.Range(startOffset, days).Select(offset => new AttendanceRecord
        {
            StudentId = studentId,
            SchoolId = schoolId,
            Date = FirstDay.AddDays(offset),
            Status = status,
            MinutesLate = status == AttendanceStatus.Late ? 5 : null,
            CreatedBy = "teacher@example.com",
            CreatedAtUtc = UtcNow
        }));
        await context.SaveChangesAsync();
    }
}
