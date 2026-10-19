using Domain.Attendance;
using Domain.Students;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Application.Abstractions.Data;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Student> Students { get; }
    DbSet<AttendanceRecord> AttendanceRecords { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
