using Application.Attendance;
using Application.Students.GetAttendanceHistory;
using Application.UnitTests.Abstractions;
using Domain.Attendance;
using Domain.Students;
using SharedKernel;

namespace Application.UnitTests.Students;

public sealed class GetStudentAttendanceHistoryQueryHandlerTests : BaseHandlerTest
{
    // Today is 2026-03-16: with the default start (September 1) the current school year is 2025-2026.
    private static readonly DateTime UtcNow = new(2026, 3, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_ReturnOnlyRequestedSchoolYearNewestFirst()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context);
        await AddRecordsAsync(context, ava.Id,
            (new DateOnly(2025, 8, 31), 1),  // last day of 2024-2025
            (new DateOnly(2025, 9, 1), 1),   // first day of 2025-2026
            (new DateOnly(2026, 3, 16), 1),
            (new DateOnly(2026, 9, 1), 1));  // first day of 2026-2027
        GetStudentAttendanceHistoryQueryHandler handler = CreateHandler(context, new AttendanceOptions());

        // Act
        Result<StudentAttendanceHistoryResponse> result = await handler.Handle(
            new GetStudentAttendanceHistoryQuery(ava.Id, "2025-2026"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.SchoolYear.ShouldBe("2025-2026");
        result.Value.FirstName.ShouldBe("Ava");
        result.Value.Records.Select(r => r.Date).ShouldBe([new DateOnly(2026, 3, 16), new DateOnly(2025, 9, 1)]);
    }

    [Fact]
    public async Task Handle_Should_IncludeRecordsFromEverySchool()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context);
        await AddRecordsAsync(context, ava.Id,
            (new DateOnly(2025, 10, 1), 1),
            (new DateOnly(2026, 2, 1), 2));
        GetStudentAttendanceHistoryQueryHandler handler = CreateHandler(context, new AttendanceOptions());

        // Act
        Result<StudentAttendanceHistoryResponse> result = await handler.Handle(
            new GetStudentAttendanceHistoryQuery(ava.Id, "2025-2026"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Records.Select(r => r.SchoolId).ShouldBe([2, 1]);
    }

    [Fact]
    public async Task Handle_Should_DefaultToCurrentSchoolYear_WhenNoneIsGiven()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context);
        await AddRecordsAsync(context, ava.Id,
            (new DateOnly(2025, 6, 1), 1),
            (new DateOnly(2026, 1, 5), 1));
        GetStudentAttendanceHistoryQueryHandler handler = CreateHandler(context, new AttendanceOptions());

        // Act
        Result<StudentAttendanceHistoryResponse> result = await handler.Handle(
            new GetStudentAttendanceHistoryQuery(ava.Id, null),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.SchoolYear.ShouldBe("2025-2026");
        result.Value.Records.ShouldHaveSingleItem().Date.ShouldBe(new DateOnly(2026, 1, 5));
    }

    [Fact]
    public async Task Handle_Should_UseConfiguredSchoolYearStart()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context);
        await AddRecordsAsync(context, ava.Id, (new DateOnly(2025, 8, 20), 1));
        var options = new AttendanceOptions { SchoolYearStartMonth = 8, SchoolYearStartDay = 15 };
        GetStudentAttendanceHistoryQueryHandler handler = CreateHandler(context, options);

        // Act
        Result<StudentAttendanceHistoryResponse> result = await handler.Handle(
            new GetStudentAttendanceHistoryQuery(ava.Id, "2025-2026"),
            CancellationToken.None);

        // Assert: with a September 1 start, August 20 would belong to 2024-2025.
        result.IsSuccess.ShouldBeTrue();
        result.Value.Records.ShouldHaveSingleItem().Date.ShouldBe(new DateOnly(2025, 8, 20));
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidSchoolYear_WhenLabelIsMalformed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Student ava = await AddStudentAsync(context);
        GetStudentAttendanceHistoryQueryHandler handler = CreateHandler(context, new AttendanceOptions());

        // Act
        Result<StudentAttendanceHistoryResponse> result = await handler.Handle(
            new GetStudentAttendanceHistoryQuery(ava.Id, "2025"),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AttendanceErrors.InvalidSchoolYear("2025"));
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenStudentDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        GetStudentAttendanceHistoryQueryHandler handler = CreateHandler(context, new AttendanceOptions());

        // Act
        Result<StudentAttendanceHistoryResponse> result = await handler.Handle(
            new GetStudentAttendanceHistoryQuery(999, null),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(StudentErrors.NotFound(999));
    }

    private static GetStudentAttendanceHistoryQueryHandler CreateHandler(TestDbContext context, AttendanceOptions options)
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(UtcNow);

        return new GetStudentAttendanceHistoryQueryHandler(context, options, dateTimeProvider);
    }

    private static async Task<Student> AddStudentAsync(TestDbContext context)
    {
        var student = new Student { FirstName = "Ava", LastName = "Martin" };
        context.Students.Add(student);
        await context.SaveChangesAsync();

        return student;
    }

    private static async Task AddRecordsAsync(TestDbContext context, int studentId, params (DateOnly Date, int SchoolId)[] records)
    {
        context.AttendanceRecords.AddRange(records.Select(r => new AttendanceRecord
        {
            StudentId = studentId,
            SchoolId = r.SchoolId,
            Date = r.Date,
            Status = AttendanceStatus.AbsentExcused,
            CreatedBy = "teacher@example.com",
            CreatedAtUtc = UtcNow
        }));
        await context.SaveChangesAsync();
    }
}
