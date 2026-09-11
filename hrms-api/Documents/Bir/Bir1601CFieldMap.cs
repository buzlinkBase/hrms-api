namespace Hrms.Api.Documents.Bir;

// Field placements for wwwroot/Bir/1601CfinalJan2018.pdf -- confirmed via the template's own
// /MediaBox to be two 612x936pt (8.5x13in) pages. The official form's Lines 15-19 (the only
// figures this app computes) all live on page 1 (index 0); page 2 is left untouched.
//
// Calibrated against a real generated PDF compared to the actual form (round 1): the header
// row pitch is 15pt/row (row6 "Taxpayer Identification Number (TIN)" = Y150, row8 "Withholding
// Agent's Name" = Y180, both confirmed well-placed), and Part II "Computation of Tax" row pitch
// is 16pt/row (Y = 96 + 16*row, derived from Line15's landing 5 rows low at the original
// spacing). Still an estimate below row 25 and for the signature block -- re-verify against a
// fresh debug PDF.
public static class Bir1601CFieldMap
{
    public static readonly IReadOnlyList<PdfField> Fields =
    [
        // ── Header (rows 1-2) ────────────────────────────────────────────────────────
        new("Period", 0, 280, 75, 9, Bold: true), // row 1 "For the Month (MM/YYYY)"
        new("AmendedReturn", 0, 450, 90, 9), // row 2 "Amended Return?" Yes/No

        // ── Withholding agent (rows 6, 8) ────────────────────────────────────────────
        new("Employer.TIN", 0, 150, 150, 9), // row 6 "Taxpayer Identification Number (TIN)"
        new("Employer.RDOCode", 0, 470, 150, 9), // row 6, "/RDO Code" to the right of TIN
        new("Employer.RegisteredName", 0, 150, 180, 9), // row 8 "Withholding Agent's Name"

        // ── Return figures (Lines 15-19), Part II "Computation of Tax" ──────────────
        new("Line15_TotalCompensation", 0, 480, 320, 9), // row 14 "Total Amount of Compensation"
        new("Line16A_StatutoryMinimumWage", 0, 480, 336, 9), // row 15
        new("Line16B_MWEPremiumPay", 0, 480, 352, 9), // row 16
        new("Line16C_OtherNonTaxable", 0, 480, 416, 9), // row 20 "Other Non-Taxable Compensation"
        new("Line17_TotalNonTaxable", 0, 480, 432, 9, Bold: true), // row 21 "Total Non-Taxable Compensation"
        new("Line18_TaxableCompensation", 0, 480, 448, 9, Bold: true), // row 22 "Total Taxable Compensation"
        new("Line19_TaxWithheld", 0, 480, 496, 10, Bold: true), // row 25 "Total Taxes Withheld"

        // ── Signatory (not visible in the calibration screenshot -- unconfirmed) ────
        new("Employer.SignatoryName", 0, 150, 850, 9, Bold: true),
        new("Employer.SignatoryTitle", 0, 150, 865, 8),
    ];
}
