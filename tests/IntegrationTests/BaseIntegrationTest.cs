using System.Net.Http.Headers;
using System.Net.Http.Json;
using Domain.Students;
using Infrastructure.Database;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

[Collection(nameof(IntegrationTestCollection))]
public abstract class BaseIntegrationTest
{
    protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
    {
        Factory = factory;
        HttpClient = factory.CreateClient();
    }

    protected HttpClient HttpClient { get; }

    protected IntegrationTestWebAppFactory Factory { get; }

    // Students have no API (docs/adr/0009-student-entity.md), so tests insert them directly.
    protected async Task<int> CreateStudentAsync(string firstName = "Test", string lastName = "Student")
    {
        int studentId = 0;

        await UseDbAsync(async dbContext =>
        {
            var student = new Student { FirstName = firstName, LastName = lastName };
            dbContext.Students.Add(student);
            await dbContext.SaveChangesAsync();
            studentId = student.Id;
        });

        return studentId;
    }

    // Reads persisted state that the API doesn't expose (e.g. audit columns).
    protected async Task<T> QueryDbAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await query(dbContext);
    }

    private async Task UseDbAsync(Func<ApplicationDbContext, Task> action)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await action(dbContext);
    }

    protected sealed record AccessTokens(string AccessToken, string RefreshToken);

    protected static string UniqueEmail() => $"test-{Guid.NewGuid():N}@example.com";

    protected async Task<Guid> RegisterUserAsync(string email)
    {
        var request = new
        {
            email,
            firstName = "Test",
            lastName = "User",
            password = "Password123"
        };

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/register", request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    protected async Task<AccessTokens> LoginAsync(string email)
    {
        var request = new { email, password = "Password123" };

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/login", request);
        response.EnsureSuccessStatusCode();

        AccessTokens? tokens = await response.Content.ReadFromJsonAsync<AccessTokens>();

        return tokens!;
    }

    protected async Task<(Guid UserId, AccessTokens Tokens)> RegisterAndLoginAsync()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);

        return (userId, tokens);
    }

    protected void Authenticate(string accessToken)
    {
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }
}
