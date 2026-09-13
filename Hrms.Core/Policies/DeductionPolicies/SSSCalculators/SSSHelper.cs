
namespace Hrms.Core.Policies.DeductionPolicies;

public record SSSTablePayload(decimal EE, decimal ER, decimal EC);
internal static class SSSHelper
{
    public static SSSModel? GetTable(DeductionPayloadContext context, decimal gross)
    {
        var table = context.Payload.SSSTableModel
            .FirstOrDefault(x => x.RangeFrom <= gross && x.RangeTo >= gross);

        return table;
    }
    public static DeductionPipeData ApplyTable(DeductionPayloadContext context, DeductionPipeData line, SSSTablePayload table, DateOnly applyToDate)
    {
        if (table == null || table.EE <= 0) return line;
        if (table.EE == 0) return line;
        line.SSS = new SSSInfo
        {
            PayrollDate = applyToDate,
            EE = (table.EE == 0 || !DeductionValidator.CanApply(table.EE, line, context)) ? 0 : table.EE,
            ER = table.ER,
            EC = table.EC,
        };
        line.RunningTotal += table.EE;
        line.RemainingGrossBalance -= table.EE;
        return line;
    }

    public static (decimal EEBalance, decimal ERBalance, decimal ECBalance) GetBalance(DeductionPayloadContext context, decimal ee, decimal er, decimal ec)
    {
        var contributions = GetCurrentMonthContribution(context);
        var eeTarget = StatutoryCapHelper.ApplyClientCap(context, StatutoryCapType.SSS, ee);
        var eebalance = Math.Max(eeTarget - contributions.Sum(x => x.EE), 0);
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
