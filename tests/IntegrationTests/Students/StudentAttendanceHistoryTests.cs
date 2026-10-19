using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Students;

public sealed class StudentAttendanceHistoryTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private static readonly DateOnly Today = FixedDateTimeProvider.Today;

    [Fact]
    public async Task GetHistory_Should_ReturnUnauthorized_WhenNotAuthenticated()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("students/1/attendance");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetHistory_Should_ReturnSavedRecordsWithStatusNames()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        int studentId = await CreateStudentAsync("Ava", "Martin");

        await PutAsync(Today.AddDays(-1), studentId, new { studentId, status = "Late", minutesLate = 15, notes = "Bus" });
        await PutAsync(Today, studentId, new { studentId, status = "AbsentExcused", minutesLate = (int?)null, notes = (string?)null });

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"students/{studentId}/attendance?schoolYear=2025-2026");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Read as strings to prove the wire format (docs/adr/0010-enums-as-strings-in-api.md).
        HistoryResponse? history = await response.Content.ReadFromJsonAsync<HistoryResponse>();
        history!.StudentId.ShouldBe(studentId);
        history.LastName.ShouldBe("Martin");
        history.SchoolYear.ShouldBe("2025-2026");
        history.Records.Select(r => r.Status).ShouldBe(["AbsentExcused", "Late"]);
        history.Records[1].MinutesLate.ShouldBe(15);
        history.Records[1].Date.ShouldBe(Today.AddDays(-1));
    }

    [Fact]
    public async Task GetHistory_Should_DefaultToCurrentSchoolYear()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        int studentId = await CreateStudentAsync();

        // Act
        HistoryResponse? history = await HttpClient.GetFromJsonAsync<HistoryResponse>($"students/{studentId}/attendance");

        // Assert
        history!.SchoolYear.ShouldBe("2025-2026");
        history.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetHistory_Should_ReturnNotFound_WhenStudentDoesNotExist()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"students/{int.MaxValue}/attendance");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetHistory_Should_ReturnBadRequest_WhenSchoolYearIsMalformed()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        int studentId = await CreateStudentAsync();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"students/{studentId}/attendance?schoolYear=2025-2027");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task PutAsync(DateOnly date, int studentId, object record)
    {
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"attendance/{date:yyyy-MM-dd}",
            new { schoolId = 7, records = new[] { record } });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, $"saving attendance for student {studentId}");
    }

    private sealed record HistoryResponse(
        int StudentId,
        string FirstName,
        string LastName,
        string SchoolYear,
        List<HistoryRecord> Records);

    private sealed record HistoryRecord(DateOnly Date, int SchoolId, string Status, int? MinutesLate, string? Notes);
}
