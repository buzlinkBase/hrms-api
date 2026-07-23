using Hrms.Domain.Entities.EmployeeEntities;
using MessagePack;

namespace Hrms.Domain.ValueObjects;

public record EmployeeFilter
{
    public Guid? EmployeeId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? PayrollGroupId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? OperationAreaId { get; set; }
}
public record ChangeRestDayEmployeeFilter: EmployeeFilter
{
    public DayName DayName  { get; set; }
}


public class CreateEmployee
{
    public int? BioId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public Guid? PayrollGroupId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? AreaId { get; set; }
    public Guid? BranchId { get; set; }

    public Guid? SectionId { get; set; }
    public Guid? PositionId { get; set; }
    public JobLevelOption JobLevel { get; set; }
    public Guid? TimeShiftId { get; set; }

    public DateTime DateRegistered { get; set; }
    public DateTime? HireDate { get; set; }
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

    public decimal MonthlyRate { get; set; }
    public decimal DailyRate { get; set; }
    //public decimal HourlyRate { get; set; }
    public decimal Cola { get; set; } //PerPayroll

    public DateTime? DOB { get; set; }
    public string BloodType { get; set; } = string.Empty;

    public PaymentMethod ModeOfPayment { get; set; } = PaymentMethod.ATM;
    public SalaryType SalaryType { get; set; } = SalaryType.MONTHLY_VARIABLE;
    //public PayrollFrequency PayrollFrequency { get; set; } = PayrollFrequency.SEMI_MONTHLY;
    public EmploymentStatus EmploymentStatus { get; set; } = EmploymentStatus.Probationary;

    public string BankName { get; set; } = string.Empty;
    public string BankNo { get; set; } = string.Empty;

    public CreateSSSRate? SSSRate { get; set; }
    public CreatePHICRate? PHICRate { get; set; }
    public CreateHDMFRate? HDMFRate { get; set; }
    public CreateTaxRate? TaxRate { get; set; }

    public string SSSNo { get; set; } = string.Empty;
    public string PHICNo { get; set; } = string.Empty;
    public string HDMFNo { get; set; } = string.Empty;
    public string TIN { get; set; } = string.Empty;

    public string Contact { get; set; } = string.Empty;
    public string Address1 { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;

    //public  ICollection<Skill> Skills { get; set; }
    //public  ICollection<Education> Educations { get; set; }
    //public  ICollection<Dependent> Dependents { get; set; }
    //public  ICollection<EmployeeRecord> EmployeeRecords { get; set; }
    //public  ICollection<EmploymentHistory> Employments { get; set; }
    //public  ICollection<AssignAsset> Assets { get; set; }
    public string ProfileImg { get; set; } = string.Empty;
    //public CreatePayrollGroup? PayrollGroup { get; set; }
    public EmployeeSettingModel? Settings { get; set; }
    //public CreateClient? Client { get; set; }
    //public  Branch? Branch { get; set; }
    //public CreateDepartment? Department { get; set; }
    //public CreatePosition? Position { get; set; }
    //public CreateArea? Area { get; set; }
    public ICollection<RestDayModel> RestDays { get; set; }
    public string Status { get; set; } = "Active";
    //public CreateTimeShift? TimeShift { get; set; }
}
public class UpdateEmployee : CreateEmployee
{
    public Guid Id { get; set; }
}

[MessagePackObject]
public partial class EmployeeModel : EmployeePackModel
{
    [IgnoreMember] public string EmployeeNo { get; set; } = string.Empty;
    [IgnoreMember] public DateTime DateRegistered { get; set; }
    [IgnoreMember] public DateOnly HireDate { get; set; }
    [IgnoreMember] public DateTime? ContractStart { get; set; }
    [IgnoreMember] public DateTime? ContractEnd { get; set; }
    [IgnoreMember] public string CivilStatus { get; set; } = string.Empty;
    [IgnoreMember] public DateTime? DateResigned { get; set; }
    [IgnoreMember] public string HiringEntity { get; set; } = string.Empty;
    [IgnoreMember] public string Gender { get; set; } = string.Empty;
    [IgnoreMember] public int Age { get; set; }
    [IgnoreMember] public decimal MonthlyRate { get; set; }
    [IgnoreMember] public decimal DailyRate { get; set; }
    [IgnoreMember] public decimal Cola { get; set; } //PerPayroll
    [IgnoreMember] public DateTime? DOB { get; set; }
    [IgnoreMember] public string BloodType { get; set; } = string.Empty;
    [IgnoreMember] public PaymentMethod ModeOfPayment { get; set; } = PaymentMethod.ATM;
    [IgnoreMember] public SalaryType SalaryType { get; set; } = SalaryType.MONTHLY_VARIABLE;
    [IgnoreMember] public PayrollFrequency PayrollFrequency { get; set; } = PayrollFrequency.SEMI_MONTHLY;
    [IgnoreMember] public EmploymentStatus EmploymentStatus { get; set; } = EmploymentStatus.Probationary;
    [IgnoreMember] public string BankName { get; set; } = string.Empty;
    [IgnoreMember] public string BankNo { get; set; } = string.Empty;
    [IgnoreMember] public CreateSSSRate? SSSRate { get; set; }
    [IgnoreMember] public CreatePHICRate? PHICRate { get; set; }
    [IgnoreMember] public CreateHDMFRate? HDMFRate { get; set; }
    [IgnoreMember] public CreateTaxRate? TaxRate { get; set; }
    [IgnoreMember] public string SSSNo { get; set; } = string.Empty;
    [IgnoreMember] public string PHICNo { get; set; } = string.Empty;
    [IgnoreMember] public string HDMFNo { get; set; } = string.Empty;
    [IgnoreMember] public string TIN { get; set; } = string.Empty;
    [IgnoreMember] public string Contact { get; set; } = string.Empty;
    [IgnoreMember] public string Address1 { get; set; } = string.Empty;
    [IgnoreMember] public string Address2 { get; set; } = string.Empty;
    [IgnoreMember] public string ProfileImg { get; set; } = string.Empty;
    [IgnoreMember] public EmployeeSettingModel? Settings { get; set; }
    [IgnoreMember] public string? PayrollGroupName { get; set; }
    [IgnoreMember] public string? BranchName { get; set; }
    [IgnoreMember] public string? TimeShiftName { get; set; }
    [IgnoreMember] public string? ClientName { get; set; }
    [IgnoreMember] public string? PositionName { get; set; }
    [IgnoreMember] public string? AreaName { get; set; }
    [IgnoreMember] public string Status { get; set; }
}
public class EmployeeFullModel
{
    public int? BioId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public Guid? PayrollGroupId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? AreaId { get; set; }
    public Guid? BranchId { get; set; }

    public Guid? SectionId { get; set; }
    public Guid? PositionId { get; set; }
    public JobLevelOption JobLevel { get; set; }
    public Guid? TimeShiftId { get; set; }

    public DateTime DateRegistered { get; set; }
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
    public int Age { get; set; }

    public decimal MonthlyRate { get; set; }
    public decimal DailyRate { get; set; }
    //public decimal HourlyRate { get; set; }
    public decimal Cola { get; set; } //PerPayroll

    public DateTime? DOB { get; set; }
    public string BloodType { get; set; } = string.Empty;

    public PaymentMethod ModeOfPayment { get; set; } = PaymentMethod.ATM;
    public SalaryType SalaryType { get; set; } = SalaryType.MONTHLY_VARIABLE;
    public PayrollFrequency PayrollFrequency { get; set; } = PayrollFrequency.SEMI_MONTHLY;
    public EmploymentStatus EmploymentStatus { get; set; } = EmploymentStatus.Probationary;

    public string BankName { get; set; } = string.Empty;
    public string BankNo { get; set; } = string.Empty;

    public CreateSSSRate? SSSRate { get; set; }
    public CreatePHICRate? PHICRate { get; set; }
    public CreateHDMFRate? HDMFRate { get; set; }
    public CreateTaxRate? TaxRate { get; set; }

    public string SSSNo { get; set; } = string.Empty;
    public string PHICNo { get; set; } = string.Empty;
    public string HDMFNo { get; set; } = string.Empty;
    public string TIN { get; set; } = string.Empty;

    public string Contact { get; set; } = string.Empty;
    public string Address1 { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;

    public ICollection<SkillModel> Skills { get; set; }
    public ICollection<EducationModel> Educations { get; set; }
    public ICollection<DependentModel> Dependents { get; set; }
    public ICollection<EmployeeRecordModel> EmployeeRecords { get; set; }
    public ICollection<EmploymentHistoryModel> Employments { get; set; }
    public ICollection<AssignAssetModel> Assets { get; set; }
    public string ProfileImg { get; set; } = string.Empty;
    public EmployeeSettingModel? Settings { get; set; }
    //public  Branch? Branch { get; set; }
    public ICollection<RestDay> RestDays { get; set; }

    public string? FullName { get; set; }
    public string? PayrollGroupName { get; set; }
    public string? BranchName { get; set; }
    public string? TimeShiftName { get; set; }
    public string? ClientName { get; set; }
    public string? DepartmentName { get; set; }
    public string? PositionName { get; set; }
    public string? AreaName { get; set; }
    public string Status { get; set; }
}
public class EmployeeModelPayrollRun
{
    public Guid Id { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    //public Guid? DepartmentId { get; set; }
    public Guid? PayrollGroupId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? AreaId { get; set; }
    public PayrollGroupModel? PayrollGroup { get; set; }
    //public Guid? BranchId { get; set; }
    public JobLevelOption JobLevel { get; set; }
    public DateOnly HireDate { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string Suffix { get; set; } = string.Empty;
    public decimal MonthlyRate { get; set; }
    public decimal DailyRate { get; set; }
    public decimal Cola { get; set; } //PerPayroll
    public PaymentMethod ModeOfPayment { get; set; } = PaymentMethod.ATM;
    public SalaryType SalaryType { get; set; } = SalaryType.MONTHLY_VARIABLE;
    public PayrollFrequency PayrollFrequency { get; set; } = PayrollFrequency.SEMI_MONTHLY;
    public EmploymentStatus EmploymentStatus { get; set; } = EmploymentStatus.Probationary;
    public string BankName { get; set; } = string.Empty;
    public string BankNo { get; set; } = string.Empty;
    public CreateSSSRate? SSSRate { get; set; }
    public CreatePHICRate? PHICRate { get; set; }
    public CreateHDMFRate? HDMFRate { get; set; }
    public CreateTaxRate? TaxRate { get; set; }
    //public string SSSNo { get; set; } = string.Empty;
    //public string PHICNo { get; set; } = string.Empty;
    //public string HDMFNo { get; set; } = string.Empty;
    //public string TIN { get; set; } = string.Empty; 
    public EmployeeSettingModel? Settings { get; set; }
    public string FullName { get; set; }
    public string Status { get; set; }

}
[MessagePackObject]
public class RestDayModel
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public DayName DayName { get; set; }
}

//Statutory Schedule and Rates
public class CreateSSSRate
{
    public ComputationBasis ComputationType { get; set; }
    //public StatutoryDeductionSchedule Frequency { get; set; }
    public decimal EE { get; set; }
    public decimal ER { get; set; }
    public decimal EC { get; set; }
    public decimal AddOns { get; set; } //ee
}
public class CreatePHICRate
{
    public ComputationBasis ComputationType { get; set; }
    //public StatutoryDeductionSchedule Frequency { get; set; } //for semi monthly
    public decimal EE { get; set; }
    public decimal ER { get; set; }
    public decimal AddOns { get; set; }
}
public class CreateHDMFRate
{
    public ComputationBasis ComputationType { get; set; }
    //public StatutoryDeductionSchedule Frequency { get; set; } //for semi monthly
    public decimal EE { get; set; }
    public decimal ER { get; set; }
    public decimal AddOns { get; set; }
}
public class CreateTaxRate
{
    public ComputationBasis ComputationType { get; set; }
    //public StatutoryDeductionSchedule Frequency { get; set; }
    public decimal EE { get; set; }
    public decimal AddOns { get; set; }
    public decimal Total => EE + AddOns;
}
public class CreateEmployeeSetting
{
    public bool IsEligibleForOvertime { get; set; }
    public bool IsEligibleForHolidayPay { get; set; }
    public bool IsEligibleForNightDifferential { get; set; }
    public bool IsEligibleForLeaveCredits { get; set; }
    public bool IsEligibleFor13thMonth { get; set; }
    //public bool IsEligibleForHazardPay { get; set; }
    //public bool IsEligibleForHealthInsurance { get; set; }
    //public bool IsEligibleForRetirementBenefits { get; set; }
    //public bool IsEligibleForPerformanceBonus { get; set; }
    //public bool IsEligibleForMealAllowance { get; set; }
    //public bool IsEligibleForTransportationAllowance { get; set; }
}
public class UpdateEmployeeSetting : CreateEmployeeSetting
{
    public Guid Id { get; set; }
}
public class EmployeeSettingModel : UpdateEmployeeSetting
{
}

[MessagePackObject]
public partial class EmployeePackModel
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public int? BioId { get; set; }
    [Key(2)] public Guid? DepartmentId { get; set; }
    [Key(3)] public Guid? PayrollGroupId { get; set; }
    [Key(4)] public Guid? ClientId { get; set; }
    [Key(5)] public Guid? AreaId { get; set; }
    [Key(6)] public Guid? BranchId { get; set; }
    [Key(7)] public Guid? SectionId { get; set; }
    [Key(8)] public Guid? PositionId { get; set; }
    [Key(9)] public Guid? TimeShiftId { get; set; }
    [Key(10)] public string FirstName { get; set; } = string.Empty;
    [Key(11)] public string LastName { get; set; } = string.Empty;
    [Key(12)] public string MiddleName { get; set; } = string.Empty;
    [Key(13)] public string Suffix { get; set; } = string.Empty;
    [Key(14)] public List<RestDayModel> RestDays { get; set; }
    [Key(15)] public string? DepartmentName { get; set; }
    [Key(16)] public string? FullName { get; set; }
}

public class EmployeeDTRRun
{
    public int? BioId { get; set; }
    public Guid Id { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? AreaId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? TimeShiftId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? PayrollGroupId { get; set; }
    public string? DepartmentName { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string Suffix { get; set; } = string.Empty;
    public List<RestDayModel> RestDays { get; set; }
}

public class EmployeeFilterResponseModel
{
    public Guid Id { get; set; }
    public int? BioId { get; set; }
    public string? Name { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? AreaId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? PayrollGroupId { get; set; }
    public string? DepartmentName { get; set; }
    public string? ClientName { get; set; }
    public string? BranchName { get; set; }
    public string? PayrollGroupName { get; set; }
    public string? AreaName { get; set; }
}


