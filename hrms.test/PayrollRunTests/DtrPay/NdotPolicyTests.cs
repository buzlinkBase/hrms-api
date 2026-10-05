using Hrms.Core.Calculators.DtrPay;

namespace hrms.test.PayrollRunTests.DtrPay;

public class NdotPolicyTests
{
    // Hourly 125, day rate 1.0, OT rate 1.25, ND rate 1.1 -- the worked example for both modes.
    private static readonly CategoryRates Rates = new(Hourly: 125m, Day: 1.0m, RawOt: 1.25m, Nd: 1.1m);

    [Fact]
    public void Additive_AlwaysPaysNothingForNdot()
    {
        var pay = new AdditiveNdotPolicy().Price(hours: 2m, Rates);

        pay.Should().Be(BucketPay.Zero);
    }

    [Fact]
    public void Compounded_PaysNdotHoursAtDayTimesOtTimesNd()
    {
        var pay = new CompoundedNdotPolicy().Price(hours: 2m, Rates);

        pay.Value.Should().Be(343.75m); // 2 x 125 x 1.0 x 1.25 x 1.1
    }
}
