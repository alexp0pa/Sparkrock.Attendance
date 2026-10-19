using Application.Abstractions.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database;

internal sealed class SqlServerExceptionClassifier : IDbExceptionClassifier
{
    // 2601: duplicate key in a unique index; 2627: unique/primary key constraint violation.
    private const int DuplicateKeyInUniqueIndex = 2601;
    private const int UniqueConstraintViolation = 2627;

    public bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: DuplicateKeyInUniqueIndex or UniqueConstraintViolation };
}
