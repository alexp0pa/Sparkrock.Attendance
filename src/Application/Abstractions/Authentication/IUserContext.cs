namespace Application.Abstractions.Authentication;

public interface IUserContext
{
    Guid UserId { get; }

    // Email claim of the logged-in user; written to audit columns (docs/adr/0007-audit-columns.md).
    string Email { get; }
}
