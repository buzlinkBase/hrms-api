namespace Hrms.Core.Policies.DTRPolicies
{
    // Helper to safely get premium rate
    internal static class PremiumRateHelper
    {
        // Chain of Responsibility: client-specific override -> global rate -> caller's
        // fallback. Handlers are stateless once linked, so one shared chain instance is
        // safe to reuse across every call.
        private static readonly IPremiumRateHandler RateChain = BuildChain();

        private static IPremiumRateHandler BuildChain()
        {
            var client = new ClientPremiumRateHandler();
            var global = new GlobalPremiumRateHandler();
            client.SetNext(global);
            return client;
        }

        public static decimal GetRate(PayrollContext context, RateType type, decimal fallback)
        {
            return RateChain.TryResolve(context, type, out var rate) ? rate : fallback;
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
        }
    }
}