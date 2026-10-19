namespace Application.Attendance;

// Global attendance settings, bound from the "Attendance" configuration section.
// Threshold: docs/adr/0006-chronic-absenteeism.md; school-year start: docs/adr/0005-school-year.md.
public sealed class AttendanceOptions
{
    public const string SectionName = "Attendance";

    public int ChronicAbsenceThreshold { get; set; } = 10;

    public int SchoolYearStartMonth { get; set; } = 9;

    // 1-28 so the start date exists in every year (no February 29).
    public int SchoolYearStartDay { get; set; } = 1;

    public void Validate()
    {
        if (ChronicAbsenceThreshold < 1)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(ChronicAbsenceThreshold)} must be at least 1.");
        }

        if (SchoolYearStartMonth is < 1 or > 12 || SchoolYearStartDay is < 1 or > 28)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(SchoolYearStartMonth)} must be 1-12 and " +
                $"{SectionName}:{nameof(SchoolYearStartDay)} must be 1-28.");
        }
    }
}
