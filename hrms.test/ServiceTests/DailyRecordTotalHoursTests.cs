namespace hrms.test.ServiceTests;

public class DailyRecordTotalHoursTests
{
    [Fact]
    public void AllZero_ReturnsZero()
    {
        new DailyRecord().TotalHours.Should().Be(0);
    }

    [Fact]
    public void SumsAcrossCategories()
    {
        var record = new DailyRecord
        {
            RegularNetHours = 8,
            RegularOTHours = 2,
            RestDayHours = 4,
            LegalHolHours = 8,
            DoubleLegalOTHours = 1,
        };

        record.TotalHours.Should().Be(23);
    }

    [Fact]
    public void IgnoresNonWorkedHourFields()
    {
        var record = new DailyRecord
        {
            RegularNetHours = 8,
            OBHours = 8,
            PaidLeaveHours = 8,
            UnpaidLeaveHours = 8,
        };

        record.TotalHours.Should().Be(8);
    }
}
