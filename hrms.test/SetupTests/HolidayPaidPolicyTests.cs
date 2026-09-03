using Hrms.Core.Services;

namespace hrms.test.SetupTests;

public class HolidayPaidPolicyTests
{
    [Fact]
    public void Resolve_LegalHoliday_IsAlwaysPaid()
    {
        HolidayPaidPolicy.Resolve(HolidayType.LEGAL).Should().BeTrue();
    }

    [Fact]
    public void Resolve_SpecialHoliday_IsAlwaysUnpaid()
    {
        HolidayPaidPolicy.Resolve(HolidayType.SPECIAL).Should().BeFalse();
    }
}
