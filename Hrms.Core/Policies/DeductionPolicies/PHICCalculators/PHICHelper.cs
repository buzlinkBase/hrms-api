
namespace Hrms.Core.Policies.DeductionPolicies;

public record PHICTablePayload(decimal EE, decimal ER);
internal static class PHICHelper
{
    public static PHICModel? GetTable(DeductionPayloadContext context, decimal gross)
    {
        var table = context.Payload.PHICTableModel
            .FirstOrDefault(x => x.MinSalaryBase <= gross && x.MaxSalaryBase >= gross);

        return table;
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
