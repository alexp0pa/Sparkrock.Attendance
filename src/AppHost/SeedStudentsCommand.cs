using System.Globalization;
using Infrastructure;
using Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace AppHost;

// "Seed students" command on the database resource in the Aspire dashboard, for human testers:
// prompts for a school id and a number of students, then runs the same DevelopmentDataSeeder the Migrator uses
// (docs/adr/0011-student-school-and-seed-command.md). Local orchestration only - it never exists in a deployment.
internal static class SeedStudentsCommand
{
    private const string SchoolIdInput = "SchoolId";
    private const string CountInput = "Count";

    public static IResourceBuilder<SqlServerDatabaseResource> WithSeedStudentsCommand(
        this IResourceBuilder<SqlServerDatabaseResource> database) =>
        database.WithCommand(
            name: "seed-students",
            displayName: "Seed students",
            executeCommand: context => ExecuteAsync(database.Resource, context),
            commandOptions: new CommandOptions
            {
                Description = "Adds demo students to a school (DevelopmentDataSeeder).",
                IconName = "PeopleAdd",
                // Only once the database is up; migrations may still be running, so the seed waits for the Migrator
                // to have created the Students table before it is useful.
                UpdateState = context => context.ResourceSnapshot.HealthStatus is HealthStatus.Healthy
                    ? ResourceCommandState.Enabled
                    : ResourceCommandState.Disabled
            });

    private static async Task<ExecuteCommandResult> ExecuteAsync(
        SqlServerDatabaseResource database,
        ExecuteCommandContext context)
    {
        IInteractionService interactionService = context.ServiceProvider.GetRequiredService<IInteractionService>();

        if (!interactionService.IsAvailable)
        {
            return CommandResults.Failure("Prompts are unavailable; run the command from the Aspire dashboard.");
        }

        InteractionInput[] inputs =
        [
            new()
            {
                Name = SchoolIdInput,
                Label = "School ID",
                InputType = InputType.Number,
                Required = true,
                Placeholder = "e.g. 42"
            },
            new()
            {
                Name = CountInput,
                Label = "Number of students",
                InputType = InputType.Number,
                Required = true,
                Value = DevelopmentDataSeeder.DefaultStudentCount.ToString(CultureInfo.InvariantCulture)
            }
        ];

        InteractionResult<InteractionInputCollection> prompt = await interactionService.PromptInputsAsync(
            "Seed students",
            $"Adds demo students to a school (at most {DevelopmentDataSeeder.MaxStudentsPerRun} per run).",
            inputs,
            new InputsDialogInteractionOptions { ValidationCallback = ValidateAsync },
            context.CancellationToken);

        if (prompt.Canceled)
        {
            return CommandResults.Canceled();
        }

        int schoolId = ParseNumber(prompt.Data[SchoolIdInput].Value)!.Value;
        int count = ParseNumber(prompt.Data[CountInput].Value)!.Value;

        string? connectionString = await database.ConnectionStringExpression.GetValueAsync(context.CancellationToken);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Database"] = connectionString })
            .Build();

        // The same persistence registration the Migrator uses, so the seeder runs exactly as it does there.
        await using ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddDatabase(configuration)
            .BuildServiceProvider();

        await using AsyncServiceScope scope = services.CreateAsyncScope();

        IReadOnlyList<int> studentIds = await scope.ServiceProvider
            .GetRequiredService<DevelopmentDataSeeder>()
            .AddStudentsAsync(schoolId, count, context.CancellationToken);

        // Shown in the database resource's console log, so testers can see which ids to use.
        ILogger resourceLogger = context.ServiceProvider
            .GetRequiredService<ResourceLoggerService>()
            .GetLogger(database);

        resourceLogger.LogInformation(
            "Seeded {Count} students for school {SchoolId}: ids {FirstId}-{LastId}",
            studentIds.Count,
            schoolId,
            studentIds[0],
            studentIds[^1]);

        return CommandResults.Success();
    }

    private static Task ValidateAsync(InputsDialogValidationContext context)
    {
        InteractionInput schoolId = context.Inputs[SchoolIdInput];
        InteractionInput count = context.Inputs[CountInput];

        if (ParseNumber(schoolId.Value) is not >= 1)
        {
            context.AddValidationError(schoolId, "Enter a school ID of 1 or more.");
        }

        if (ParseNumber(count.Value) is not (>= 1 and <= DevelopmentDataSeeder.MaxStudentsPerRun))
        {
            context.AddValidationError(
                count,
                $"Enter a number between 1 and {DevelopmentDataSeeder.MaxStudentsPerRun}.");
        }

        return Task.CompletedTask;
    }

    private static int? ParseNumber(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) ? number : null;
}
