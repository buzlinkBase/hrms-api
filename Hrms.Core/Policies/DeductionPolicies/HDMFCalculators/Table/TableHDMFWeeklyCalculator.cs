namespace Hrms.Core.Policies.DeductionPolicies
{
    public class TableHDMFWeeklyCalculator : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var resolver = new CutoffPolicyResolver();

            if (line.IsLimit) return line;

            var baseRate = StatutoryHelper.GetWeeklyGrossBaseRate(context);
            var table = HDMFHelper.GetTable(context, baseRate);
            if (table == null) return line;

            var balances = HDMFHelper.GetBalance(context, table.EmployeeShare, table.EmployerShare);

            if (balances.EEBalance == 0) return line;
            if (line.RemainingGrossBalance < balances.EEBalance) return line;

            int divisor = 1;
            int? daysWorked = null;
            int? totalDaysInMonth = null;

            try
            {
                if (new IsCrossMonth().IsSatisfiedBy(context.Payload))
                {
                    if (StatutoryHelper.IsHiredThisMonth(context))
                    {
                        totalDaysInMonth = DateTime.DaysInMonth(context.Payload.FromDate.Year, context.Payload.FromDate.Month);
                        daysWorked = (context.Payload.ToDate.ToDateTime(TimeOnly.MinValue) -
                                      context.Employee.HireDate.ToDateTime(TimeOnly.MinValue)).Days + 1;
                    }
                    else
                    {
                        divisor = 1; // already active → deduct all
                    }
                }
                else
                {
                    //var currentCutoff = resolver.GetCurrentCutoff(context);

                    if (table.EmployeeShare == balances.EEBalance)
                    {
                        divisor = context.Payload.FromDate.GetNumberOfWeeksInMonth();
                    }

                    if (StatutoryHelper.IsHiredThisMonth(context))
                    {
                        totalDaysInMonth = DateTime.DaysInMonth(context.Payload.FromDate.Year, context.Payload.FromDate.Month);
                        daysWorked = (context.Payload.ToDate.ToDateTime(TimeOnly.MinValue) -
                                      context.Employee.HireDate.ToDateTime(TimeOnly.MinValue)).Days + 1;

                        // If hired in last week → proration will apply
                        if (!(context.Payload.FromDate.IsLastWeekOfMonth() || context.Payload.ToDate.IsLastWeekOfMonth()))
                        {
                            divisor = context.Payload.FromDate.GetRemainingWeeksInMonth();
                        }
                    }
                }
            }
            catch (CutoffMismatchException ex)
            {
                //AuditLogger.Warn(ex.Message);
                divisor = 1;
            }

            // Audit clarity
            line.Metadata["Divisor"] = divisor;
            if (daysWorked.HasValue) line.Metadata["Proration"] = $"{daysWorked}/{totalDaysInMonth}";

            var payload = new HDMFTablePayload(
                StatutoryHelper.CalcRemainingBalance(table.EmployeeShare, balances.EEBalance, divisor, daysWorked, totalDaysInMonth),
                StatutoryHelper.CalcRemainingBalance(table.EmployerShare, balances.ERBalance, divisor, daysWorked, totalDaysInMonth));

            return HDMFHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }
}