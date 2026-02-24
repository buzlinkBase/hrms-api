namespace Hrms.Core
{
    internal class RateProvider
    {
        public Dictionary<RateType, decimal> GetDefaultPremiumRates()
        {
            return new Dictionary<RateType, decimal>
            {
                // Base Rates
                [RateType.REGULAR] = RATE_DEFAULT.REGULAR,
                [RateType.NIGHTDIFF] = RATE_DEFAULT.NIGHTDIFF,
                [RateType.OVERTIME] = RATE_DEFAULT.OVERTIME,
                [RateType.RESTDAY_DUTY] = RATE_DEFAULT.RESTDAY_DUTY,
                [RateType.LEGAL_HOLIDAY] = RATE_DEFAULT.LEGAL_HOLIDAY,
                [RateType.LEGAL_HOLIDAY_DUTY] = RATE_DEFAULT.LEGAL_HOLIDAY_DUTY,
                [RateType.SPECIAL_WORKING] = RATE_DEFAULT.SPECIAL_WORKING,
                [RateType.SPECIAL_NON_WORKING] = RATE_DEFAULT.SPECIAL_NON_WORKING,
                [RateType.RESTDAY_SPECIAL] = RATE_DEFAULT.RESTDAY_SPECIAL
            };
        }
    }
}
