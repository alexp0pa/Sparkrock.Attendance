using Web.Api.Middleware;

namespace Web.Api.Extensions;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        app.UseMiddleware<SecurityHeadersMiddleware>();

        return app;
    }
}
