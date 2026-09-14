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

        // Unlike GetRate, doesn't silently substitute a fallback -- callers that need to know
        // whether a rate was actually configured (client or company row exists) rather than
        // just "some value to use" (e.g. ClientOverrideOtRateStrategy, which must behave as a
        // no-op when nothing was explicitly set) should use this instead.
        public static bool TryGetRate(PayrollContext context, RateType type, out decimal rate)
        {
            return RateChain.TryResolve(context, type, out rate);
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