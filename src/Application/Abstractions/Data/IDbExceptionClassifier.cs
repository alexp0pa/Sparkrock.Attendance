using Microsoft.EntityFrameworkCore;

namespace Application.Abstractions.Data;

// Lets handlers react to a specific database failure without depending on the database provider.
public interface IDbExceptionClassifier
{
    bool IsUniqueConstraintViolation(DbUpdateException exception);
}
