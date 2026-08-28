namespace Hrms.Core.Policies.DTRPolicies
{
    /// <summary>
    /// GoF Chain of Responsibility: each handler either resolves the premium rate itself
    /// or defers to the next handler in the chain. New override tiers (e.g. a future
    /// department-level rate) plug in as one more handler without touching the others.
    /// </summary>
    public interface IPremiumRateHandler
    {
        IPremiumRateHandler SetNext(IPremiumRateHandler next);
        bool TryResolve(PayrollContext context, RateType type, out decimal rate);
    }

    public abstract class PremiumRateHandlerBase : IPremiumRateHandler
    {
        private IPremiumRateHandler? _next;

        public IPremiumRateHandler SetNext(IPremiumRateHandler next)
        {
            _next = next;
            return next;
        }

        public bool TryResolve(PayrollContext context, RateType type, out decimal rate)
        {
            if (TryResolveSelf(context, type, out rate)) return true;
            return _next != null && _next.TryResolve(context, type, out rate);
        }

        protected abstract bool TryResolveSelf(PayrollContext context, RateType type, out decimal rate);
    }

    /// <summary>First link: the employee's client, if it has its own override for this RateType.</summary>
    public class ClientPremiumRateHandler : PremiumRateHandlerBase
    {
        protected override bool TryResolveSelf(PayrollContext context, RateType type, out decimal rate)
        {
            var clientId = context.Employee?.ClientId;
            if (clientId.HasValue &&
                context.Payload.ClientPremiumRates.TryGetValue(new ClientRateKey(clientId.Value, type), out rate))
            {
                return true;
            }
            rate = default;
            return false;
        }
    }

    /// <summary>Second link: the company-wide rate table.</summary>
    public class GlobalPremiumRateHandler : PremiumRateHandlerBase
    {
        protected override bool TryResolveSelf(PayrollContext context, RateType type, out decimal rate)
        {
            return context.Payload.PremiumRates.TryGetValue(type, out rate);
        }
    }
}
