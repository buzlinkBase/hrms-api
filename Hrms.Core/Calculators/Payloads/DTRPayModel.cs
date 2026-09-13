
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

}
