# Query Slice Templates

Files go in `src/Application/{Feature}/{UseCase}/`. Queries are reads: no validator, no `SaveChangesAsync`.

## Query

```csharp
using Application.Abstractions.Messaging;

namespace Application.Projects.GetById;

public sealed record GetProjectByIdQuery(int ProjectId) : IQuery<ProjectResponse>;
```

Paged list: `public sealed record GetProjectsQuery(string? Search, int Page, int PageSize) : IQuery<PagedList<ProjectResponse>>;`

## Response DTO

Lives next to the query. Flat, serialization-friendly, never a domain entity.

```csharp
namespace Application.Projects.GetById;

public sealed class ProjectResponse
{
    public int Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
}
```

Each use-case folder owns its own response DTO — do not share DTOs across slices even if they look identical today.

## Handler — single item

Scope to the current user, project with `.Select` straight into the DTO:

```csharp
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Projects;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Projects.GetById;

internal sealed class GetProjectByIdQueryHandler(IApplicationDbContext context, IUserContext userContext)
    : IQueryHandler<GetProjectByIdQuery, ProjectResponse>
{
    public async Task<Result<ProjectResponse>> Handle(GetProjectByIdQuery query, CancellationToken cancellationToken)
    {
        ProjectResponse? project = await context.Projects
            .Where(p => p.Id == query.ProjectId && p.OwnerId == userContext.UserId)
            .Select(p => new ProjectResponse
            {
                Id = p.Id,
                OwnerId = p.OwnerId,
                Name = p.Name,
                IsArchived = p.IsArchived,
                CreatedAt = p.CreatedAt,
                ArchivedAt = p.ArchivedAt
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            return Result.Failure<ProjectResponse>(ProjectErrors.NotFound(query.ProjectId));
        }

        return project;
    }
}
```

## Handler — paged list

Use `PagedList<T>.CreateAsync` (from `Application.Abstractions.Pagination`) on the projected query. Always apply a deterministic order before paging:

```csharp
IQueryable<ProjectResponse> projects = context.Projects
    .Where(p => p.OwnerId == userContext.UserId)
    .OrderByDescending(p => p.CreatedAt)
    .Select(p => new ProjectResponse { /* ... */ });

PagedList<ProjectResponse> page = await PagedList<ProjectResponse>.CreateAsync(
    projects, query.Page, query.PageSize, cancellationToken);

return page;
```
