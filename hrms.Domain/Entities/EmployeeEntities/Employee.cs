namespace Hrms.Domain.Entities.EmployeeEntities;

public class Employee : BaseEntity
{
    public Guid? UserId { get; set; }
    public int? BioId { get; set; }
    public string? Email { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public Guid PayrollGroupId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? AreaId { get; set; }
    public Guid? BranchId { get; set; }

    public Guid? SectionId { get; set; }
    public Guid? PositionId { get; set; }
    public JobLevelOption JobLevel { get; set; }
    public Guid? TimeShiftId { get; set; }

    public DateTime DateRegistered { get; set; } = DateTime.UtcNow;
    public DateOnly HireDate { get; set; }
    public DateTime? ContractStart { get; set; }
    public DateTime? ContractEnd { get; set; }

    public string CivilStatus { get; set; } = string.Empty;
    public DateTime? DateResigned { get; set; }
    public string HiringEntity { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string Suffix { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public int? Age { get; set; }

    public decimal MonthlyRate { get; set; }
    public decimal DailyRate { get; set; }
    public decimal Cola { get; set; } //PerPayroll

    // FIXED salary only: whether DailyRate is entered manually or derived as
    // (MonthlyRate * 12) / FactorDays. See DailyRateResolver.
    public DailyRateMode DailyRateMode { get; set; } = DailyRateMode.Manual;
    public decimal? FactorDays { get; set; }
    // MonthlyTotalDays mode only: when true, ignores FactorDays and divides by the actual
    // number of days in each payroll month (28/29/30/31) instead of a fixed denominator.
    public bool UseActualMonthDays { get; set; }

    public bool IsRestDayPaid { get; set; }
    public bool IsRegularHolidayIncluded { get; set; }
    public bool IsSpecialNonWorkingIncluded { get; set; }
    public bool IsNightDiffIncluded { get; set; }

    // When true, use this employee's own IsXxxIncluded toggles above for Fixed-salary
    // payroll. When false, fall back to the tenant-wide PayrollInclusionDefaults.
    // See EmployeePayrollInclusionResolver.
    public bool UseEmployeeOverride { get; set; } = true;

    public DateTime? DOB { get; set; }
    public string BloodType { get; set; } = string.Empty;

    public PaymentMethod ModeOfPayment { get; set; } = PaymentMethod.ATM;
    public SalaryType SalaryType { get; set; } = SalaryType.VARIABLE;
    //public PayrollFrequency PayrollFrequency { get; set; } = PayrollFrequency.SEMI_MONTHLY;
    public EmploymentStatus EmploymentStatus { get; set; } = EmploymentStatus.Probationary;

    public string BankName { get; set; } = string.Empty;
    public string BankNo { get; set; } = string.Empty;

    public virtual SSSRate? SSSRate { get; set; }
    public virtual PHICRate? PHICRate { get; set; }
    public virtual HDMFRate? HDMFRate { get; set; }
    public virtual TaxRate? TaxRate { get; set; }

    public string SSSNo { get; set; } = string.Empty;
    public string PHICNo { get; set; } = string.Empty;
    public string HDMFNo { get; set; } = string.Empty;
    public string TIN { get; set; } = string.Empty;

    public string Contact { get; set; } = string.Empty;
    public string Address1 { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;

    public virtual ICollection<Skill> Skills { get; set; }
    public virtual ICollection<Education> Educations { get; set; }
    public virtual ICollection<Dependent> Dependents { get; set; }
    public virtual ICollection<EmployeeRecord> EmployeeRecords { get; set; }
    public virtual ICollection<EmploymentHistory> Employments { get; set; }
    public virtual ICollection<AssignAsset> Assets { get; set; }
    public string ProfileImg { get; set; } = string.Empty;
    public virtual PayrollGroup? PayrollGroup { get; set; }
    public virtual EmployeeSetting? Settings { get; set; }
    public virtual Client? Client { get; set; }
    public virtual Branch? Branch { get; set; }
    public virtual Department? Department { get; set; }
    public virtual Department? HeadedDepartment { get; set; }
    public virtual Position? Position { get; set; }
    public virtual CostCenters? Area { get; set; }
    public virtual ICollection<RestDay> RestDays { get; set; }
    public virtual ICollection<EmployeeFixedSchedule> FixedSchedule { get; set; }
    public virtual TimeShift? TimeShift { get; set; }
    public Employee()
    {
        RestDays = new List<RestDay>();
        FixedSchedule = new List<EmployeeFixedSchedule>();
    }
}
public class SSSRate : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; }
    public ComputationBasis ComputationType { get; set; }
    //public StatutoryDeductionSchedule Frequency { get; set; }//for semi monthly
    public decimal EE { get; set; }
    public decimal ER { get; set; }
    public decimal EC { get; set; }
    public decimal AddOns { get; set; }

}
public class PHICRate : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; }
    public ComputationBasis ComputationType { get; set; }
    //public StatutoryDeductionSchedule Frequency { get; set; } //for semi monthly
    public decimal EE { get; set; }
    public decimal ER { get; set; }
    public decimal AddOns { get; set; }
}
public class HDMFRate : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; }
    public ComputationBasis ComputationType { get; set; }
    //public StatutoryDeductionSchedule Frequency { get; set; } //for semi monthly
    public decimal EE { get; set; }
    public decimal ER { get; set; }
    public decimal AddOns { get; set; }
}
public class TaxRate : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; }
    public ComputationBasis ComputationType { get; set; }
    //public StatutoryDeductionSchedule Frequency { get; set; }
    public decimal EE { get; set; }
    public decimal AddOns { get; set; }
}


public class EmployeeSetting : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; }
    public bool IsEligibleForOvertime { get; set; }
    public bool IsEligibleForHolidayPay { get; set; }
    public bool IsEligibleForNightDifferential { get; set; }
    public bool IsEligibleForLeaveCredits { get; set; }
    public bool IsEligibleFor13thMonth { get; set; }
    public bool IsNoDTRNotRequired { get; set; } = false;

    //public bool IsEligibleForHazardPay { get; set; }
    //public bool IsEligibleForHealthInsurance { get; set; }
    //public bool IsEligibleForRetirementBenefits { get; set; }
    //public bool IsEligibleForPerformanceBonus { get; set; }
    //public bool IsEligibleForMealAllowance { get; set; }
    //public bool IsEligibleForTransportationAllowance { get; set; }
}