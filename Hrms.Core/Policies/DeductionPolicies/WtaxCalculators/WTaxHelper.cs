
namespace Hrms.Core.Policies.DeductionPolicies;

public record WTaxTablePayload(decimal TaxableIncome, decimal TaxDue);
internal static class WTaxHelper
{
    // Filtered by the employee's own PayrollFrequency first — BIR's Revised Withholding Tax
    // Table has a distinct bracket table per frequency (Daily/Weekly/Semi-Monthly/Monthly),
    // and their peso ranges overlap extensively, so matching RangeFrom/RangeTo alone across
    // every frequency's rows combined can silently resolve to the wrong frequency's table.
    public static WTaxModel? GetTable(DeductionPayloadContext context, decimal gross)
    {
        var payrollType = context.Employee.PayrollFrequency.ToString();
        return context.Payload.TaxTableModel
            .Where(x => x.PayrollType == payrollType)
            .FirstOrDefault(x => x.RangeFrom <= gross && x.RangeTo >= gross);
    }
    public static DeductionPipeData ApplyTable(DeductionPayloadContext context, DeductionPipeData line, WTaxTablePayload table, DateOnly applyToDate)
    {
        if (table == null || table.TaxDue <= 0) return line;
        line.TaxInfo = new WTaxInfo
        {
            PayrollDate = applyToDate,
            TaxableIncome = table.TaxableIncome,
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
}
