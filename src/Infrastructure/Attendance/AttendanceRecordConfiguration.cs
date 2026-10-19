using Domain.Attendance;
using Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Attendance;

internal sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        // MinutesLate is required (> 0) for Late and empty otherwise (ADR 0003); the validator checks it first.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_AttendanceRecords_MinutesLate",
            "([Status] = 'Late' AND [MinutesLate] > 0) OR ([Status] <> 'Late' AND [MinutesLate] IS NULL)"));

        builder.HasKey(r => r.Id);

        // Stored by name (docs/adr/0010-enums-as-strings-in-api.md).
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        builder.Property(r => r.Notes).HasMaxLength(500);

        builder.Property(r => r.CreatedBy).HasMaxLength(256);

        builder.Property(r => r.ModifiedBy).HasMaxLength(256);

        // One record per student per date, whatever the school (ADR 0004, ADR 0008);
        // also serves the per-student school-year range queries.
        builder.HasIndex(r => new { r.StudentId, r.Date }).IsUnique();

        // Per-school chronic absenteeism list (ADR 0006, ADR 0008).
        builder.HasIndex(r => new { r.SchoolId, r.Date });

        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(r => r.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
