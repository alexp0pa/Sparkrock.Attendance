# Endpoint Templates

One file per use case in `src/Web.Api/Endpoints/{Feature}/{UseCase}.cs`. Endpoints implement `IEndpoint` and are auto-discovered by `AddEndpoints`/`MapEndpoints` — no registration needed. They appear in Swagger UI (`/swagger`) automatically.

## Command with response body (POST → 200 + value)

```csharp
using Application.Abstractions.Messaging;
using Application.Projects.Create;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Projects;

internal sealed class Create : IEndpoint
{
    public sealed class Request
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public DateTime? DueDate { get; set; }
        public List<string> Tags { get; set; } = [];
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("projects", async (
            Request request,
            ICommandHandler<CreateProjectCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateProjectCommand
            {
                Name = request.Name,
                Description = request.Description,
                DueDate = request.DueDate,
                Tags = request.Tags
            };

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Projects)
        .RequireAuthorization();
    }
}
```

## Void command from route parameter (PUT/DELETE → 204)

```csharp
using Application.Abstractions.Messaging;
using Application.Projects.Archive;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Projects;

internal sealed class Archive : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("projects/{id:guid}/archive", async (
            Guid id,
            ICommandHandler<ArchiveProjectCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new ArchiveProjectCommand(id);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Projects)
        .RequireAuthorization();
    }
}
```

## Query (GET → 200), with optional paging parameters

```csharp
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Projects.Get;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Projects;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("projects", async (
            string? search,
            int? page,
            int? pageSize,
            IQueryHandler<GetProjectsQuery, PagedList<ProjectResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetProjectsQuery(search, page ?? 1, pageSize ?? 20);

            Result<PagedList<ProjectResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Projects)
        .RequireAuthorization();
    }
}
```

## Rules

- Routes are lowercase, plural, no leading slash: `projects`, `projects/{id:guid}`, `users/{userId:guid}/projects`. Route constraints (`:guid`, `:int`, `:datetime`) on all typed parameters.
- The nested `Request` class exists only when there's a JSON body; it maps 1:1 to the command inside the lambda (enum values arrive as `int` and are cast).
- Resolve the handler interface (`ICommandHandler<...>` / `IQueryHandler<...>`) directly as a lambda parameter — the decorated instance is injected.
- Always end with `.WithTags(Tags.{Feature})` and `.RequireAuthorization()` (or `.HasPermission(Permissions.X)` where a permission constant exists). Add the feature constant to `Tags.cs` if new.
- Failures never get hand-rolled responses — `CustomResults.Problem` translates the `Error` to RFC 7807 ProblemDetails with the right status code.
- Endpoints contain no business logic: map request → command/query, call the handler, match the result.
