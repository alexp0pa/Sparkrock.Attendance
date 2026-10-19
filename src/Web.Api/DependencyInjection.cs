using System.Text.Json.Serialization;
using Web.Api.Infrastructure;

namespace Web.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        // REMARK: If you want to use Controllers, you'll need this.
        services.AddControllers();

        // Enums travel as their names (docs/adr/0010-enums-as-strings-in-api.md).
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }
}
