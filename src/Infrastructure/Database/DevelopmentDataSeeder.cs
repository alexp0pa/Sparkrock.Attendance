using Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Database;

// Inserts demo students for local development and manual testing (docs/adr/0009-student-entity.md,
// docs/adr/0011-student-school-and-seed-command.md): there is no roster API yet.
// Used by the Migrator (Development only) and by the AppHost's "Seed students" command - never in production.
public sealed class DevelopmentDataSeeder(ApplicationDbContext dbContext, ILogger<DevelopmentDataSeeder> logger)
{
    public const int DefaultSchoolId = 1;
    public const int DefaultStudentCount = 10;
    public const int MaxStudentsPerRun = 500;

    private static readonly string[] FirstNames =
        ["Ava", "Liam", "Olivia", "Noah", "Emma", "William", "Charlotte", "Lucas", "Amelia", "Benjamin"];

    private static readonly string[] LastNames =
        ["Martin", "Tremblay", "Roy", "Gagnon", "Côté", "Bouchard", "Gauthier", "Morin", "Lavoie", "Fortin"];

    // Migrator start-up: seeds the default demo students once, when the table is empty.
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await dbContext.Students.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Students already present; skipping development seed");
            return;
        }

        await AddStudentsAsync(DefaultSchoolId, DefaultStudentCount, cancellationToken);
    }

    // Adds `count` demo students to `schoolId` and returns their ids, in insertion order.
    public async Task<IReadOnlyList<int>> AddStudentsAsync(
        int schoolId,
        int count,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(schoolId, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxStudentsPerRun);

        // Continue the name sequence after the existing students, so repeated runs produce varied names.
        int offset = await dbContext.Students.CountAsync(cancellationToken);

        Student[] students = [.. Enumerable.Range(offset, count).Select(index => new Student
        {
            SchoolId = schoolId,
            FirstName = FirstNames[index % FirstNames.Length],
            LastName = LastNames[(index + index / FirstNames.Length) % LastNames.Length]
        })];

        dbContext.Students.AddRange(students);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Count} development students for school {SchoolId}", count, schoolId);

        return [.. students.Select(s => s.Id)];
    }
}
