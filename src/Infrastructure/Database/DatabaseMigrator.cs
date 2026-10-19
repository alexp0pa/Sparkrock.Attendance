using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Database;

// Applies pending EF Core migrations. The Migrator host and the integration tests both use it,
// so test databases are prepared through the same code path as a deployment.
public sealed class DatabaseMigrator(ApplicationDbContext dbContext, ILogger<DatabaseMigrator> logger)
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        string[] pendingMigrations = [.. await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)];

        if (pendingMigrations.Length == 0)
        {
            logger.LogInformation("Database is up to date; no migrations to apply");
            return;
        }

        logger.LogInformation(
            "Applying {Count} migration(s): {Migrations}",
            pendingMigrations.Length,
            string.Join(", ", pendingMigrations));

        await dbContext.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Migrations applied");
    }
}
