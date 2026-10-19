using Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Database;

// Inserts demo students for local development (docs/adr/0009-student-entity.md): there is no roster API yet.
// Called by the Migrator only in the Development environment - production migrations carry no demo data.
public sealed class DevelopmentDataSeeder(ApplicationDbContext dbContext, ILogger<DevelopmentDataSeeder> logger)
{
    private static readonly (string FirstName, string LastName)[] DemoStudents =
    [
        ("Ava", "Martin"),
        ("Liam", "Tremblay"),
        ("Olivia", "Roy"),
        ("Noah", "Gagnon"),
        ("Emma", "Côté"),
        ("William", "Bouchard"),
        ("Charlotte", "Gauthier"),
        ("Lucas", "Morin"),
        ("Amelia", "Lavoie"),
        ("Benjamin", "Fortin")
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await dbContext.Students.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Students already present; skipping development seed");
            return;
        }

        dbContext.Students.AddRange(DemoStudents.Select(s => new Student
        {
            FirstName = s.FirstName,
            LastName = s.LastName
        }));

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Count} development students", DemoStudents.Length);
    }
}
