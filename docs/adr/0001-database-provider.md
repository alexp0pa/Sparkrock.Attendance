# 0001. Database provider: SQL Server

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

The `ca-simplified-api` template needs a database provider chosen when the project is generated. SQL Server is the organization standard.

## Decision

Sparkrock.Attendance uses SQL Server, because it is the organization standard.

## Alternatives considered

- **PostgreSQL**: supported by the template, but not the organization standard.

## Consequences

- EF Core uses `Microsoft.EntityFrameworkCore.SqlServer` (with `Aspire.Microsoft.EntityFrameworkCore.SqlServer`), and migrations are SQL Server-specific.
- Integration tests use `Testcontainers.MsSql` with the `mcr.microsoft.com/mssql/server:2022-latest` image.
- Aspire AppHost starts the database with `AddSqlServer("sql")` (`Aspire.Hosting.SqlServer`).
- Switching providers later would mean regenerating migrations and changing these packages and resources.
