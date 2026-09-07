using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

// Persists one SSS/PHIC/HDMF/WTax ledger row per employee for a completed payroll run, so the
// next cutoff's balance-netting (SSSHelper/PHICHelper/HDMFHelper.GetBalance) can see what's
// already been withheld this month instead of always treating the target as untouched. Shared
// by every payroll-generating flow (regular, 13th Month, Last Pay) — extracted so that mapping
// logic exists in exactly one place instead of being copy-pasted into each.
public class StatutoryContributionLedgerService
{
    private readonly SSSContributionService _sssContributionService;
    private readonly PHICContributionService _phicContributionService;
    private readonly HDMFContributionService _hdmfContributionService;
    private readonly TaxContributionService _taxContributionService;

    public StatutoryContributionLedgerService(
        SSSContributionService sssContributionService,
        PHICContributionService phicContributionService,
        HDMFContributionService hdmfContributionService,
        TaxContributionService taxContributionService)
    {
        _sssContributionService = sssContributionService;
        _phicContributionService = phicContributionService;
        _hdmfContributionService = hdmfContributionService;
        _taxContributionService = taxContributionService;
    }

    public async Task SaveAsync(List<PayrollSummaryLine> lines, CancellationToken token)
    {
        var sssRows = lines
            .Where(l => l.SSSContribution > 0 || l.EmployerSSSContribution > 0)
            .Select(l => new SSSContribution
            {
                PayrollBatchId = l.PayrollBatchId,
                EmployeeId = l.EmployeeId,
                PayrollFrom = l.PayPeriodStart,
                PayrollTo = l.PayPeriodEnd,
                PayrollDate = l.StatutoryCreditDate,
                EE = l.SSSContribution,
                ER = l.EmployerSSSContribution - l.EmployerECContribution,
                EC = l.EmployerECContribution,
                TotalContibution = l.SSSContribution + l.EmployerSSSContribution,
            })
            .ToList();

        var phicRows = lines
            .Where(l => l.PhilHealthContribution > 0 || l.EmployerPhilHealthContribution > 0)
            .Select(l => new PHICContribution
            {
                PayrollBatchId = l.PayrollBatchId,
                EmployeeId = l.EmployeeId,
                PayrollFrom = l.PayPeriodStart,
                PayrollTo = l.PayPeriodEnd,
                PayrollDate = l.StatutoryCreditDate,
                EmployeeShare = l.PhilHealthContribution,
                EmployerShare = l.EmployerPhilHealthContribution,
                TotalContribution = l.PhilHealthContribution + l.EmployerPhilHealthContribution,
            })
            .ToList();

        var hdmfRows = lines
            .Where(l => l.PagIbigContribution > 0 || l.EmployerPagIbigContribution > 0)
            .Select(l => new HDMFContribution
            {
                PayrollBatchId = l.PayrollBatchId,
                EmployeeId = l.EmployeeId,
                PayrollFrom = l.PayPeriodStart,
                PayrollTo = l.PayPeriodEnd,
                PayrollDate = l.StatutoryCreditDate,
                EmployeeShare = l.PagIbigContribution,
                EmployerShare = l.EmployerPagIbigContribution,
                TotalContribution = l.PagIbigContribution + l.EmployerPagIbigContribution,
            })
            .ToList();

        var taxRows = lines
            .Where(l => l.WithholdingTax > 0)
            .Select(l => new WTaxContribution
            {
                PayrollBatchId = l.PayrollBatchId,
                EmployeeId = l.EmployeeId,
                PayrollFrom = l.PayPeriodStart,
                PayrollTo = l.PayPeriodEnd,
                PayrollDate = l.PostingPeriod,
                Date = l.PayPeriodEnd,
                Amount = l.WithholdingTax,
            })
            .ToList();

        if (sssRows.Count > 0) await _sssContributionService.AddRangeAsync(sssRows, token);
        if (phicRows.Count > 0) await _phicContributionService.AddRangeAsync(phicRows, token);
        if (hdmfRows.Count > 0) await _hdmfContributionService.AddRangeAsync(hdmfRows, token);
        if (taxRows.Count > 0) await _taxContributionService.AddRangeAsync(taxRows, token);
    }
}
