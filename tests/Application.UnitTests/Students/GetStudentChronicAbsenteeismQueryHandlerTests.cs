using Application.Attendance;
using Application.Students.GetChronicAbsenteeism;
using Application.UnitTests.Abstractions;
using Domain.Attendance;
using Domain.Students;
using SharedKernel;

namespace Application.UnitTests.Students;

public sealed class GetStudentChronicAbsenteeismQueryHandlerTests : BaseHandlerTest
{
    private const int SchoolId = 7;
    private const string SchoolYear = "2025-2026";

    // Today is 2026-03-16: with the default start (September 1) the current school year is 2025-2026.
    private static readonly DateTime UtcNow = new(2026, 3, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly FirstDay = new(2025, 9, 1);

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    public async Task Handle_Should_FlagStudentOnlyFromTheDefaultThreshold(int absences, bool expectedChronic)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context);
        await AddRecordsAsync(context, ava.Id, SchoolId, AttendanceStatus.AbsentUnexcused, FirstDay, absences);
        GetStudentChronicAbsenteeismQueryHandler handler = CreateHandler(context, new AttendanceOptions());

        // Act
        Result<StudentChronicAbsenteeismResponse> result = await handler.Handle(
            new GetStudentChronicAbsenteeismQuery(ava.Id, SchoolYear),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Threshold.ShouldBe(10);
        result.Value.TotalAbsences.ShouldBe(absences);
        result.Value.IsChronicallyAbsent.ShouldBe(expectedChronic);
    }

    // 9 absences in the year plus one more right on either side of the school-year end.
    [Theory]
    [InlineData(2026, 8, 31, 10, true)]  // last day of 2025-2026 counts
    [InlineData(2026, 9, 1, 9, false)]   // first day of 2026-2027 doesn't
    [InlineData(2025, 8, 31, 9, false)]  // last day of 2024-2025 doesn't
    public async Task Handle_Should_CountOnlyAbsencesInsideTheSchoolYear(
        int year, int month, int day, int expectedTotal, bool expectedChronic)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context);
        await AddRecordsAsync(context, ava.Id, SchoolId, AttendanceStatus.AbsentUnexcused, FirstDay, 9);
        await AddRecordsAsync(context, ava.Id, SchoolId, AttendanceStatus.AbsentUnexcused, new DateOnly(year, month, day), 1);
        GetStudentChronicAbsenteeismQueryHandler handler = CreateHandler(context, new AttendanceOptions());

        // Act
        Result<StudentChronicAbsenteeismResponse> result = await handler.Handle(
            new GetStudentChronicAbsenteeismQuery(ava.Id, SchoolYear),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalAbsences.ShouldBe(expectedTotal);
        result.Value.IsChronicallyAbsent.ShouldBe(expectedChronic);
    }

    [Fact]
    public async Task Handle_Should_ReportExcusedAndUnexcusedSeparatelyAndIgnoreLateAndPresent()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context);
        await AddRecordsAsync(context, ava.Id, SchoolId, AttendanceStatus.AbsentExcused, FirstDay, 2);
        await AddRecordsAsync(context, ava.Id, SchoolId, AttendanceStatus.AbsentUnexcused, FirstDay.AddDays(10), 1);
        await AddRecordsAsync(context, ava.Id, SchoolId, AttendanceStatus.Late, FirstDay.AddDays(20), 5);
        await AddRecordsAsync(context, ava.Id, SchoolId, AttendanceStatus.Present, FirstDay.AddDays(30), 5);
        GetStudentChronicAbsenteeismQueryHandler handler = CreateHandler(context, new AttendanceOptions());

        // Act
        Result<StudentChronicAbsenteeismResponse> result = await handler.Handle(
            new GetStudentChronicAbsenteeismQuery(ava.Id, SchoolYear),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ExcusedAbsences.ShouldBe(2);
        result.Value.UnexcusedAbsences.ShouldBe(1);
        result.Value.TotalAbsences.ShouldBe(3);
    }

    [Fact]
    public async Task Handle_Should_CountOnlyAbsencesAtTheStudentsOwnSchool()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context);
        await AddRecordsAsync(context, ava.Id, SchoolId, AttendanceStatus.AbsentUnexcused, FirstDay, 2);
        await AddRecordsAsync(context, ava.Id, SchoolId + 1, AttendanceStatus.AbsentUnexcused, FirstDay.AddDays(10), 10);
        GetStudentChronicAbsenteeismQueryHandler handler = CreateHandler(context, new AttendanceOptions());

        // Act
        Result<StudentChronicAbsenteeismResponse> result = await handler.Handle(
            new GetStudentChronicAbsenteeismQuery(ava.Id, SchoolYear),
            CancellationToken.None);

        // Assert: 12 absences in total, but only 2 at the student's school (ADR 0008, ADR 0011).
        result.IsSuccess.ShouldBeTrue();
        result.Value.SchoolId.ShouldBe(SchoolId);
        result.Value.TotalAbsences.ShouldBe(2);
        result.Value.IsChronicallyAbsent.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnZeroCounts_WhenStudentHasNoAbsences()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context);
        GetStudentChronicAbsenteeismQueryHandler handler = CreateHandler(context, new AttendanceOptions());

        // Act
        Result<StudentChronicAbsenteeismResponse> result = await handler.Handle(
            new GetStudentChronicAbsenteeismQuery(ava.Id, null),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.SchoolYear.ShouldBe(SchoolYear);
        result.Value.TotalAbsences.ShouldBe(0);
        result.Value.IsChronicallyAbsent.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_UseConfiguredThreshold()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context);
        await AddRecordsAsync(context, ava.Id, SchoolId, AttendanceStatus.AbsentExcused, FirstDay, 3);
        GetStudentChronicAbsenteeismQueryHandler handler =
            CreateHandler(context, new AttendanceOptions { ChronicAbsenceThreshold = 3 });

        // Act
        Result<StudentChronicAbsenteeismResponse> result = await handler.Handle(
            new GetStudentChronicAbsenteeismQuery(ava.Id, SchoolYear),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Threshold.ShouldBe(3);
        result.Value.IsChronicallyAbsent.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenStudentDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        GetStudentChronicAbsenteeismQueryHandler handler = CreateHandler(context, new AttendanceOptions());

        // Act
        Result<StudentChronicAbsenteeismResponse> result = await handler.Handle(
            new GetStudentChronicAbsenteeismQuery(999, SchoolYear),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(StudentErrors.NotFound(999));
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidSchoolYear_WhenLabelIsMalformed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context);
        GetStudentChronicAbsenteeismQueryHandler handler = CreateHandler(context, new AttendanceOptions());

        // Act
        Result<StudentChronicAbsenteeismResponse> result = await handler.Handle(
            new GetStudentChronicAbsenteeismQuery(ava.Id, "2025"),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AttendanceErrors.InvalidSchoolYear("2025"));
    }

    private static GetStudentChronicAbsenteeismQueryHandler CreateHandler(TestDbContext context, AttendanceOptions options)
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(UtcNow);

        return new GetStudentChronicAbsenteeismQueryHandler(context, options, dateTimeProvider);
    }

    private static async Task<Student> AddStudentAsync(TestDbContext context)
    {
        var student = new Student { SchoolId = SchoolId, FirstName = "Ava", LastName = "Martin" };
        context.Students.Add(student);
        await context.SaveChangesAsync();

        return student;
    }

    // Adds one record per day for `days` consecutive days starting at `firstDate`.
    private static async Task AddRecordsAsync(
        TestDbContext context,
        int studentId,
        int schoolId,
        AttendanceStatus status,
        DateOnly firstDate,
        int days)
    {
        context.AttendanceRecords.AddRange(Enumerable.Range(0, days).Select(offset => new AttendanceRecord
        {
            StudentId = studentId,
            SchoolId = schoolId,
            Date = firstDate.AddDays(offset),
            Status = status,
            MinutesLate = status == AttendanceStatus.Late ? 5 : null,
            CreatedBy = "teacher@example.com",
            CreatedAtUtc = UtcNow
        }));
        await context.SaveChangesAsync();
    }
}
