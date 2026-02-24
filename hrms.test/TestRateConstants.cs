using Hrms.Domain;
namespace hrms.test;

internal class TestRateProvider
{
    public static Dictionary<RateType, decimal> GetDefaultPremiumRates()
    {
        return new Dictionary<RateType, decimal>
        {
            // Base Rates for test coverage
            [RateType.REGULAR] = TEST_RATE_DEFAULT.REGULAR,
            [RateType.NIGHTDIFF] = TEST_RATE_DEFAULT.NIGHTDIFF,
            [RateType.OVERTIME] = TEST_RATE_DEFAULT.OVERTIME,
            [RateType.RESTDAY_DUTY] = TEST_RATE_DEFAULT.RESTDAY_DUTY,
            [RateType.LEGAL_HOLIDAY] = TEST_RATE_DEFAULT.LEGAL_HOLIDAY,
            [RateType.LEGAL_HOLIDAY_DUTY] = TEST_RATE_DEFAULT.LEGAL_HOLIDAY_DUTY,
            [RateType.SPECIAL_WORKING] = TEST_RATE_DEFAULT.SPECIAL_WORKING,
            [RateType.SPECIAL_NON_WORKING] = TEST_RATE_DEFAULT.SPECIAL_NON_WORKING,
            [RateType.RESTDAY_SPECIAL] = TEST_RATE_DEFAULT.RESTDAY_SPECIAL
        };
    }
}

internal static class TEST_RATE_DEFAULT
{
    // BASE RATES
    public const decimal REGULAR = 1.00m;
    public const decimal NIGHTDIFF = 1.10m;
    public const decimal OVERTIME = 1.25m;
    public const decimal RESTDAY_DUTY = 1.30m;
    public const decimal LEGAL_HOLIDAY = 1.00m; // holiday pay (no work)
    public const decimal LEGAL_HOLIDAY_DUTY = 2.00m;
    public const decimal SPECIAL_WORKING = 1.00m; // same as regular
    public const decimal SPECIAL_NON_WORKING = 1.30m;
    public const decimal RESTDAY_SPECIAL = 1.50m; // rest day + special non-working
}