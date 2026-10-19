using SharedKernel;

namespace IntegrationTests;

// A fixed "now" keeps date rules (today, current school year) deterministic whatever day the tests run.
public sealed class FixedDateTimeProvider : IDateTimeProvider
{
    // Monday 2026-03-16: the current school year (default start September 1) is 2025-2026.
    public static readonly DateTime FixedUtcNow = new(2026, 3, 16, 12, 0, 0, DateTimeKind.Utc);

    public static DateOnly Today => DateOnly.FromDateTime(FixedUtcNow);

    public DateTime UtcNow => FixedUtcNow;
}
