namespace Hrms.Core.Policies.DeductionPolicies
{
    public class TableHDMFMonthlyCalculator : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var resolver = new CutoffPolicyResolver();

            var baseRate = StatutoryHelper.GetMonthlyGrossBaseRate(context);
            var rate = HDMFHelper.GetTable(context, baseRate);
            if (rate == null) return line;

            var balances = HDMFHelper.GetBalance(context, rate.EmployeeShare, rate.EmployerShare);

            int divisor = 1; // default full deduction
            int? daysWorked = null;
            int? totalDaysInMonth = null;

            try
            {
                // Cross-month payroll
                if (new IsCrossMonth().IsSatisfiedBy(context.Payload))
                {
                    if (StatutoryHelper.IsHiredThisMonth(context))
                    {
                        totalDaysInMonth = DateTime.DaysInMonth(context.Payload.FromDate.Year, context.Payload.FromDate.Month);
                        daysWorked = (context.Payload.ToDate.ToDateTime(TimeOnly.MinValue) -
                                      context.Employee.HireDate.ToDateTime(TimeOnly.MinValue)).Days + 1;
                        divisor = 0; // proration path
                    }
                    else
                    {
                        divisor = 1; // already active → deduct all
                    }
                }
                else if (StatutoryHelper.IsHiredThisMonth(context))
                {
                    totalDaysInMonth = DateTime.DaysInMonth(context.Payload.FromDate.Year, context.Payload.FromDate.Month);
                    daysWorked = (context.Payload.ToDate.ToDateTime(TimeOnly.MinValue) -
                                  context.Employee.HireDate.ToDateTime(TimeOnly.MinValue)).Days + 1;

                    var currentCutoff = resolver.GetCurrentCutoff(context);
                    if (context.Employee.HireDate.Day > currentCutoff.Day)
                    {
                        divisor = 0; // proration path
                    }
                }
            }
            catch (CutoffMismatchException ex)
            {
                //AuditLogger.Warn(ex.Message);
                divisor = 1; // fallback
            }

            // Audit clarity
            if (daysWorked.HasValue)
                line.Metadata["Proration"] = $"{daysWorked}/{totalDaysInMonth}";
            else
                line.Metadata["Divisor"] = divisor;

            var payload = new HDMFTablePayload(
                StatutoryHelper.CalcRemainingBalance(rate.EmployeeShare, balances.EEBalance, divisor, daysWorked, totalDaysInMonth),
                StatutoryHelper.CalcRemainingBalance(rate.EmployerShare, balances.ERBalance, divisor, daysWorked, totalDaysInMonth));

            return HDMFHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }
}