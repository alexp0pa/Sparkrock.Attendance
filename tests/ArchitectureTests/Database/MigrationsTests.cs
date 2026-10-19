using Infrastructure;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace ArchitectureTests.Database;

public class MigrationsTests
{
    // Compares the EF Core model with the latest migration snapshot - no database needed, so it runs
    // with the fast test suites (and the Stop hook) and catches a model change without a migration.
    [Fact]
    public void Model_Should_HaveNoPendingChanges()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        using ServiceProvider services = new ServiceCollection()
            .AddDatabase(configuration)
            .BuildServiceProvider();

        using IServiceScope scope = services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.Database.HasPendingModelChanges().ShouldBeFalse(
            "The EF Core model has changes that no migration captures. Add one with /add-migration.");
    }
}
