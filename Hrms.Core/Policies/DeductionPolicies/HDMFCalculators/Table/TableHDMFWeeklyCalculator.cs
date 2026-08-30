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

            var scheduleAction = StatutoryScheduleResolver.Resolve(context, resolver);
            if (scheduleAction == StatutoryReleaseAction.ReleaseNothing) return line;

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
                    // Recompute the remaining-weeks-in-month divisor every time (not just
                    // "if nothing withheld yet") so it decreases correctly week over week
                    // once prior withholding is actually persisted, instead of sweeping the
                    // entire remaining balance the first time the ledger isn't empty.
                    divisor = context.Payload.FromDate.GetRemainingWeeksInMonth();

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

            // FirstHalfMonth/SecondHalfMonth on their matching cutoff — release the full
            // remaining balance now instead of spreading it across the remaining weeks.
            if (scheduleAction == StatutoryReleaseAction.ReleaseFullBalanceNow)
            {
                divisor = 1;
                daysWorked = null;
                totalDaysInMonth = null;
            }

            // Audit clarity
            line.Metadata["Divisor"] = divisor;
            if (daysWorked.HasValue) line.Metadata["Proration"] = $"{daysWorked}/{totalDaysInMonth}";

            var strategy = CutoffAllocationStrategyFactory.Resolve(context.Employee.SalaryType);
            var payload = new HDMFTablePayload(
                strategy.AllocateFirstCutoffShare(table.EmployeeShare, balances.EEBalance, context, divisor, daysWorked, totalDaysInMonth),
                strategy.AllocateFirstCutoffShare(table.EmployerShare, balances.ERBalance, context, divisor, daysWorked, totalDaysInMonth));

            return HDMFHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }
}