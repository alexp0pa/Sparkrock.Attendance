---
name: add-migration
description: Create and apply an EF Core migration (SQL Server). Use when the user changes the model (adds or edits an entity or its configuration) and needs a migration, or asks to update the database schema.
argument-hint: <migration name or a description of the model change, e.g. "Add_Projects">
---

# Add an EF Core Migration

Migrations live in the **Infrastructure** project (`src/Infrastructure/Database/Migrations/`); **Web.Api** is the startup project (it owns configuration and DI). The EF tool is pinned in the local tool manifest (`.config/dotnet-tools.json`) to match EF Core 8 — always invoke it as `dotnet tool run dotnet-ef`, never a global `dotnet ef` (a newer global tool can't load the .NET 8 app).

## Steps

1. Finish the model change first — entity + `IEntityTypeConfiguration` + a `DbSet` on `ApplicationDbContext`, `IApplicationDbContext`, and `TestDbContext` (see `/add-entity`). The migration reads the model, so it must compile.

2. Create the migration from the repo root:

   ```
   dotnet tool restore
   dotnet tool run dotnet-ef migrations add {Migration_Name} --project src/Infrastructure --startup-project src/Web.Api --output-dir Database/Migrations
   ```

   Names are `PascalCase_With_Underscores` (e.g. `Add_Projects`, `Add_Project_Archiving`).

3. Review the generated `Up`/`Down` — column types (bounded string lengths, not max-length columns), indexes, foreign keys, and that nothing unexpected was dropped. Generated files are excluded from analyzers via `.editorconfig`, so don't hand-format them.

4. Verify: `dotnet test tests/ArchitectureTests` — `MigrationsTests.Model_Should_HaveNoPendingChanges` fails whenever the model has changes no migration captures (the Stop hook runs it too).

5. Apply it. The **Migrator** project applies migrations (via `DatabaseMigrator` in Infrastructure, which the integration tests also use) — the API never does. `dotnet run --project src/AppHost` runs the Migrator against the Aspire-managed SQL Server before starting the API. Integration tests migrate their own Testcontainers database automatically. No database is needed to *create* a migration. Never add `Database.Migrate()` calls to the API.

## Rules

- Never edit a migration that has already been applied/committed — add a new one.
- To drop the last **unapplied** migration: `dotnet tool run dotnet-ef migrations remove --project src/Infrastructure --startup-project src/Web.Api`.
- Commit the migration together with the model change.
