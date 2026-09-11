namespace Hrms.Api.Documents.Bir;

// Field placements for wwwroot/Bir/2316Sep2021ENCS_Final.pdf -- confirmed via the template's
// own /MediaBox to be a single 612x936pt (8.5x13in) page.
//
// Calibrated against a real generated PDF compared to the actual form (round 1): "Year" was
// already well-placed. Everything else needed correction -- the real form has no box for
// EmployeeNo or CivilStatus (removed here; EmployeeNo has no BuildValues counterpart either,
// CivilStatus is still on Bir2316Model, just unprinted), Employee fields belong to Part I (rows
// 3-6, 25pt/row pitch, anchored on row 6 "Registered Address" = Y180), Employer fields belong
// to Part II "Employer Information (Present)" (rows 12-14, same pitch continued -- and Part II
// has no RDO Code box either, so Employer.RDOCode is also unprinted), and the aggregate
// compensation/tax figures belong to Part IVA "Summary" (rows 19-21+), not the itemized
// Part IV-B breakdown they were originally (wrongly) placed against. Part IVA's rows past 21
// and the signature block weren't visible in the calibration screenshot -- still estimates,
// re-verify against a fresh debug PDF.
public static class Bir2316FieldMap
{
    public static readonly IReadOnlyList<PdfField> Fields =
    [
        // ── Header ───────────────────────────────────────────────────────────────────
        new("Year", 0, 480, 150, 9, Bold: true), // row 1 "For the Year (YYYY)" -- confirmed well-placed

        // ── Part I — Employee Information (rows 3-6, anchored on row 6 = Y180) ──────
        new("Employee.TIN", 0, 150, 105, 9), // row 3 "TIN"
        new("Employee.FullName", 0, 150, 130, 9), // row 4 "Employee's Name"
        new("Employee.RDOCode", 0, 400, 155, 9), // row 5 "RDO Code"
        new("Employee.Address", 0, 150, 180, 9), // row 6 "Registered Address"

        // ── Part II — Employer Information (Present) (rows 12-14, same pitch) ───────
        new("Employer.TIN", 0, 150, 330, 9), // row 12 "TIN"
        new("Employer.RegisteredName", 0, 150, 355, 9), // row 13 "Employer's Name"
        new("Employer.Address", 0, 150, 380, 9), // row 14 "Registered Address"

        // ── Part IVA — Summary (rows 19-21+, same pitch) ────────────────────────────
        new("GrossCompensation", 0, 480, 505, 9), // row 19 "Gross Compensation Income from Present Employer"
        new("NonTaxableCompensation", 0, 480, 530, 9), // row 20 "Less: Total Non-Taxable/Exempt Compensation Income"
        new("TaxableCompensation", 0, 480, 555, 9, Bold: true), // row 21 "Taxable Compensation Income from Present Employer"
        // Rows past 21 (13th month pay, SSS/PhilHealth/Pag-IBIG, total tax withheld) weren't
        // visible in the calibration screenshot -- placed continuing the same 25pt/row pitch as
        // a starting estimate only.
        new("ThirteenthMonthPay", 0, 480, 580, 9),
        new("TotalSSS", 0, 480, 605, 9),
        new("TotalPhilHealth", 0, 480, 630, 9),
        new("TotalPagIbig", 0, 480, 655, 9),
        new("TotalTaxWithheld", 0, 480, 680, 9, Bold: true),

        // ── Signatories (not visible in the calibration screenshot -- unconfirmed) ──
        new("Employer.SignatoryName", 0, 150, 850, 9, Bold: true),
        new("Employer.SignatoryTitle", 0, 150, 865, 8),
        new("Employee.SignatureName", 0, 400, 850, 9, Bold: true),
    ];
}
