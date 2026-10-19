using System.Net;
using System.Net.Http.Json;
using Domain.Attendance;

namespace IntegrationTests.Students;

public sealed class StudentChronicAbsenteeismTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private static readonly DateOnly Today = FixedDateTimeProvider.Today;

    [Fact]
    public async Task GetStudentChronic_Should_ReturnUnauthorized_WhenNotAuthenticated()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("students/1/chronic-absenteeism");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // Threshold boundary with the default configuration (ADR 0006): 9 absent days -> not chronic, 10 -> chronic.
    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    public async Task GetStudentChronic_Should_FlagFromTenAbsences(int absences, bool expectedChronic)
    {
        // Arrange
        await AuthenticateAsNewUserAsync();
        int schoolId = UniqueSchoolId();
        int studentId = await CreateStudentAsync(schoolId: schoolId);
        await SaveAbsencesAsync(schoolId, studentId, absences);

        // Act
        StudentChronicResponse? status = await HttpClient.GetFromJsonAsync<StudentChronicResponse>(
            $"students/{studentId}/chronic-absenteeism?schoolYear=2025-2026");

        // Assert
        status!.StudentId.ShouldBe(studentId);
        status.SchoolId.ShouldBe(schoolId);
        status.SchoolYear.ShouldBe("2025-2026");
        status.Threshold.ShouldBe(10);
        status.ExcusedAbsences.ShouldBe(absences / 2);
        status.UnexcusedAbsences.ShouldBe(absences - absences / 2);
        status.TotalAbsences.ShouldBe(absences);
        status.IsChronicallyAbsent.ShouldBe(expectedChronic);
    }

    // School-year boundary (ADR 0005): an absence on the last day of the previous year (2025-08-31) doesn't
    // count toward 2025-2026, so 9 + 1 stays below the threshold; it does count when 2024-2025 is requested.
    [Fact]
    public async Task GetStudentChronic_Should_CountOnlyTheRequestedSchoolYear()
    {
        // Arrange
        await AuthenticateAsNewUserAsync();
        int schoolId = UniqueSchoolId();
        int studentId = await CreateStudentAsync(schoolId: schoolId);
        await SaveAbsencesAsync(schoolId, studentId, 9);

        // The API only accepts dates in the current school year (ADR 0004), so the earlier record goes in directly.
        await QueryDbAsync(async db =>
        {
            db.AttendanceRecords.Add(new AttendanceRecord
            {
                StudentId = studentId,
                SchoolId = schoolId,
                Date = new DateOnly(2025, 8, 31),
                Status = AttendanceStatus.AbsentUnexcused,
                CreatedBy = "test@example.com",
                CreatedAtUtc = FixedDateTimeProvider.FixedUtcNow
            });
            return await db.SaveChangesAsync();
        });

        // Act
        StudentChronicResponse? current = await HttpClient.GetFromJsonAsync<StudentChronicResponse>(
            $"students/{studentId}/chronic-absenteeism");
        StudentChronicResponse? previous = await HttpClient.GetFromJsonAsync<StudentChronicResponse>(
            $"students/{studentId}/chronic-absenteeism?schoolYear=2024-2025");

        // Assert
        current!.SchoolYear.ShouldBe("2025-2026");
        current.TotalAbsences.ShouldBe(9);
        current.IsChronicallyAbsent.ShouldBeFalse();
        previous!.TotalAbsences.ShouldBe(1);
    }

    [Fact]
    public async Task GetStudentChronic_Should_AgreeWithSchoolList()
    {
        // Arrange
        await AuthenticateAsNewUserAsync();
        int schoolId = UniqueSchoolId();
        int studentId = await CreateStudentAsync(schoolId: schoolId);
        await SaveAbsencesAsync(schoolId, studentId, 10);

        // Act
        StudentChronicResponse? status = await HttpClient.GetFromJsonAsync<StudentChronicResponse>(
            $"students/{studentId}/chronic-absenteeism");
        SchoolListResponse? list = await HttpClient.GetFromJsonAsync<SchoolListResponse>(
            $"schools/{schoolId}/chronic-absenteeism");

        // Assert: both endpoints use the same calculation.
        status!.IsChronicallyAbsent.ShouldBeTrue();
        list!.Students.ShouldHaveSingleItem().TotalAbsences.ShouldBe(status.TotalAbsences);
    }

    [Fact]
    public async Task GetStudentChronic_Should_ReturnNotFound_WhenStudentDoesNotExist()
    {
        // Arrange
        await AuthenticateAsNewUserAsync();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"students/{int.MaxValue}/chronic-absenteeism");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetStudentChronic_Should_ReturnBadRequest_WhenSchoolYearIsMalformed()
    {
        // Arrange
        await AuthenticateAsNewUserAsync();
        int studentId = await CreateStudentAsync();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            $"students/{studentId}/chronic-absenteeism?schoolYear=2025-2027");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // The test database is shared, so each test uses its own school.
    private static int UniqueSchoolId() => Random.Shared.Next(100_000, int.MaxValue);

    private async Task AuthenticateAsNewUserAsync()
    {
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
    }

    // Saves one absence per day going back from yesterday, alternating unexcused and excused.
    private async Task SaveAbsencesAsync(int schoolId, int studentId, int days)
    {
        for (int day = 1; day <= days; day++)
        {
            string status = day % 2 == 0 ? "AbsentExcused" : "AbsentUnexcused";

            HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
                $"attendance/{Today.AddDays(-day):yyyy-MM-dd}",
                new { schoolId, records = new[] { new { studentId, status, minutesLate = (int?)null, notes = (string?)null } } });

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }
    }

    private sealed record StudentChronicResponse(
        int StudentId,
        int SchoolId,
        string SchoolYear,
        int Threshold,
        int ExcusedAbsences,
        int UnexcusedAbsences,
        int TotalAbsences,
        bool IsChronicallyAbsent);

    private sealed record SchoolListResponse(List<SchoolListStudent> Students);

    private sealed record SchoolListStudent(int StudentId, int TotalAbsences);
}
