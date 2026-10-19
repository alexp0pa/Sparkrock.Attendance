IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// SQL Server runs in a persistent container with a data volume, so restarts are fast and data survives.
IResourceBuilder<SqlServerDatabaseResource> database = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume()
    .AddDatabase("Database", "app");

// The Migrator applies migrations once and exits; the API starts only after it completed successfully.
IResourceBuilder<ProjectResource> migrator = builder.AddProject<Projects.Migrator>("migrator")
    .WithReference(database)
    .WaitFor(database);

builder.AddProject<Projects.Web_Api>("web-api")
    .WithReference(database)
    .WaitForCompletion(migrator);

await builder.Build().RunAsync();
