namespace Hrms.Api.Documents.Bir;

// Field placements for wwwroot/Bir/1601CfinalJan2018.pdf -- confirmed via the template's own
// /MediaBox to be two 612x936pt (8.5x13in) pages. The official form's Lines 15-19 (the only
// figures this app computes) all live on page 1 (index 0); page 2 is left untouched.
//
// IMPORTANT: same caveat as Bir2316FieldMap -- these coordinates are uncalibrated placeholders.
// Use ?debug=true (Development only) to line them up against the real form.
public static class Bir1601CFieldMap
{
    public static readonly IReadOnlyList<PdfField> Fields =
    [
        // ── Header / withholding agent ───────────────────────────────────────────────
        new("Period", 0, 400, 150, 9, Bold: true),
        new("Employer.RegisteredName", 0, 150, 180, 9),
        new("Employer.TIN", 0, 400, 180, 9),
        new("Employer.RDOCode", 0, 150, 205, 9),
        new("AmendedReturn", 0, 400, 205, 9),

        // ── Return figures (Lines 15-19) ─────────────────────────────────────────────
        new("Line15_TotalCompensation", 0, 480, 400, 9),
        new("Line16A_StatutoryMinimumWage", 0, 480, 425, 9),
        new("Line16B_MWEPremiumPay", 0, 480, 450, 9),
        new("Line16C_OtherNonTaxable", 0, 480, 475, 9),
        new("Line17_TotalNonTaxable", 0, 480, 500, 9, Bold: true),
        new("Line18_TaxableCompensation", 0, 480, 525, 9, Bold: true),
        new("Line19_TaxWithheld", 0, 480, 560, 10, Bold: true),

        // ── Signatory ────────────────────────────────────────────────────────────────
        new("Employer.SignatoryName", 0, 150, 850, 9, Bold: true),
        new("Employer.SignatoryTitle", 0, 150, 865, 8),
    ];
}
