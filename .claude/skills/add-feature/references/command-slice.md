# Command Slice Templates

Files go in `src/Application/{Feature}/{UseCase}/`. Replace `{Feature}` (plural, e.g. `Projects`), `{Entity}` (e.g. `Project`), and use-case names throughout.

## Command

Positional record for few parameters:

```csharp
using Application.Abstractions.Messaging;

namespace Application.Projects.Archive;

public sealed record ArchiveProjectCommand(Guid ProjectId) : ICommand;
```

Class with settable properties when there are many parameters:

```csharp
using Application.Abstractions.Messaging;

namespace Application.Projects.Create;

public sealed class CreateProjectCommand : ICommand<Guid>
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public List<string> Tags { get; set; } = [];
}
```

- `ICommand` → handler returns `Result` (endpoint responds `204 NoContent`).
- `ICommand<TResponse>` → handler returns `Result<TResponse>` (endpoint responds `200 Ok`).

## Validator

Same folder. Auto-registered (internal types included) and executed by `ValidationDecorator` before the handler runs.

```csharp
using FluentValidation;

namespace Application.Projects.Create;

internal sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Description).MaximumLength(2000);
        RuleFor(c => c.DueDate).GreaterThanOrEqualTo(DateTime.Today).When(c => c.DueDate.HasValue);
    }
}
```

## Handler

`internal sealed`, primary constructor, `IApplicationDbContext` for data access. Guard clauses return `Result.Failure` with Domain errors; the happy path mutates, saves, and returns.

```csharp
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Projects;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Projects.Archive;

internal sealed class ArchiveProjectCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    IUserContext userContext)
    : ICommandHandler<ArchiveProjectCommand>
{
    public async Task<Result> Handle(ArchiveProjectCommand command, CancellationToken cancellationToken)
    {
        Project? project = await context.Projects
            .SingleOrDefaultAsync(
                p => p.Id == command.ProjectId && p.OwnerId == userContext.UserId,
                cancellationToken);

        if (project is null)
        {
            return Result.Failure(ProjectErrors.NotFound(command.ProjectId));
        }

        if (project.IsArchived)
        {
            return Result.Failure(ProjectErrors.AlreadyArchived(command.ProjectId));
        }

        project.IsArchived = true;
        project.ArchivedAt = dateTimeProvider.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
```

Notes:
- Ownership: filter by `userContext.UserId` in the query (preferred) or compare explicitly and return `Result.Failure(UserErrors.Unauthorized())`.
- Set `Id = Guid.NewGuid()` explicitly when creating an entity, so the handler can return it.
- For a returning command (`ICommand<Guid>`), return the value directly — `Result<T>` has an implicit conversion: `return project.Id;`.
- Side effects (notifications, related updates) are explicit calls in the handler, before or after `SaveChangesAsync` as the consistency need dictates — there is no domain-event mechanism.

## Domain additions (if needed)

Error factory on the existing `{Entity}Errors` class in `src/Domain/{Feature}/`:

```csharp
public static Error AlreadyArchived(Guid projectId) => Error.Problem(
    "Projects.AlreadyArchived",
    $"The project with Id = '{projectId}' is already archived.");
```

Error type → HTTP status (via `CustomResults.Problem`): `NotFound` → 404, `Conflict` → 409, `Problem`/`Validation` → 400, `Failure` → 500.
