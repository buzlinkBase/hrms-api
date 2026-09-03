namespace Hrms.Core.Policies.DeductionPolicies.WtaxCalculators
{
    // Independent per-period withholding tax — BIR's Revised Withholding Tax Table (RR
    // 11-2018) taxes each pay period on its own compensation against that period's own
    // frequency-specific bracket table (WTaxHelper.GetTable filters by
    // Employee.PayrollFrequency). No monthly aggregation, no splitting across cutoffs, no
    // cross-period true-up — unlike SSS/PHIC/HDMF's monthly-ceiling contributions, tax
    // doesn't accumulate a running balance to settle later in the month. One calculator now
    // serves every frequency (Daily/Weekly/Semi-Monthly/Monthly), since frequency only ever
    // affected which bracket table applied, not the computation shape.
    public class TableWTaxCalculator : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            if (line.IsLimit) return line;

            var taxableIncome = context.PayrollLine.GrossIncome
                         - (line.SSS?.EE ?? 0)
                         - (line.PHIC?.EE ?? 0)
                         - (line.HDMF?.EE ?? 0);

            var table = WTaxHelper.GetTable(context, taxableIncome);
            if (table == null) return line;

            var due = WTaxHelper.GetCalculatedDue(table, taxableIncome);
            if (due <= 0) return line;

            var payload = new WTaxTablePayload(taxableIncome, due);
            return WTaxHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }
}
