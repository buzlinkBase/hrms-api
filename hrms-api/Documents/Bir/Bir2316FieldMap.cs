namespace Hrms.Api.Documents.Bir;

// Field placements for wwwroot/Bir/2316Sep2021ENCS_Final.pdf -- confirmed via the template's
// own /MediaBox to be a single 612x936pt (8.5x13in) page.
//
// IMPORTANT: these X/Y coordinates are placeholders, not calibrated against the real form --
// this environment has no PDF-to-image rendering available to verify them visually. They're
// laid out as a simple, readable two-column list purely so the mechanism can be exercised
// end-to-end; before relying on this for real filing/employee handout, generate a PDF with
// ?debug=true (Development only -- see PayrollReportsController), compare it against the blank
// official form, and adjust every X/Y below to match.
public static class Bir2316FieldMap
{
    public static readonly IReadOnlyList<PdfField> Fields =
    [
        // ── Header ───────────────────────────────────────────────────────────────────
        new("Year", 0, 480, 150, 9, Bold: true),

        // ── Employee ─────────────────────────────────────────────────────────────────
        new("Employee.FullName", 0, 150, 180, 9),
        new("Employee.EmployeeNo", 0, 400, 180, 9),
        new("Employee.TIN", 0, 150, 205, 9),
        new("Employee.RDOCode", 0, 400, 205, 9),
        new("Employee.CivilStatus", 0, 150, 230, 9),
        new("Employee.Address", 0, 150, 255, 9),

        // ── Employer ─────────────────────────────────────────────────────────────────
        new("Employer.RegisteredName", 0, 150, 300, 9),
        new("Employer.TIN", 0, 400, 300, 9),
        new("Employer.RDOCode", 0, 150, 325, 9),
        new("Employer.Address", 0, 150, 350, 9),

        // ── Compensation and tax withheld ────────────────────────────────────────────
        new("GrossCompensation", 0, 400, 400, 9),
        new("NonTaxableCompensation", 0, 400, 425, 9),
        new("ThirteenthMonthPay", 0, 400, 450, 9),
        new("TaxableCompensation", 0, 400, 475, 9, Bold: true),
        new("TotalSSS", 0, 400, 510, 9),
        new("TotalPhilHealth", 0, 400, 535, 9),
        new("TotalPagIbig", 0, 400, 560, 9),
        new("TotalTaxWithheld", 0, 400, 595, 9, Bold: true),

        // ── Signatories ──────────────────────────────────────────────────────────────
        new("Employer.SignatoryName", 0, 150, 850, 9, Bold: true),
        new("Employer.SignatoryTitle", 0, 150, 865, 8),
        new("Employee.SignatureName", 0, 400, 850, 9, Bold: true),
    ];
}
