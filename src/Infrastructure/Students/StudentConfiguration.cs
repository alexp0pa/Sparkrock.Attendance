using Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Students;

internal sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.FirstName).HasMaxLength(100);

        builder.Property(s => s.LastName).HasMaxLength(100);

        // Plain int like AttendanceRecord.SchoolId: there is no school table (ADR 0008).
        builder.HasIndex(s => s.SchoolId);
    }
}
