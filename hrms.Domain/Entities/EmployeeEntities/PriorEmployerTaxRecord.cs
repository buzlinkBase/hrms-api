using BuzlinkRepository;

namespace Hrms.Domain.Entities.EmployeeEntities;

// A newly-hired employee's previous employer's year-to-date compensation/tax figures (from the
// BIR Form 2316 they submit at hire) — one record per employee per calendar year, consolidated
// into that year's Year-End Tax Annualization alongside this employer's own posted payroll. See
// TaxAnnualizationService.ComputeAsync.
[DisableSoftDelete]
public class PriorEmployerTaxRecord : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;
    public int Year { get; set; }
    public bool HasPriorEmployer { get; set; }
    public string? PriorEmployerName { get; set; }
    public decimal GrossIncomeYtd { get; set; }
    public decimal NonTaxableYtd { get; set; }
    public decimal StatutoryDeductionsYtd { get; set; }
    public decimal TaxWithheldYtd { get; set; }
}
