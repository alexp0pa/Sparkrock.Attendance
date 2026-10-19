using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Data;
using Domain.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Attendance;

public sealed class SaveDailyAttendanceTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const int SchoolId = 7;
    private static readonly DateOnly Today = FixedDateTimeProvider.Today;

    [Fact]
    public async Task SaveDaily_Should_ReturnUnauthorized_WhenNotAuthenticated()
    {
        // Act
        HttpResponseMessage response = await PutAsync(Today, SchoolId, Present(1));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SaveDaily_Should_PersistAllRecords_WhenValid()
    {
        // Arrange
        await LoginAsNewUserAsync();
        int ava = await CreateStudentAsync("Ava");
        int liam = await CreateStudentAsync("Liam");

        // Act
        HttpResponseMessage response = await PutAsync(Today, SchoolId,
            new { studentId = ava, status = "Late", minutesLate = 10, notes = "Bus" },
            new { studentId = liam, status = "AbsentExcused", minutesLate = (int?)null, notes = (string?)null });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        List<AttendanceRecord> records = await RecordsAsync(ava, liam);
        records.Count.ShouldBe(2);
        records.Single(r => r.StudentId == ava).Status.ShouldBe(AttendanceStatus.Late);
        records.Single(r => r.StudentId == ava).MinutesLate.ShouldBe(10);
        records.Single(r => r.StudentId == liam).Status.ShouldBe(AttendanceStatus.AbsentExcused);
    }

    [Fact]
    public async Task SaveDaily_Should_KeepOneRecordPerStudent_WhenSameRequestIsRepeated()
    {
        // Arrange
        await LoginAsNewUserAsync();
        int ava = await CreateStudentAsync("Ava");
        (await PutAsync(Today, SchoolId, Present(ava))).EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage response = await PutAsync(Today, SchoolId,
            new { studentId = ava, status = "AbsentUnexcused", minutesLate = (int?)null, notes = (string?)null });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        AttendanceRecord record = (await RecordsAsync(ava)).ShouldHaveSingleItem();
        record.Status.ShouldBe(AttendanceStatus.AbsentUnexcused);
    }

    [Fact]
    public async Task SaveDaily_Should_RejectWholeBatchAndWriteNothing_WhenAStudentIsUnknown()
    {
        // Arrange
        await LoginAsNewUserAsync();
        int ava = await CreateStudentAsync("Ava");

        // Act
        HttpResponseMessage response = await PutAsync(Today, SchoolId, Present(ava), Present(int.MaxValue));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await RecordsAsync(ava)).ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveDaily_Should_RejectWholeBatchAndWriteNothing_WhenARecordIsInvalid()
    {
        // Arrange
        await LoginAsNewUserAsync();
        int ava = await CreateStudentAsync("Ava");
        int liam = await CreateStudentAsync("Liam");

        // Act: Late without minutes breaks ADR 0003.
        HttpResponseMessage response = await PutAsync(Today, SchoolId,
            Present(ava),
            new { studentId = liam, status = "Late", minutesLate = (int?)null, notes = (string?)null });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await RecordsAsync(ava, liam)).ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveDaily_Should_ReturnBadRequest_WhenDateIsInTheFuture()
    {
        // Arrange
        await LoginAsNewUserAsync();
        int ava = await CreateStudentAsync("Ava");

        // Act
        HttpResponseMessage response = await PutAsync(Today.AddDays(1), SchoolId, Present(ava));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await RecordsAsync(ava)).ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveDaily_Should_ReturnBadRequest_WhenStatusIsUnknown()
    {
        // Arrange
        await LoginAsNewUserAsync();
        int ava = await CreateStudentAsync("Ava");

        // Act
        HttpResponseMessage response = await PutAsync(Today, SchoolId,
            new { studentId = ava, status = "P", minutesLate = (int?)null, notes = (string?)null });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SaveDaily_Should_StoreOneRecord_WhenTwoRequestsRaceForTheSameStudentAndDate()
    {
        // Arrange
        await LoginAsNewUserAsync();
        int ava = await CreateStudentAsync("Ava");

        // Act
        HttpResponseMessage[] responses = await Task.WhenAll(
            PutAsync(Today, SchoolId, Present(ava)),
            PutAsync(Today, SchoolId, Present(ava)));

        // Assert: the unique index admits one row; a loser gets 409 and may retry.
        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.NoContent || r.StatusCode == HttpStatusCode.Conflict);
        (await RecordsAsync(ava)).Count.ShouldBe(1);
    }

    // Deterministic check of what the handler maps to 409 Attendance.ConcurrentSave: only the unique index
    // violation, never other database failures such as a foreign-key violation.
    [Fact]
    public async Task DbExceptionClassifier_Should_RecognizeOnlyUniqueViolations()
    {
        // Arrange
        int ava = await CreateStudentAsync("Ava");

        // Act
        bool duplicateIsUnique = await IsUniqueViolationAsync(ava, ava);
        bool foreignKeyIsUnique = await IsUniqueViolationAsync(int.MaxValue);

        // Assert
        duplicateIsUnique.ShouldBeTrue();
        foreignKeyIsUnique.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveDaily_Should_ReturnBadRequest_WhenARecordIsNull()
    {
        // Arrange
        await LoginAsNewUserAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"attendance/{Today:yyyy-MM-dd}",
            new { schoolId = SchoolId, records = new object?[] { null } });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SaveDaily_Should_WriteLoggedInUserEmailToAuditColumns()
    {
        // Arrange
        string creatorEmail = await LoginAsNewUserAsync();
        int ava = await CreateStudentAsync("Ava");
        (await PutAsync(Today, SchoolId, Present(ava))).EnsureSuccessStatusCode();

        string modifierEmail = await LoginAsNewUserAsync();

        // Act
        HttpResponseMessage response = await PutAsync(Today, SchoolId,
            new { studentId = ava, status = "AbsentExcused", minutesLate = (int?)null, notes = (string?)null });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        AttendanceRecord record = (await RecordsAsync(ava)).ShouldHaveSingleItem();
        record.CreatedBy.ShouldBe(creatorEmail);
        record.CreatedAtUtc.ShouldBe(FixedDateTimeProvider.FixedUtcNow);
        record.ModifiedBy.ShouldBe(modifierEmail);
        record.ModifiedAtUtc.ShouldBe(FixedDateTimeProvider.FixedUtcNow);
    }

    private static object Present(int studentId) =>
        new { studentId, status = "Present", minutesLate = (int?)null, notes = (string?)null };

    private Task<HttpResponseMessage> PutAsync(DateOnly date, int schoolId, params object[] records) =>
        HttpClient.PutAsJsonAsync($"attendance/{date:yyyy-MM-dd}", new { schoolId, records });

    private async Task<string> LoginAsNewUserAsync()
    {
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);
        Authenticate(tokens.AccessToken);

        return email;
    }

    // Inserts one record per student id directly and reports whether the failure is classified as a unique violation.
    private Task<bool> IsUniqueViolationAsync(params int[] studentIds) =>
        QueryDbAsync(async db =>
        {
            db.AttendanceRecords.AddRange(studentIds.Select(studentId => new AttendanceRecord
            {
                StudentId = studentId,
                SchoolId = SchoolId,
                Date = Today.AddDays(-30),
                Status = AttendanceStatus.Present,
                CreatedBy = "test@example.com",
                CreatedAtUtc = FixedDateTimeProvider.FixedUtcNow
            }));

            DbUpdateException exception = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());

            return Classifier.IsUniqueConstraintViolation(exception);
        });

    private IDbExceptionClassifier Classifier => Factory.Services.GetRequiredService<IDbExceptionClassifier>();

    private Task<List<AttendanceRecord>> RecordsAsync(params int[] studentIds) =>
        QueryDbAsync(db => db.AttendanceRecords
            .AsNoTracking()
            .Where(r => studentIds.Contains(r.StudentId))
            .ToListAsync());
}
