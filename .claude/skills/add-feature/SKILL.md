---
name: add-feature
description: Scaffold a complete Clean Architecture feature slice — command or query, custom handler, FluentValidation validator, minimal API endpoint, and tests (unit, validator, integration). Use when the user asks to add a feature, use case, command, query, or endpoint.
argument-hint: <feature description, e.g. "archive a project" or "get active projects for the current user">
---

# Add a Feature (Vertical Slice)

Scaffold a full use case following this solution's conventions: an Application-layer use case with a custom command/query handler, a Web.Api minimal-API endpoint, and tests. No MediatR — the codebase uses its own `ICommand`/`IQuery` abstractions with Scrutor-registered handlers and decorators.

## Workflow

1. **Classify the use case.** A state change is a **command**; a read is a **query**. Derive names from the pattern: verb + entity, e.g. `ArchiveProjectCommand`, `GetActiveProjectsQuery`.
2. **Check the Domain layer.** If the entity or its `{Entity}Errors` class doesn't exist, add it first (see the `add-entity` skill).
3. **Create the Application slice** in `src/Application/{Feature}/{UseCase}/` — command/query, handler, validator (commands only), response DTO (queries only). Templates: [references/command-slice.md](references/command-slice.md) and [references/query-slice.md](references/query-slice.md).
4. **Create the endpoint** in `src/Web.Api/Endpoints/{Feature}/{UseCase}.cs`. Template: [references/endpoint.md](references/endpoint.md).
5. **Write tests** — handler unit tests, validator tests, and an integration test. Templates: [references/tests.md](references/tests.md).
6. **Verify:** `dotnet build` then `dotnet test`. All three test projects must pass, including `ArchitectureTests` (layer dependency rules). Integration tests need Docker running.

## Non-negotiable conventions

- **Folder = use case.** One folder per use case under `src/Application/{Feature}/` (e.g. `Projects/Archive/`), containing all files for that slice.
- **Handlers are `internal sealed`** with primary constructors, implementing `ICommandHandler<TCommand>`, `ICommandHandler<TCommand, TResponse>`, or `IQueryHandler<TQuery, TResponse>`.
- **No manual DI registration.** Handlers, validators, and endpoints are discovered by assembly scanning (Scrutor, `AddValidatorsFromAssembly`, `AddEndpoints`). Never touch `DependencyInjection.cs` for a new slice.
- **Return `Result` / `Result<T>`, never throw** for expected failures. Errors come from static factories on `{Entity}Errors` in the Domain layer with codes like `"Projects.NotFound"`.
- **Validation lives in a `{Command}Validator`** (FluentValidation). It runs automatically via `ValidationDecorator` — the handler never validates input shape itself. Queries have no validators (the decorator only wraps commands).
- **Data access via `IApplicationDbContext`** (from `Application.Abstractions.Data`) — never reference Infrastructure from Application.
- **Ownership** for user-owned data: filter by `IUserContext.UserId`, or compare and return `UserErrors.Unauthorized()` on mismatch.
- **Ids are `int`** for every entity except `User`/`RefreshToken` (`Guid`): commands/queries/errors/DTOs take `int`, create commands are `ICommand<int>` and return the database-generated id after `SaveChangesAsync`, routes use `{id:int}` (`docs/adr/0002-integer-entity-ids.md`).
- **Time** comes from `IDateTimeProvider` — never `DateTime.UtcNow` in Application code.
- **Queries project directly to a `{X}Response` DTO** with `.Select(...)` — never return domain entities. List queries return `PagedList<T>` when the result set is unbounded.
- **Endpoints** implement `IEndpoint`, resolve the handler interface directly from DI, use `result.Match(Results.Ok, CustomResults.Problem)` (or `Results.NoContent` for void commands), tag with the `Tags` class, and call `.RequireAuthorization()` (or `.HasPermission(...)`).

## Naming reference

| Artifact | Pattern | Example |
|---|---|---|
| Command | `{Verb}{Entity}Command` | `ArchiveProjectCommand` |
| Query | `Get{X}Query` | `GetActiveProjectsQuery` |
| Handler | `{Command/Query}Handler` | `ArchiveProjectCommandHandler` |
| Validator | `{Command}Validator` | `ArchiveProjectCommandValidator` |
| Response | `{X}Response` | `ProjectResponse` |
| Endpoint | `{UseCase}.cs` in `Endpoints/{Feature}/` | `Endpoints/Projects/Archive.cs` |
| Unit test | `{Handler}Tests` | `ArchiveProjectCommandHandlerTests` |
| Test method | `Handle_Should_{Outcome}_When{Condition}` | `Handle_Should_ReturnNotFound_WhenProjectDoesNotExist` |
