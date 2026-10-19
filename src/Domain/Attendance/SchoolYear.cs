using System.Globalization;

namespace Domain.Attendance;

// The single implementation of the school-year rule (docs/adr/0005-school-year.md):
// a year runs from the configured start month/day up to the day before the same date a year later.
public sealed record SchoolYear(int StartYear, DateOnly Start, DateOnly EndExclusive)
{
    public string Label => $"{StartYear}-{StartYear + 1}";

    public bool Contains(DateOnly date) => date >= Start && date < EndExclusive;

    public static SchoolYear Containing(DateOnly date, int startMonth, int startDay)
    {
        var startThisYear = new DateOnly(date.Year, startMonth, startDay);

        int startYear = date >= startThisYear ? date.Year : date.Year - 1;

        return Create(startYear, startMonth, startDay);
    }

    public static bool TryParse(string? label, int startMonth, int startDay, out SchoolYear? schoolYear)
    {
        schoolYear = null;

        string[] parts = label?.Split('-') ?? [];

        if (parts.Length != 2 ||
            parts[0].Length != 4 ||
            parts[1].Length != 4 ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int startYear) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int endYear) ||
            endYear != startYear + 1 ||
            startYear < 1)
        {
            return false;
        }

        schoolYear = Create(startYear, startMonth, startDay);

        return true;
    }

    private static SchoolYear Create(int startYear, int startMonth, int startDay) =>
        new(
            startYear,
            new DateOnly(startYear, startMonth, startDay),
            new DateOnly(startYear + 1, startMonth, startDay));
}
