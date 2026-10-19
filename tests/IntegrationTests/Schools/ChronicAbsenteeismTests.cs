using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Schools;

public sealed class ChronicAbsenteeismTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private static readonly DateOnly Today = FixedDateTimeProvider.Today;

    [Fact]
    public async Task GetChronic_Should_ReturnUnauthorized_WhenNotAuthenticated()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("schools/1/chronic-absenteeism");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetChronic_Should_ListStudent_WhenAbsencesReachDefaultThreshold()
    {
        // Arrange
        await AuthenticateAsNewUserAsync();
        int schoolId = UniqueSchoolId();
        int studentId = await CreateStudentAsync("Ava", "Martin");
        await SaveAbsencesAsync(schoolId, studentId, days: 10);

        // Act
        ChronicResponse? chronic = await HttpClient.GetFromJsonAsync<ChronicResponse>(
            $"schools/{schoolId}/chronic-absenteeism?schoolYear=2025-2026");

        // Assert: the default configuration flags 10 absent days (ADR 0006).
        chronic!.Threshold.ShouldBe(10);
        chronic.SchoolYear.ShouldBe("2025-2026");
        ChronicStudent student = chronic.Students.ShouldHaveSingleItem();
        student.StudentId.ShouldBe(studentId);
        student.LastName.ShouldBe("Martin");
        student.TotalAbsences.ShouldBe(10);
    }

    [Fact]
    public async Task GetChronic_Should_DropStudentImmediately_WhenAnAbsenceIsCorrected()
    {
        // Arrange
        await AuthenticateAsNewUserAsync();
        int schoolId = UniqueSchoolId();
        int studentId = await CreateStudentAsync();
        await SaveAbsencesAsync(schoolId, studentId, days: 10);

        // Act: correct one day to Present - nothing is stored, so the next request reflects it (ADR 0006).
        await PutAsync(Today.AddDays(-1), schoolId, studentId, "Present");
        ChronicResponse? chronic = await HttpClient.GetFromJsonAsync<ChronicResponse>(
            $"schools/{schoolId}/chronic-absenteeism");

        // Assert
        chronic!.Students.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetChronic_Should_ReturnEmptyList_WhenSchoolHasNoRecords()
    {
        // Arrange
        await AuthenticateAsNewUserAsync();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"schools/{UniqueSchoolId()}/chronic-absenteeism");

        // Assert: there is no school table, so an unknown school is just empty (ADR 0008).
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ChronicResponse>())!.Students.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetChronic_Should_ReturnBadRequest_WhenSchoolYearIsMalformed()
    {
        // Arrange
        await AuthenticateAsNewUserAsync();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("schools/1/chronic-absenteeism?schoolYear=2025");

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

    private async Task SaveAbsencesAsync(int schoolId, int studentId, int days)
    {
        for (int day = 1; day <= days; day++)
        {
            await PutAsync(Today.AddDays(-day), schoolId, studentId, day % 2 == 0 ? "AbsentExcused" : "AbsentUnexcused");
        }
    }

    private async Task PutAsync(DateOnly date, int schoolId, int studentId, string status)
    {
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"attendance/{date:yyyy-MM-dd}",
            new { schoolId, records = new[] { new { studentId, status, minutesLate = (int?)null, notes = (string?)null } } });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private sealed record ChronicResponse(int SchoolId, string SchoolYear, int Threshold, List<ChronicStudent> Students);

    private sealed record ChronicStudent(
        int StudentId,
        string FirstName,
        string LastName,
        int ExcusedAbsences,
        int UnexcusedAbsences,
        int TotalAbsences);
}
