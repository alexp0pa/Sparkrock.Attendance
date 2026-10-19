namespace Domain.Attendance;

// Fixed set of statuses (docs/adr/0003-attendance-statuses.md); absent/excused follow from the status.
public enum AttendanceStatus
{
    Present,
    Late,
    AbsentExcused,
    AbsentUnexcused
}
