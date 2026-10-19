---
name: ca-review
description: Review pending changes against this solution's Clean Architecture conventions — layer boundaries, Result-based error handling, slice structure, validation, endpoints, and test coverage. Use when the user asks to review changes, check conventions, or audit a feature before committing.
argument-hint: [optional: specific files or feature to review; defaults to the working-tree diff]
context: fork
---

# Clean Architecture Convention Review

You run in a forked context: review with fresh eyes, independent of the session that wrote the code. Review the given scope (default: `git diff HEAD` + untracked files) against the conventions below. Report findings with `file:line` references, ordered by severity. Do not fix anything — the main session decides what to change.

## Checklist

### Layer boundaries (violations are blockers)
- Domain references only `SharedKernel` — no EF Core, no Application/Infrastructure/Web.Api types.
- Application references Domain + SharedKernel only; data access exclusively through `IApplicationDbContext`; no `using Infrastructure.*` anywhere in Application.
- Persistence details (keys, lengths, conversions, indexes, relationships) live in Infrastructure `IEntityTypeConfiguration<>` classes, not on entities.
- Web.Api endpoints contain no business logic — only request→command mapping and result matching.

### Slice structure
- One folder per use case under `src/Application/{Feature}/{UseCase}/`; the endpoint mirrors it at `src/Web.Api/Endpoints/{Feature}/{UseCase}.cs`.
- Naming: `{Verb}{Entity}Command` / `Get{X}Query` / `...Handler` / `...Validator` / `{X}Response`.
- Handlers are `internal sealed` with primary constructors, implementing the custom `ICommandHandler<>`/`IQueryHandler<>` — no MediatR, no manual DI registration for handlers/validators/endpoints.
- Response DTOs are per-slice; queries project with `.Select(...)` and never return domain entities; unbounded lists are paged.

### Error handling
- Expected failures return `Result`/`Result<T>` — no exceptions for control flow, no try/catch around business rules.
- Errors come from static `{Entity}Errors` factories with `"{Feature}.{Reason}"` codes and the semantically correct type (`NotFound`/`Conflict`/`Problem`/`Failure`).
- Endpoints translate failures only via `result.Match(Results.Ok|NoContent, CustomResults.Problem)`.

### Validation & security
- Every command has a FluentValidation `{Command}Validator`; handlers don't re-check input shape (but do enforce business rules).
- Handlers acting on user-owned data enforce ownership: filter by `IUserContext.UserId` or return `UserErrors.Unauthorized()`.
- New endpoints call `.RequireAuthorization()` (or `.HasPermission(...)`) and `.WithTags(Tags.X)`.
- No `DateTime.UtcNow`/`DateTime.Now` in Application — use `IDateTimeProvider`.

### Data
- Strings that are indexed or bounded have `HasMaxLength`; uniqueness rules are backed by a unique index.
- A migration exists for every model change, and it matches the configuration.
- Migrations are applied only by the Migrator — any `Database.Migrate()`/`MigrateAsync()` in Web.Api is a blocker.
- No N+1 queries: related rows are loaded in one query, not per item in a loop.

### Tests
- New/changed handlers have unit tests covering every `Result.Failure` path plus the happy path (persisted state).
- New/changed validators have `TestValidate` tests per rule.
- New/changed endpoints have integration tests over real HTTP.
- Test naming and Arrange/Act/Assert structure match existing tests.

### Decisions
- If `docs/adr/` contains decision records, the code matches them; flag any divergence.

## Output format

Group findings as **Blockers** (layer violations, missing auth, thrown exceptions for expected failures, missing migration), **Convention violations** (naming, structure, error codes), and **Test gaps**. For each: `file:line`, what's wrong, and the one-line fix. Close with a verdict: ready to commit, or what must change first. If everything passes, say so and run `dotnet build` + `dotnet test` to confirm.
