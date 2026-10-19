namespace Domain.Attendance;

public sealed class AttendanceRecord
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int SchoolId { get; set; }
    public DateOnly Date { get; set; }
    public AttendanceStatus Status { get; set; }
    public int? MinutesLate { get; set; }
    public string? Notes { get; set; }
    public string CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
}
