# Test Templates

Stack: xUnit + Shouldly + NSubstitute (unit), `FluentValidation.TestHelper` (validators), `WebApplicationFactory` + Testcontainers SQL Server (integration). Every new use case gets all three.

`GlobalUsings.cs` imports `Xunit`, `Shouldly` (both test projects) and `NSubstitute` (unit tests). Add `using SharedKernel;` yourself when you need `Result`/`Error`.

## Handler unit tests

`tests/Application.UnitTests/{Feature}/{UseCase}{Command|Query}HandlerTests.cs`. Inherit `BaseHandlerTest` — it provides `CreateDbContext()` (a fresh in-memory `TestDbContext` implementing `IApplicationDbContext`). Mock only interfaces (`IUserContext`, `IDateTimeProvider`, ...) with NSubstitute — never mock the DbContext.

Cover: each failure path (one test per guard clause) and the happy path including persisted state.

```csharp
using Application.Abstractions.Authentication;
using Application.Projects.Archive;
using Application.UnitTests.Abstractions;
using Domain.Projects;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Projects;

public sealed class ArchiveProjectCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenProjectDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var command = new ArchiveProjectCommand(Guid.NewGuid());
        var handler = new ArchiveProjectCommandHandler(context, dateTimeProvider, userContext);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProjectErrors.NotFound(command.ProjectId));
    }

    [Fact]
    public async Task Handle_Should_ArchiveProject_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            OwnerId = UserId,
            Name = "Archive me",
            CreatedAt = DateTime.UtcNow
        };
        context.Projects.Add(project);
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        var command = new ArchiveProjectCommand(project.Id);
        var handler = new ArchiveProjectCommandHandler(context, dateTimeProvider, userContext);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Project archived = await context.Projects.SingleAsync(p => p.Id == project.Id);
        archived.IsArchived.ShouldBeTrue();
    }
}
```

Conventions:
- Test names: `Handle_Should_{Outcome}_When{Condition}`.
- `// Arrange` / `// Act` / `// Assert` comments in every test.
- Assert failures by comparing the exact error: `result.Error.ShouldBe(ProjectErrors.NotFound(id))`.
- `TestDbContext` must expose the same `DbSet`s as `IApplicationDbContext` — add one there whenever you add a `DbSet`.

## Validator tests

`tests/Application.UnitTests/{Feature}/{Feature}ValidatorsTests.cs` (extend the existing file if present). One class covers all validators of a feature.

```csharp
using Application.Projects.Create;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Projects;

public sealed class ProjectValidatorsTests
{
    private readonly CreateProjectCommandValidator _createValidator = new();

    [Fact]
    public void CreateValidator_Should_HaveError_WhenNameIsEmpty()
    {
        var command = new CreateProjectCommand { Name = string.Empty };

        TestValidationResult<CreateProjectCommand> result = _createValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Name);
    }
}
```

Cover each rule's failure plus one fully-valid command (`ShouldNotHaveAnyValidationErrors`). Validators are `internal`; the unit test project already has `InternalsVisibleTo`.

## Integration tests

`tests/IntegrationTests/{Feature}/{Feature}Tests.cs` (extend the existing file if present). Inherit `BaseIntegrationTest(factory)` — it runs the real API against a Testcontainers SQL Server and provides `HttpClient`, `RegisterAndLoginAsync()`, and `Authenticate(token)`. Tests go through real HTTP, never call handlers directly. Define private response DTO records inside the test class.

```csharp
using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Projects;

public sealed class ProjectsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task ArchiveProject_Should_ReturnNoContent()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync("projects", new { name = "Demo" });
        createResponse.EnsureSuccessStatusCode();
        Guid projectId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"projects/{projectId}/archive", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
```

Minimum coverage per endpoint: one unauthorized test (no token → 401) if it's a new route family, one happy-path test asserting observable state via a follow-up GET, and one failure translation test (e.g. unknown id → 404, invalid body → 400) when the handler has failure paths.

The database is shared across the test collection — make data unique per test (fresh user via `RegisterAndLoginAsync()`, new ids) instead of relying on an empty table.

## Run

```
dotnet test
```

Integration tests need Docker running. Architecture tests fail the build if a layer dependency rule is violated — fix the dependency, never the test.
