
namespace Hrms.Core.Policies.DeductionPolicies;

public record WTaxTablePayload(decimal TaxDue);
internal static class WTaxHelper
{
    public static WTaxModel? GetTable(DeductionPayloadContext context, decimal gross)
    {
        var table = context.Payload.TaxTableModel
            .FirstOrDefault(x => x.RangeFrom <= gross && x.RangeTo >= gross);
        return table;
    }
    public static DeductionPipeData ApplyTable(DeductionPayloadContext context, DeductionPipeData line, WTaxTablePayload table, DateOnly applyToDate)
    {
        if (table == null || table.TaxDue <= 0) return line;
        if (table.TaxDue == 0) return line;
        line.TaxInfo = new WTaxInfo
        {
            PayrollDate = applyToDate,
            TaxableIncome = table.TaxDue,
            TaxDue = table.TaxDue
        };
        line.RunningTotal += table.TaxDue;
        line.RemainingGrossBalance -= table.TaxDue;
        return line;
    }

    public static decimal GetCalculatedDue(WTaxModel table, decimal gross)
    {
        if (table == null) return 0;
        return table.BaseTaxDue + (Math.Max(0, gross - table.RangeFrom) * table.AddOnPercentage);
    }

    public static decimal GetBalance(DeductionPayloadContext context, decimal ee)
    {
        var contributions = GetCurrentMonthContribution(context);
        return Math.Max(ee - contributions.Sum(x => x.TaxDue), 0);
    }
    private static List<WTaxContributionModel> GetCurrentMonthContribution(DeductionPayloadContext context)
    {
        context.Payload.TaxContribution.TryGetValue(new EmployeeKey(context.Employee.Id), out var accumulated);
        accumulated ??= new List<WTaxContributionModel>();
        return accumulated;
    }
}
