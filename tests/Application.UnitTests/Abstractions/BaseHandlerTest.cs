using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Abstractions;

public abstract class BaseHandlerTest
{
    protected static TestDbContext CreateDbContext()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"clean-architecture-{Guid.NewGuid()}")
            .Options;

        return new TestDbContext(options);
    }
}
