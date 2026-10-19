using Domain.Attendance;

namespace Application.UnitTests.Attendance;

// docs/adr/0005-school-year.md: one configurable start (default September 1), label 'YYYY-YYYY'.
public sealed class SchoolYearTests
{
    [Theory]
    [InlineData(2025, 9, 1, 2025)]
    [InlineData(2026, 8, 31, 2025)]
    [InlineData(2026, 9, 1, 2026)]
    [InlineData(2026, 1, 15, 2025)]
    public void Containing_Should_ReturnYearStartingOnSeptemberFirst_WhenDefaultStart(
        int year, int month, int day, int expectedStartYear)
    {
        // Arrange
        var date = new DateOnly(year, month, day);

        // Act
        var schoolYear = SchoolYear.Containing(date, 9, 1);

        // Assert
        schoolYear.StartYear.ShouldBe(expectedStartYear);
        schoolYear.Contains(date).ShouldBeTrue();
    }

    [Fact]
    public void Containing_Should_UseConfiguredStart_WhenStartIsAugust15()
    {
        // Arrange
        var date = new DateOnly(2026, 8, 20);

        // Act
        var schoolYear = SchoolYear.Containing(date, 8, 15);

        // Assert
        schoolYear.Label.ShouldBe("2026-2027");
        schoolYear.Start.ShouldBe(new DateOnly(2026, 8, 15));
        schoolYear.EndExclusive.ShouldBe(new DateOnly(2027, 8, 15));
    }

    [Fact]
    public void Contains_Should_ExcludeEndDate()
    {
        // Arrange
        var schoolYear = SchoolYear.Containing(new DateOnly(2025, 10, 1), 9, 1);

        // Act & Assert
        schoolYear.Contains(new DateOnly(2026, 8, 31)).ShouldBeTrue();
        schoolYear.Contains(new DateOnly(2026, 9, 1)).ShouldBeFalse();
    }

    [Fact]
    public void TryParse_Should_ReturnSchoolYear_WhenLabelIsValid()
    {
        // Act
        bool parsed = SchoolYear.TryParse("2025-2026", 9, 1, out SchoolYear? schoolYear);

        // Assert
        parsed.ShouldBeTrue();
        schoolYear!.Start.ShouldBe(new DateOnly(2025, 9, 1));
        schoolYear.EndExclusive.ShouldBe(new DateOnly(2026, 9, 1));
        schoolYear.Label.ShouldBe("2025-2026");
    }

    [Fact]
    public void TryParse_Should_UseConfiguredStart_WhenStartIsAugust15()
    {
        // Act
        bool parsed = SchoolYear.TryParse("2025-2026", 8, 15, out SchoolYear? schoolYear);

        // Assert
        parsed.ShouldBeTrue();
        schoolYear!.Start.ShouldBe(new DateOnly(2025, 8, 15));
        schoolYear.EndExclusive.ShouldBe(new DateOnly(2026, 8, 15));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2025")]
    [InlineData("2025-2027")]
    [InlineData("2026-2025")]
    [InlineData("25-26")]
    [InlineData("abcd-efgh")]
    [InlineData("2025-2026-2027")]
    [InlineData("+202-2026")]
    public void TryParse_Should_Fail_WhenLabelIsInvalid(string? label)
    {
        // Act
        bool parsed = SchoolYear.TryParse(label, 9, 1, out SchoolYear? schoolYear);

        // Assert
        parsed.ShouldBeFalse();
        schoolYear.ShouldBeNull();
    }
}
