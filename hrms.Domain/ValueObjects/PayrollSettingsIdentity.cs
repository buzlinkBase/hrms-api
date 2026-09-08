
namespace Hrms.Domain.ValueObjects;

public static class PayrollSettingsIdentity
{
    public const string IdentityType = "PayrollSettings";
    public const string KeyFiscalYearStartMonth = "FiscalYearStartMonth";
    public const string KeyThirteenthMonthExemptionCeiling = "ThirteenthMonthExemptionCeiling";
    // Multiplier against an employee's CurrentAverageMonthlyNetPay — a Year-End Tax
    // Annualization collection larger than (average monthly net pay * this) is flagged with a
    // warning in Preview (informational only, never blocks Generate). Default 1.0 — see
    // TaxAnnualizationService.ComputeAsync.
    public const string KeyLargeTaxCollectionWarningMultiplier = "LargeTaxCollectionWarningMultiplier";
}
