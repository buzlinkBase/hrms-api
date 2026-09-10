namespace Hrms.Domain.ValueObjects;

public class CreateLeave
{
    // Identification
    public string Code { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public string LegalBasis { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;

    // Pay & Source
    public PaySource PaySource { get; set; } = PaySource.Company;
    public bool EmployerAdvancesPayment { get; set; }

    // Accrual
    public AccrualBasis AccrualBasis { get; set; } = AccrualBasis.None;
    public double Credits { get; set; }
    public double AccrualRate { get; set; }
    public double? MaxAccrualBalance { get; set; }
    public bool ProRateFirstYear { get; set; }
    public LeaveReset LeaveReset { get; set; } = LeaveReset.PerPeriod;

    // Eligibility
    public int MinServiceMonths { get; set; }
    public GenderRestriction GenderRestriction { get; set; } = GenderRestriction.None;
    public bool RequiresApproval { get; set; } = true;
    public bool RequiresSupportingDocument { get; set; }

    // Application Rules
    public bool AllowHalfDay { get; set; } = true;
    public bool AllowPartial { get; set; }
    public bool AllowNegativeBalance { get; set; }
    public bool RequiresCredits { get; set; } = true;
    public double? MaxDaysPerYear { get; set; }
    public int? MaxConsecutiveDays { get; set; }

    // Carry-Over
    public CarryOverType CarryOverType { get; set; } = CarryOverType.Forfeit;
    public double CarryOverMaxDays { get; set; }
    public int? CarryOverExpiryMonths { get; set; }

    // Cash Conversion
    public bool ConvertToCash { get; set; }
    public decimal CashConversionRate { get; set; } = 1.0m;
    public double? MaxCashConversionDays { get; set; }

    // Statutory
    public bool IsStatutory { get; set; }
}

public class UpdateLeave : CreateLeave
{
    public Guid Id { get; set; }
}

public class LeaveModel : UpdateLeave
{
}
