namespace Hrms.Core.Policies.DTRPolicies
{
    // Helper to safely get premium rate
    internal static class PremiumRateHelper
    {
        public static decimal GetRate(PayrollContext context, RateType type, decimal fallback)
        {
            if (!context.Payload.PremiumRates.TryGetValue(type, out var premiumRate))
            {
                premiumRate = fallback;
            }
            return premiumRate;
        }

        public static decimal GetHourlyRate(PayrollContext context)
        {
            return RateHelper.GetHourlyRate(context);
        }
    }

    public class RateHelper
    {
        public static decimal GetDailyRate(PayrollContext context)
        {
            return context.Employee.DailyRate;
        }

        public static decimal GetHourlyRate(PayrollContext context)
        {
            return context.Employee.DailyRate / (decimal)context.DailyRecord.ShiftWorkingHour;
            //if (context.Employee.SalaryType == SalaryType.MONTHLY_FIXED)
            //{
            //    var monthly = context.Employee.MonthlyRate;
            //    var daily = monthly / (context.Payload.CompanyPolicy.TotalDaysInaYear / 12);
            //    return daily / 8; // hourly
            //}
            //else
            //{
            //    return context.Employee.DailyRate / 8;
            //}
        }
    }
}