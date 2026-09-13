
namespace Hrms.Core.Policies.DeductionPolicies;

public record PHICTablePayload(decimal EE, decimal ER);
internal static class PHICHelper
{
    public static PHICModel? GetTable(DeductionPayloadContext context, decimal gross)
    {
        var table = context.Payload.PHICTableModel
            .FirstOrDefault(x => x.MinSalaryBase <= gross && x.MaxSalaryBase >= gross);
        if (table == null) return null;

        var cap = StatutoryCapHelper.GetClientCap(context, StatutoryCapType.PhilHealth);
        if (cap == null || table.EmployeeShare <= cap) return table;

        // See SSSHelper.GetTable's identical comment — downgrade to the highest bracket that
        // still respects the cap, keeping EE/ER as one self-consistent bracket.
        return context.Payload.PHICTableModel
            .Where(x => x.EmployeeShare <= cap)
            .OrderByDescending(x => x.EmployeeShare)
            .FirstOrDefault()
            ?? context.Payload.PHICTableModel
            .OrderBy(x => x.EmployeeShare)
            .FirstOrDefault();
    }
    public static DeductionPipeData ApplyTable(DeductionPayloadContext context, DeductionPipeData line, PHICTablePayload table, DateOnly applyToDate)
    {
        if (table == null || table.EE <= 0) return line;
        if (table.EE == 0) return line;
        line.PHIC = new PHICInfo
        {
            PayrollDate = applyToDate,
            EE = (table.EE == 0 || !DeductionValidator.CanApply(table.EE, line, context)) ? 0 : table.EE,
            ER = table.ER,
        };
        line.RunningTotal += table.EE;
        line.RemainingGrossBalance -= table.EE;
        return line;
    }

    public static (decimal EEBalance, decimal ERBalance) GetBalance(DeductionPayloadContext context, decimal ee, decimal er)
    {
        // Client capping is already fully applied in GetTable (by downgrading to a smaller,
        // self-consistent bracket) — ee/er here are already the capped values.
        var contributions = GetCurrentMonthContribution(context);
        var eebalance = Math.Max(ee - contributions.Sum(x => x.EmployeeShare), 0);
        var erbalance = Math.Max(er - contributions.Sum(x => x.EmployerShare), 0);
        return (eebalance, erbalance);
    }
    private static List<PHICContributionModel> GetCurrentMonthContribution(DeductionPayloadContext context)
    {
        context.Payload.PHICContribution.TryGetValue(new EmployeeKey(context.Employee.Id), out var accumulated);
        accumulated ??= new List<PHICContributionModel>();
        return accumulated;
    }
}
