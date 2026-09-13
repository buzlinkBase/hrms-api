
namespace Hrms.Core.Policies.DeductionPolicies;

public record HDMFTablePayload(decimal EE, decimal ER);
internal static class HDMFHelper
{
    public static HDMFModel? GetTable(DeductionPayloadContext context, decimal gross)
    {
        var table = context.Payload.HDMFTableModel
            .FirstOrDefault(x => x.MinSalaryBase <= gross && x.MaxSalaryBase >= gross);

        return table;
    }
    public static DeductionPipeData ApplyTable(DeductionPayloadContext context, DeductionPipeData line, HDMFTablePayload table, DateOnly applyToDate)
    {
        if (table == null || table.EE <= 0) return line;
        if (table.EE == 0) return line;
        line.HDMF = new HDMFInfo
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
        var eeTarget = StatutoryCapHelper.ApplyClientCap(context, StatutoryCapType.PagIbig, ee);
        var eebalance = Math.Max(eeTarget - contributions.Sum(x => x.EmployeeShare), 0);
        var erbalance = Math.Max(er - contributions.Sum(x => x.EmployerShare), 0);
        return (eebalance, erbalance);
    }
    private static List<HDMFContributionModel> GetCurrentMonthContribution(DeductionPayloadContext context)
    {
        context.Payload.HDMFContribution.TryGetValue(new EmployeeKey(context.Employee.Id), out var accumulated);
        accumulated ??= new List<HDMFContributionModel>();
        return accumulated;
    }
}
