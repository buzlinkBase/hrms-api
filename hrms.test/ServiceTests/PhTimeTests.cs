using Hrms.Core.Services;

namespace hrms.test.ServiceTests;

/// <summary>
/// Dashboard filters run on Philippine calendar days while CreatedAt is stored in UTC.
/// </summary>
public class PhTimeTests
{
    [Theory]
    [InlineData("2026-10-21T15:59:00", "2026-10-21")] // 23:59 in Manila
    [InlineData("2026-10-21T16:00:00", "2026-10-22")] // midnight in Manila
    [InlineData("2026-10-21T17:30:00", "2026-10-22")]
    public void Today_IsThePhilippineDate(string utc, string expected)
    {
        var utcNow = DateTime.SpecifyKind(DateTime.Parse(utc), DateTimeKind.Utc);

        PhTime.Today(utcNow).Should().Be(DateOnly.Parse(expected));
    }

    [Fact]
    public void StartOfDayUtc_IsManilaMidnightInUtc()
    {
        var start = PhTime.StartOfDayUtc(new DateOnly(2026, 10, 22));

        start.Should().Be(new DateTime(2026, 10, 21, 16, 0, 0, DateTimeKind.Utc));
        start.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void AsUtc_KeepsTheValueAndMarksItUtc()
    {
        var stored = new DateTime(2026, 10, 22, 1, 30, 0, DateTimeKind.Unspecified);

        var utc = PhTime.AsUtc(stored);

        utc.Ticks.Should().Be(stored.Ticks);
        utc.Kind.Should().Be(DateTimeKind.Utc);
    }
}
