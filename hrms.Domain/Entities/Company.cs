namespace Hrms.Domain.Entities;

public class Company : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int TotalWorkingDays { get; set; }
    public int TakehomePercentage { get; set; }
    public bool ApplyStatutoryOnActualMonth { get; set; } = true;
    public string TIN { get; set; } = string.Empty;

    // ── Government agency registration (BIR/SSS/PhilHealth/Pag-IBIG report headers) ─────
    public string RDOCode { get; set; } = string.Empty;
    public string SSSNumber { get; set; } = string.Empty;
    public string PhilHealthNumber { get; set; } = string.Empty;
    public string PagIbigNumber { get; set; } = string.Empty;
    public string AuthorizedSignatoryName { get; set; } = string.Empty;
    public string AuthorizedSignatoryTitle { get; set; } = string.Empty;

    // ── Classification ──────────────────────────────────────────────────────────────────
    // Stable codes, not display text. Tenant Service owns the industry lookup and keeps the
    // console's copy; this is HRIS's copy for the Company Info screen, synced on save.
    public string IndustryCode { get; set; } = string.Empty;
    public string SubIndustryCode { get; set; } = string.Empty;
    /// <summary>sole-proprietorship, partnership, corporation, cooperative, non-profit or other.</summary>
    public string BusinessType { get; set; } = string.Empty;
    /// <summary>Employee-count band: 1-10, 11-50, 51-200, 201-500, 501-1000 or 1000+.</summary>
    public string CompanySize { get; set; } = string.Empty;

    // ── Names and online presence ───────────────────────────────────────────────────────
    /// <summary>The legal / registered business name, when it differs from the display name in <see cref="Description"/>.</summary>
    public string RegisteredName { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;

    // ── Address parts ───────────────────────────────────────────────────────────────────
    // <see cref="Address"/> stays the street / address line (older companies keep their whole
    // address there); these add the rest. CompanyAddress.Compose joins them for display elsewhere.
    public string Barangay { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = CompanyDefaults.Country;

    // ── Contacts ────────────────────────────────────────────────────────────────────────
    public string SecondaryPhone { get; set; } = string.Empty;
    public string HrContactName { get; set; } = string.Empty;
    public string HrContactEmail { get; set; } = string.Empty;
    public string HrContactPhone { get; set; } = string.Empty;
    public string PayrollContactName { get; set; } = string.Empty;
    public string PayrollContactEmail { get; set; } = string.Empty;
    public string PayrollContactPhone { get; set; } = string.Empty;

    // ── BIR and registration ────────────────────────────────────────────────────────────
    /// <summary>private, government or household. Empty = not set (treated as private).</summary>
    public string EmployerType { get; set; } = string.Empty;
    public bool IsWithholdingAgent { get; set; }
    /// <summary>BIR Alphanumeric Tax Code of the company's withholding on compensation.</summary>
    public string AtcCode { get; set; } = string.Empty;
    public string TinBranchCode { get; set; } = string.Empty;
    public string SssBranchCode { get; set; } = string.Empty;
    public string PhilHealthBranchCode { get; set; } = string.Empty;
    public string PagIbigBranchCode { get; set; } = string.Empty;
    public string SecDtiNumber { get; set; } = string.Empty;
    public DateTime? RegistrationDate { get; set; }
    public string BusinessPermitNo { get; set; } = string.Empty;
    /// <summary>Stored for the owner's reference; no reminder is sent yet.</summary>
    public DateTime? BusinessPermitExpiry { get; set; }

    /// <summary>Print the company logo at the top of every payslip (when a logo is uploaded). Defaults to on.</summary>
    public bool ShowLetterhead { get; set; } = true;

    /// <summary>Printed above the page number on every payslip (e.g. "System-generated; no signature required").</summary>
    public string PayslipFooter { get; set; } = string.Empty;

    // ── Employee ID numbering ───────────────────────────────────────────────────────────
    /// <summary>"Off" (default): the employee number is free text. "Auto": new employees get the next number.</summary>
    public string EmployeeIdMode { get; set; } = EmployeeIdSettings.Off;
    public string EmployeeIdPrefix { get; set; } = string.Empty;
    public int EmployeeIdPadding { get; set; } = EmployeeIdSettings.DefaultPadding;
    public bool EmployeeIdIncludeYear { get; set; }
    /// <summary>The next number to hand out. Server-owned: only EmployeeNumberAllocator moves it, never a Company Info save.</summary>
    public int NextEmployeeNo { get; set; } = 1;

    // ── Regional defaults (Philippines) ─────────────────────────────────────────────────
    // Stored for the company; nothing reads the time zone yet -- attendance still runs on its
    // own settings. Rows from before these existed hold "" until saved: readers fall back to
    // CompanyDefaults.
    public string TimeZone { get; set; } = CompanyDefaults.TimeZone;
    public string Currency { get; set; } = CompanyDefaults.Currency;
    /// <summary>Comma-separated weekday names, e.g. "Sunday" or "Saturday,Sunday".</summary>
    public string WorkWeekRestDays { get; set; } = CompanyDefaults.RestDays;

    /// <summary>
    /// Where the company logo is stored (an IFileStorage key), or null for none. Changed only by
    /// OrganizationImageService -- never through the Company Info save, so that can't wipe it.
    /// </summary>
    public string? LogoKey { get; set; }
}

