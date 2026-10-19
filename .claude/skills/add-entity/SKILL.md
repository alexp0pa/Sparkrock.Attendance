---
name: add-entity
description: Add a new domain entity — entity class, error catalog, EF Core configuration, DbContext wiring, and migration. Use when the user asks to add an entity, aggregate, domain model, or table.
argument-hint: <entity description, e.g. "Project with a name, owner, and archived flag">
---

# Add a Domain Entity

Create a new entity and wire it through every layer.

## Files to create/modify

1. **Entity** — `src/Domain/{Feature}/{Entity}.cs`

```csharp
namespace Domain.Projects;

public sealed class Project
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
}
```

Plain `sealed class` (no base class), `Guid Id`, settable properties, collections initialized with `= [];`. There are no domain events in this solution: side effects of a change happen explicitly in the command handler that makes it.

2. **Error catalog** — `src/Domain/{Feature}/{Entity}Errors.cs`

```csharp
using SharedKernel;

namespace Domain.Projects;

public static class ProjectErrors
{
    public static Error NotFound(Guid projectId) => Error.NotFound(
        "Projects.NotFound",
        $"The project with the Id = '{projectId}' was not found");
}
```

Codes are `"{FeaturePlural}.{Reason}"`. Pick the factory by semantics: `Error.NotFound` (404), `Error.Conflict` (409), `Error.Problem` (400), `Error.Failure` (500).

3. **EF configuration** — `src/Infrastructure/{Feature}/{Entity}Configuration.cs`

```csharp
using Domain.Projects;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Projects;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(200);

        builder.HasIndex(p => new { p.OwnerId, p.Name }).IsUnique();

        builder.HasOne<User>().WithMany().HasForeignKey(p => p.OwnerId);
    }
}
```

SQL Server specifics:
- Give every `string` a `HasMaxLength(...)` — bounded columns document the domain's limits and index well; unbounded strings become max-length/`text` columns.
- Uniqueness rules belong in a unique index here, in addition to any check in the handler.
- Relationships are configured shadow-style (`HasOne<User>().WithMany()`) — entities hold foreign-key ids, not navigation properties. Configurations are picked up automatically by `ApplyConfigurationsFromAssembly`.

4. **DbContext wiring** — add `DbSet<{Entity}> {Plural}` to **all three**:
   - `src/Application/Abstractions/Data/IApplicationDbContext.cs` (`DbSet<Project> Projects { get; }`)
   - `src/Infrastructure/Database/ApplicationDbContext.cs` (`public DbSet<Project> Projects { get; set; }`)
   - `tests/Application.UnitTests/Abstractions/TestDbContext.cs` (keeps handlers unit-testable)

5. **Migration** — use the `add-migration` skill (`Add_{Plural}`).

## Rules

- The Domain project references only `SharedKernel` — no EF, no Application types. Persistence concerns (keys, lengths, conversions, indexes, relationships) live exclusively in the Infrastructure configuration.
- Run `dotnet build` and `dotnet test` when done — `ArchitectureTests` enforce the layer rules.
- If the user also wants use cases for the entity, continue with the `add-feature` skill.
