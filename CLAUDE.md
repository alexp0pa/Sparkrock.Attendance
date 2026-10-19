# Clean Architecture (.NET 8) — notes for Claude Code

Working conventions for this solution. Match them so generated code looks hand-written.

## Architecture (dependencies point inward)

- **SharedKernel** — `Result`/`Result<T>`, `Error`/`ErrorType`, `ValidationError`, `IDateTimeProvider`. No dependencies.
- **Domain** — entities (plain `sealed` classes, no base class) and error catalogs (`{Entity}Errors`). References SharedKernel only. No EF, no application types. No domain events — side effects are explicit in command handlers.
- **Application** — use cases as vertical slices: `{Feature}/{UseCase}/`. MediatR-free `ICommand`/`IQuery` + handlers, FluentValidation validators, validation + logging decorators (Scrutor), abstractions (`IApplicationDbContext`, `IUserContext`, `ITokenProvider`, `IPasswordHasher`), `PagedList<T>`. References Domain + SharedKernel.
- **Infrastructure** — EF Core 8 + SQL Server, migrations (`Database/Migrations`), JWT auth + refresh tokens, permission authorization. References Application.
- **Web.Api** — minimal-API endpoints (`IEndpoint`, auto-discovered), Swagger UI, global exception handling + `ProblemDetails`, security headers, Serilog. References Infrastructure + ServiceDefaults.
- **Migrator** — worker that applies pending EF Core migrations once and exits (non-zero on failure). The API **never** migrates the database. Run the Migrator exactly once per deployment (a job/pipeline step), never one per API instance.
- **ServiceDefaults** — Aspire defaults: OpenTelemetry (OTLP), health checks, service discovery, HTTP resilience.
- **AppHost** — Aspire orchestration: SQL Server container (persistent, with data volume) → Migrator → Web.Api (starts only after the Migrator completed).

`tests/ArchitectureTests` enforces the layer rules — fix the dependency, never the test — and checks that the EF Core model has no changes missing a migration (`MigrationsTests`; fix it with `/add-migration`).

## Legacy system (`legacy/`) — read-only reference

- `legacy/` holds the old system. It is **read-only**: read it to understand existing behaviour, and cite `file:line` whenever you rely on it.
- **Never edit it**, and **never port or wrap** any of its code in the new .NET projects. The new system is designed from scratch.
- Enforced by `Edit`/`Write` deny rules on `legacy/**` in `.claude/settings.json`.

## Conventions

- **CQRS, no MediatR** — `ICommand`/`ICommand<T>` + `ICommandHandler<...>`, `IQuery<T>` + `IQueryHandler<Q,R>`. Handlers are `internal sealed` with primary constructors, auto-registered by Scrutor scanning; wrapped by `ValidationDecorator` + `LoggingDecorator`.
- **Errors, not exceptions** — return `Result.Failure(...)` with an `Error` for expected failures. Error codes are `"{FeaturePlural}.{Reason}"`. Endpoints translate with `result.Match(Results.Ok, CustomResults.Problem)`.
- **Endpoints** implement `IEndpoint`; protect with `.RequireAuthorization()` / `.HasPermission("...")`; tag with `Tags`.
- **Validation** — FluentValidation `AbstractValidator<TCommand>`; runs in the validation decorator.
- **Data** — inject `IApplicationDbContext`; entities use FK ids (shadow relationships), no navigations. Configurations in `Infrastructure/{Feature}/` are auto-applied. Bound every string with `HasMaxLength`.
- **Time** — `IDateTimeProvider`, never `DateTime.UtcNow` in Application.
- **List queries** return `PagedList<T>`.
- **Decisions** — record significant architectural decisions as ADRs in `docs/adr/` (copy `0000-template.md`).
- **`docs/adr/` is the source of truth for architectural decisions.** Follow the accepted ADRs; when code, skills or this file disagree with an ADR, the ADR wins — change a decision only with a new ADR that supersedes the old one.

## Build, run & test

- `dotnet build` — **analyzer-strict**: `TreatWarningsAsErrors` + SonarAnalyzer. Keep code warning-clean; prefer collection expressions (`[.. x]`) and `ToUpperInvariant`.
- `dotnet test` — integration tests use Testcontainers (SQL Server), so Docker must be running.
- `dotnet run --project src/AppHost` — starts SQL Server + the API; the Aspire dashboard shows logs/traces; Swagger UI at `http://localhost:5000/swagger`. The Migrator applies migrations before the API starts.
- EF tooling is a pinned local tool: `dotnet tool restore`, then `dotnet tool run dotnet-ef ...` (see `/add-migration`).

## Claude Code setup (`.claude/`)

- **Hooks:** a Stop hook (`quality-gate.ps1`) builds and runs unit + architecture tests before a turn ends and feeds failures back; a PreToolUse hook (`guard-bash.ps1`) blocks destructive commands (force-push, hard reset, git clean, database drop).
- **Skills (prefer these):** `/add-entity` · `/add-migration` · `/add-feature` · `/add-tests` · `/ca-review` (runs in a forked context for an independent review).
