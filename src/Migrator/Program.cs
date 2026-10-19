using Infrastructure;
using Infrastructure.Database;

// Applies pending EF Core migrations, then exits (non-zero on failure).
// Run it exactly once per deployment, before the API starts: Aspire orders it locally; in production
// use a one-off job or a release-pipeline step - not one instance per API replica, or migrations race.
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDatabase(builder.Configuration);

// Connection resiliency (retries while the database container is still starting) and tracing.
builder.EnrichSqlServerDbContext<ApplicationDbContext>();

using IHost host = builder.Build();

using IServiceScope scope = host.Services.CreateScope();

await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync();
