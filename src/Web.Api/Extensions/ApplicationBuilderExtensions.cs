namespace Web.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseSwaggerWithUi(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI();

        return app;
    }
}
