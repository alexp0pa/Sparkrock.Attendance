using System.Security.Claims;

namespace Infrastructure.Authentication;

internal static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal? principal)
    {
        string? userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out Guid parsedUserId) ?
            parsedUserId :
            throw new ApplicationException("User id is unavailable");
    }

    // The JWT "email" claim is mapped to ClaimTypes.Email by the bearer handler's inbound claim mapping.
    public static string GetEmail(this ClaimsPrincipal? principal)
    {
        string? email = principal?.FindFirstValue(ClaimTypes.Email);

        return !string.IsNullOrWhiteSpace(email) ?
            email :
            throw new ApplicationException("User email is unavailable");
    }
}
