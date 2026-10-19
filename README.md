# Sparkrock.Attendance

A .NET 8 Web API built on a Clean Architecture template (SharedKernel → Domain → Application → Infrastructure → Web.Api), orchestrated with .NET Aspire and backed by SQL Server.

## Run

Prerequisites: .NET 8 SDK, Docker.

```bash
dotnet run --project src/AppHost
```

This starts SQL Server in a container, runs the Migrator (applies EF Core migrations, then exits), and starts the API once migrations succeeded. Open the Aspire dashboard link printed in the console for logs, traces and metrics, and Swagger UI at http://localhost:5000/swagger.

The API never migrates the database itself: with several instances running, startup migrations would race. In production, run the Migrator (`src/Migrator/Dockerfile`) once per deployment as a job or release-pipeline step, before rolling out the API.

## Test

```bash
dotnet test
```

Integration tests run against a throwaway SQL Server container (Testcontainers), so Docker must be running.

## Architecture

| Project | Responsibility |
|---|---|
| `SharedKernel` | `Result`/`Error`, `IDateTimeProvider` |
| `Domain` | Entities, error catalogs |
| `Application` | Use cases as vertical slices (custom CQRS handlers, FluentValidation, validation/logging decorators) |
| `Infrastructure` | EF Core + SQL Server, JWT authentication, authorization |
| `Web.Api` | Minimal-API endpoints, ProblemDetails, Swagger |
| `Migrator` | Applies EF Core migrations once per deployment, then exits |
| `ServiceDefaults` / `AppHost` | .NET Aspire: OpenTelemetry, health checks, local orchestration |

Architecture decisions are recorded in [`docs/adr/`](docs/adr/).

## Starting point

Generated with `dotnet new ca-simplified-api` from a personal .NET 8 Clean Architecture template, which is derived from Milan Jovanović's public Clean Architecture template.
