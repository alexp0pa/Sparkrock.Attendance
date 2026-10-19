using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Students;

// The seeder behind the Migrator's development seed and the AppHost's "Seed students" command (ADR 0011).
public sealed class DevelopmentDataSeederTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task AddStudents_Should_InsertRequestedNumberOfStudentsForSchool()
    {
        // Arrange
        int schoolId = Random.Shared.Next(100_000, int.MaxValue);

        // Act
        IReadOnlyList<int> studentIds = await SeedAsync(schoolId, 3);

        // Assert
        studentIds.Count.ShouldBe(3);

        List<int> stored = await QueryDbAsync(db => db.Students
            .Where(s => s.SchoolId == schoolId)
            .OrderBy(s => s.Id)
            .Select(s => s.Id)
            .ToListAsync());
        stored.ShouldBe(studentIds);
    }

    [Fact]
    public async Task AddStudents_Should_ReturnStudentsUsableForAttendance()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        IReadOnlyList<int> studentIds = await SeedAsync(schoolId: 5, count: 1);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"students/{studentIds[0]}/attendance");

        // Assert
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(1, DevelopmentDataSeeder.MaxStudentsPerRun + 1)]
    public async Task AddStudents_Should_Reject_WhenSchoolIdOrCountIsOutOfRange(int schoolId, int count)
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => SeedAsync(schoolId, count));
    }

    private async Task<IReadOnlyList<int>> SeedAsync(int schoolId, int count)
    {
        using IServiceScope scope = Factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<DevelopmentDataSeeder>()
            .AddStudentsAsync(schoolId, count);
    }
}
