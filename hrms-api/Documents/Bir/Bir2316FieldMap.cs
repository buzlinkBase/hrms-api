namespace Hrms.Api.Documents.Bir;

// Field placements for wwwroot/Bir/2316Sep2021ENCS_Final.pdf -- confirmed via the template's
// own /MediaBox to be a single 612x936pt (8.5x13in) page.
//
// The form has TWO independent side-by-side columns sharing the page's vertical space: the
// LEFT column (Part I Employee, Part II/III Employer, Part IVA Summary -- rows 1-28, ~X150-460,
// 25pt/row pitch, anchored on row 6 "Registered Address" = Y180) and the RIGHT column (Part
// IV-B Details of Compensation Income -- rows 29-52, ~X480-700, ~25pt/row pitch, anchored on
// row 29 "Basic Salary" = Y105, derived from where "Year" -- unrelated to this column, just
// pinned near the top -- and the round-2 aggregate totals actually landed).
//
// Round 2 finding: the aggregate totals (Gross/NonTaxable/Taxable/TaxWithheld Compensation)
// were landing in the RIGHT column (X480 = inside Part IV-B's box area) when they belong in the
// LEFT column's Part IVA summary -- a wrong X, not just a wrong Y. Also found: 13th Month Pay
// and the SSS/GSIS/PHIC/HDMF contributions (combined into ONE box on the real form, row 36 --
// not 3 separate boxes) DO have dedicated rows on Part IV-B section A, so they're mapped there
// now instead of being dropped. EmployeeNo/CivilStatus/Employer.RDOCode still have no matching
// box on this form revision (dropped, per round 1). Year's own exact position and the
// Employee/Employer Part I/II/III rows are still not fully confirmed -- re-verify.
public static class Bir2316FieldMap
{
    public static readonly IReadOnlyList<PdfField> Fields =
    [
        // ── Header ───────────────────────────────────────────────────────────────────
        new("Year", 0, 160, 75, 9, Bold: true), // row 1 "For the Year (YYYY)"

        // ── Part I — Employee Information (rows 3-6, anchored on row 6 = Y180) ──────
        new("Employee.TIN", 0, 150, 105, 9), // row 3 "TIN"
        new("Employee.FullName", 0, 150, 130, 9), // row 4 "Employee's Name"
        new("Employee.RDOCode", 0, 400, 155, 9), // row 5 "RDO Code"
        new("Employee.Address", 0, 150, 180, 9), // row 6 "Registered Address" -- confirmed correct

        // ── Part II — Employer Information (Present) (rows 12-14, same pitch) ───────
        new("Employer.TIN", 0, 150, 330, 9), // row 12 "TIN"
        new("Employer.RegisteredName", 0, 150, 355, 9), // row 13 "Employer's Name"
        new("Employer.Address", 0, 150, 380, 9), // row 14 "Registered Address"

        // ── Part IVA — Summary, LEFT column (rows 19-28, same pitch as Part I/II) ───
        new("GrossCompensation", 0, 400, 505, 9), // row 19 "Gross Compensation Income from Present Employer"
        new("NonTaxableCompensation", 0, 400, 530, 9), // row 20 "Less: Total Non-Taxable/Exempt Compensation Income"
        new("TaxableCompensation", 0, 400, 555, 9, Bold: true), // row 21 "Taxable Compensation Income from Present Employer"
        new("TotalTaxWithheld", 0, 400, 730, 9, Bold: true), // row 28 "Total Taxes Withheld (Sum of Items 26 and 27)"

        // ── Part IV-B section A, RIGHT column (rows 29-38, anchored on row 29 = Y105) ─
        new("ThirteenthMonthPay", 0, 480, 230, 9), // row 34 "13th Month Pay and Other Benefits"
        new("GovtContributions", 0, 480, 280, 9), // row 36 "SSS, GSIS, PHIC, HDMF Mandatory Contributions..." (SSS+PhilHealth+Pag-IBIG combined -- one box, not three)

        // ── Signatories (not visible in the calibration screenshot -- unconfirmed) ──
        new("Employer.SignatoryName", 0, 150, 850, 9, Bold: true),
        new("Employer.SignatoryTitle", 0, 150, 865, 8),
        new("Employee.SignatureName", 0, 400, 850, 9, Bold: true),
    ];
}
