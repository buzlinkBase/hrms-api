
namespace Hrms.Core.Policies.DeductionPolicies;

public record SSSTablePayload(decimal EE, decimal ER, decimal EC);
internal static class SSSHelper
{
    public static SSSModel? GetTable(DeductionPayloadContext context, decimal gross)
    {
        var table = context.Payload.SSSTableModel
            .FirstOrDefault(x => x.RangeFrom <= gross && x.RangeTo >= gross);
        if (table == null) return null;

        var cap = StatutoryCapHelper.GetClientCap(context, StatutoryCapType.SSS);
        if (cap == null || table.EE <= cap) return table;

        // The employee's natural bracket exceeds the client's configured cap — downgrade to
        // the highest bracket that still respects it, so EE/ER/EC stay one self-consistent
        // government-table row (reads as an ordinary lower-bracket match, not a partial
        // override). Falls back to the lowest bracket if even that one exceeds the cap.
        return context.Payload.SSSTableModel
            .Where(x => x.EE <= cap)
            .OrderByDescending(x => x.EE)
            .FirstOrDefault()
            ?? context.Payload.SSSTableModel
            .OrderBy(x => x.EE)
            .FirstOrDefault();
    }
    public static DeductionPipeData ApplyTable(DeductionPayloadContext context, DeductionPipeData line, SSSTablePayload table, DateOnly applyToDate)
    {
        if (table == null || table.EE <= 0) return line;

        // Minimum Take-Home Pay — see DeductionValidator.CanApply. IsLimit is a pipeline-wide
        // stop flag (every policy/calculator checks it on entry), so hitting the floor here
        // halts PHIC/HDMF/WTax/ScheduledDeductions too, not just this one deduction.
        if (!DeductionValidator.CanApply(table.EE, line, context))
        {
            line.IsLimit = true;
            return line;
        }

        line.SSS = new SSSInfo
        {
            PayrollDate = applyToDate,
            EE = table.EE,
            ER = table.ER,
            EC = table.EC,
        };
        line.RunningTotal += table.EE;
        line.RemainingGrossBalance -= table.EE;
        return line;
    }

    public static (decimal EEBalance, decimal ERBalance, decimal ECBalance) GetBalance(DeductionPayloadContext context, decimal ee, decimal er, decimal ec)
    {
        // Client capping is already fully applied in GetTable (by downgrading to a smaller,
        // self-consistent bracket) — ee/er/ec here are already the capped values, so this
        // just nets them against whatever's already been posted this month, same as always.
        var contributions = GetCurrentMonthContribution(context);
        var eebalance = Math.Max(ee - contributions.Sum(x => x.EE), 0);
        var erbalance = Math.Max(er - contributions.Sum(x => x.ER), 0);
        var ecbalance = Math.Max(ec - contributions.Sum(x => x.EC), 0);
        return (eebalance, erbalance, ecbalance);
    }
    private static List<SSSContributionModel> GetCurrentMonthContribution(DeductionPayloadContext context)
    {
        context.Payload.SSSContribution.TryGetValue(new EmployeeKey(context.Employee.Id), out var accumulated);
        accumulated ??= new List<SSSContributionModel>();
        return accumulated;
    }
}
