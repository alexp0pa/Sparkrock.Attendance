using System.Reflection;
using Application;
using HealthChecks.UI.Client;
using Infrastructure;
using Infrastructure.Database;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Web.Api;
using Web.Api.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Serilog owns the logging pipeline; writeToProviders forwards events to the OpenTelemetry
// logger provider registered by AddServiceDefaults, so logs also reach the Aspire dashboard.
builder.Logging.ClearProviders();

builder.AddServiceDefaults();

builder.Host.UseSerilog(
    (context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration),
    writeToProviders: true);

builder.Services.AddSwaggerGenWithAuth();

builder.Services
    .AddApplication()
    .AddPresentation()
    .AddInfrastructure(builder.Configuration);

// Adds health checks, connection resiliency, and tracing to the DbContext registered by Infrastructure.
builder.EnrichSqlServerDbContext<ApplicationDbContext>();

builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

WebApplication app = builder.Build();

app.MapEndpoints();

// Migrations are applied by the Migrator project (once per deployment), never by the API itself.
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerWithUi();
}

app.MapDefaultEndpoints();

app.MapHealthChecks("health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.UseSecurityHeaders();

app.UseSerilogRequestLogging();

app.UseExceptionHandler();

app.UseAuthentication();

app.UseAuthorization();

// REMARK: If you want to use Controllers, you'll need this.
app.MapControllers();

await app.RunAsync();

// REMARK: Required for functional and integration tests to work.
namespace Web.Api
{
    public partial class Program;
}
