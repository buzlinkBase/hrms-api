
namespace Hrms.Core.Calculators.Payloads;

public class DTRPayModel
{
    public Guid? DtrId { get; set; }
    public string? DTRRef { get; set; }
    public DateOnly Date { get; set; }
    public Guid EmployeeId { get; set; }
    // Copied from this specific day's own DailyRecord (set at DTR-generation time from that
    // day's attendance) -- NOT from the employee's current/master ClientId/PayrollGroupId
    // (PayrollSummaryLine.ClientId/PayrollGroupId already use the latter, unchanged). An
    // employee deployed to different clients on different days within one cutoff needs each
    // day's breakdown to carry that day's own client/department/payroll group for billing.
    public Guid? ClientId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? PayrollGroupId { get; set; }
    public decimal DailyRate { get; set; }
    public SalaryType SalaryType { get; set; }
    // Same enum PayrollContext.WorkType carries into this calculation — surfaced here purely
    // for display (e.g. TimeHourPayResultsModal's per-day breakdown), so a reviewer can see
    // which DTR policy classified this day without re-deriving it from the pay columns.
    public WorkType WorkType { get; set; }


    public decimal LateAmount { get; set; }
    public decimal UTAmount { get; set; }
    public decimal AbsentAmount { get; set; }
    public decimal PaidLeave { get; set; }
    public decimal UnpaidLeave { get; set; }

    public decimal RegularDayPay { get; set; }
    public decimal RegularOTPay { get; set; }
    public decimal RegularNDPay { get; set; }
    public decimal RegularNDOTPay { get; set; }

    public decimal RestDayPay { get; set; }
    public decimal RestDayOTPay { get; set; }
    public decimal RestDayNDPay { get; set; }
    public decimal RestDayNDOTPay { get; set; }

    public decimal LegalPay { get; set; }
    public decimal LegalOTPay { get; set; }
    public decimal LegalNDPay { get; set; }
    public decimal LegalNDOTPay { get; set; }

    public decimal SpecialPay { get; set; }
    public decimal SpecialOTPay { get; set; }
    public decimal SpecialNDPay { get; set; }
    public decimal SpecialNDOTPay { get; set; }

    public decimal RestLegalPay { get; set; }
    public decimal RestLegalOTPay { get; set; }
    public decimal RestLegalNDPay { get; set; }
    public decimal RestLegalNDOTPay { get; set; }

    public decimal RestSpecialPay { get; set; }
    public decimal RestSpecialOTPay { get; set; }
    public decimal RestSpecialNDPay { get; set; }
    public decimal RestSpecialNDOTPay { get; set; }

    public decimal DoubleLegalPay { get; set; }
    public decimal DoubleLegalOTPay { get; set; }
    public decimal DoubleLegalNDPay { get; set; }
    public decimal DoubleLegalNDOTPay { get; set; }

    public decimal RestDoubleLegalPay { get; set; }
    public decimal RestDoubleLegalOTPay { get; set; }
    public decimal RestDoubleLegalNDPay { get; set; }
    public decimal RestDoubleLegalNDOTPay { get; set; }

    public decimal LegalWorked { get; set; }
    public decimal LegalUnWorked { get; set; }
    public decimal RestLegalWorked { get; set; }
    public decimal RestLegalUnWorked { get; set; }
    public decimal DoubleLegalWorked { get; set; }
    public decimal DoubleLegalUnworked { get; set; }
    public decimal RestDoubleLegalWorked { get; set; }
    public decimal RestDoubleLegalUnworked { get; set; }
    public decimal TotalExcludingBasic { get; set; }
    public decimal Holiday { get; set; }
    public decimal NDPremiumPay { get; set; }
    public decimal OTPremiumPay { get; set; }

    public decimal TotalOT { get; set; }
    public decimal TotalND { get; set; }
    public decimal TotalNDOT { get; set; }

    // Per-category segregation of the already-blended {Category}OTPay/NDPay/NDOTPay above, each
    // figure priced against ONE rate alone (never compounded with another tier):
    // - OTBasePay: hours(OT) * rawOTRate * hourlyRate -- the raw HOLIDAY_OT/OVERTIME rate alone,
    //   ignoring day-type compounding and any client override on the actual paid OTPay.
    // - NDBasePay: hours(ND) * dayTypeRate * hourlyRate -- the day-type rate alone, no ND premium.
    // - NDOTBasePay: hours(NDOT) * rawOTRate * hourlyRate -- same raw OT rate as OTBasePay above,
    //   applied to the NDOT hours, ignoring the day-type rate AND the night-diff premium.
    // - NDPremiumPay / NDOTPremiumPay: hours * (nightDiffRate - 1) * hourlyRate -- e.g. a 1.10
    //   rate contributes only the .10, against the plain hourly rate, not any other tier.
    // These are additive recording for extraction/reporting, not a decomposition that sums back
    // to the blended totals above -- each figure is independently priced against its own single
    // rate, by design (matches the client's own spreadsheet, which shows the same four figures
    // this way). The existing blended fields above are unchanged by any of this.
    public decimal RegularNDBasePay { get; set; }
    public decimal RegularNDPremiumPay { get; set; }
    public decimal RegularNDOTBasePay { get; set; }
    public decimal RegularOTBasePay { get; set; }
    public decimal RegularNDOTPremiumPay { get; set; }

    public decimal RestDayNDBasePay { get; set; }
    public decimal RestDayNDPremiumPay { get; set; }
    public decimal RestDayNDOTBasePay { get; set; }
    public decimal RestDayOTBasePay { get; set; }
    public decimal RestDayNDOTPremiumPay { get; set; }

    public decimal LegalNDBasePay { get; set; }
    public decimal LegalNDPremiumPay { get; set; }
    public decimal LegalNDOTBasePay { get; set; }
    public decimal LegalOTBasePay { get; set; }
    public decimal LegalNDOTPremiumPay { get; set; }

    public decimal SpecialNDBasePay { get; set; }
    public decimal SpecialNDPremiumPay { get; set; }
    public decimal SpecialNDOTBasePay { get; set; }
    public decimal SpecialOTBasePay { get; set; }
    public decimal SpecialNDOTPremiumPay { get; set; }

    public decimal RestLegalNDBasePay { get; set; }
    public decimal RestLegalNDPremiumPay { get; set; }
    public decimal RestLegalNDOTBasePay { get; set; }
    public decimal RestLegalOTBasePay { get; set; }
    public decimal RestLegalNDOTPremiumPay { get; set; }

    public decimal RestSpecialNDBasePay { get; set; }
    public decimal RestSpecialNDPremiumPay { get; set; }
    public decimal RestSpecialNDOTBasePay { get; set; }
    public decimal RestSpecialOTBasePay { get; set; }
    public decimal RestSpecialNDOTPremiumPay { get; set; }

    public decimal DoubleLegalNDBasePay { get; set; }
    public decimal DoubleLegalNDPremiumPay { get; set; }
    public decimal DoubleLegalNDOTBasePay { get; set; }
    public decimal DoubleLegalOTBasePay { get; set; }
    public decimal DoubleLegalNDOTPremiumPay { get; set; }

    public decimal RestDoubleLegalNDBasePay { get; set; }
    public decimal RestDoubleLegalNDPremiumPay { get; set; }
    public decimal RestDoubleLegalNDOTBasePay { get; set; }
    public decimal RestDoubleLegalOTBasePay { get; set; }
    public decimal RestDoubleLegalNDOTPremiumPay { get; set; }
}
