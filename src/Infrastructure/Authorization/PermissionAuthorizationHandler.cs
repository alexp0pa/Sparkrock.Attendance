using Microsoft.AspNetCore.Authorization;

namespace Infrastructure.Authorization;

internal sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // Unauthenticated users never satisfy a permission; the middleware turns this into a 401 challenge.
        if (context.User is not { Identity.IsAuthenticated: true })
        {
            return Task.CompletedTask;
        }

        // TODO: Placeholder - every authenticated user is granted every permission. Replace with a real
        // lookup (e.g. permission claims in the token, or a per-user query) once permissions are modeled.
        context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
