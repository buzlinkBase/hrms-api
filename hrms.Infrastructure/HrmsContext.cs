using BuzlinkRepository;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using MassTransit;
using Microsoft.Extensions.Configuration;
namespace Hrms.Infrastructure;

using Microsoft.EntityFrameworkCore;

public class HrmsContext : DbContext, IDbContext
{
    private readonly TenantConnectionStringInfo _tenantConnectionInfo;
    private readonly ITenantProvider _tenantProvider;
    private readonly IConfiguration _configuration;

    public HrmsContext(
         DbContextOptions<HrmsContext> options,
         TenantConnectionStringInfo tenantConnectionInfo,
         ITenantProvider tenantProvider,
         IConfiguration configuration) : base(options)
    {
        _tenantConnectionInfo = tenantConnectionInfo;
        _tenantProvider = tenantProvider;
        _configuration = configuration;
    }  

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    { 
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.UseTenantAndDateFilter(_tenantProvider?.TenantId ?? Guid.Empty);

        // Every query implicitly filters on TenantId + DeletedAt via the global query filter above,
        // so every entity that carries them needs an index covering that filter.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IEntityTenant).IsAssignableFrom(entityType.ClrType)) continue;
            if (entityType.FindProperty(nameof(ITimeStamp.DeletedAt)) == null) continue;

            modelBuilder.Entity(entityType.ClrType)
                .HasIndex(nameof(IEntityTenant.TenantId), nameof(ITimeStamp.DeletedAt));
        }
    }

    #region "dbsets" 
    public DbSet<Company> Companies { get; set; }
    //public DbSet<PayrollInclusionDefaults> PayrollInclusionDefaults { get; set; }
    public DbSet<Branch> Branches { get; set; }
    public DbSet<Client> Clients { get; set; }
    public DbSet<CostCenters> Areas { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<Payroll> Payrolls { get; set; }
    public DbSet<PayrollDtrDetail> PayrollDtrDetails { get; set; }
    public DbSet<PayrollDeductionDetail> PayrollDeductionDetails { get; set; }
    public DbSet<RetirementFund> RetirementFunds { get; set; }
    public DbSet<RetirementLedger> RetirementLedgers { get; set; }
    public DbSet<UniformAllowanceFund> UniformAllowanceFunds { get; set; }
    public DbSet<UniformAllowanceLedger> UniformAllowanceLedgers { get; set; }
    public DbSet<PayrollBatch> PayrollBatches { get; set; }
    public DbSet<PayrollGroup> PayrollGroups { get; set; }
    public DbSet<CutoffDay> CutoffDays { get; set; }
    public DbSet<Section> Sections { get; set; }
    public DbSet<RateTable> PremiumRates { get; set; }
    public DbSet<ClientRateTable> ClientRateTables { get; set; }
    public DbSet<ClientBillingInfo> ClientBillingInfos { get; set; }
    public DbSet<EmployeeSetting> EmployeeSettings { get; set; }

    public DbSet<DailyRecord> DailyTimeRecords { get; set; }
    public DbSet<DtrLeaveMetaData>  DtrLeaveMetaDatas { get; set; }
    public DbSet<Attendance> Attendances { get; set; }
    public DbSet<Employee> Employees { get; set; }
    public DbSet<Skill> Skills { get; set; }
    public DbSet<Education> Educations { get; set; }
    public DbSet<Dependent> Dependents { get; set; }
    public DbSet<PriorEmployerTaxRecord> PriorEmployerTaxRecords { get; set; }
    public DbSet<PayrollOpeningBalance> PayrollOpeningBalances { get; set; }
    public DbSet<EmploymentHistory> Employments { get; set; }
    public DbSet<EmployeeRecord> EmployeeRecords { get; set; }
    public DbSet<AssignAsset> AssignAssets { get; set; }
    public DbSet<SSSRate> SSSRates { get; set; }
    public DbSet<PHICRate> PHICRates { get; set; }
    public DbSet<HDMFRate> HDMFRates { get; set; }
    public DbSet<TaxRate> TaxRates { get; set; }
    public DbSet<Position> Positions { get; set; }
    public DbSet<RestDay> RestDays { get; set; }
    public DbSet<EmployeeFixedSchedule> EmployeeFixedSchedules { get; set; }
    public DbSet<RestDayDate> RestDayDates { get; set; }

    public DbSet<Leave> Leaves { get; set; }
    public DbSet<LeaveCredits> LeaveCredits { get; set; }
    public DbSet<LeaveLedger> LeaveLedgers { get; set; }
    public DbSet<Holiday> Holidays { get; set; }
    public DbSet<MinimumWageRate> MinimumWageRates { get; set; }
    public DbSet<ChangeHoliday> ChangeHolidays { get; set; }
    public DbSet<ClientHoliday> ClientHolidays { get; set; }
    public DbSet<ManualBatchEntryLog> ManualAttendance { get; set; }
    public DbSet<TimeShift> TimeShifts { get; set; }
    public DbSet<WorkSchedulePlan> WorkSchedulePlans { get; set; }
    public DbSet<OverTimeApplication> OTApplications { get; set; }
    public DbSet<UnderTimeApplication> UTApplications { get; set; }
    public DbSet<TravelOrderApplication> TravelOrderApplications { get; set; }
    public DbSet<PassSlipApplication> PassSlipApplications { get; set; }
    public DbSet<EmployeeProfileUpdateRequest> EmployeeProfileUpdateRequests { get; set; }
    public DbSet<EmployeeProfileUpdateRequestDocument> EmployeeProfileUpdateRequestDocuments { get; set; }

    public DbSet<OtherIncomeType> AllowanceTypes { get; set; }
    public DbSet<ProratedAllowanceForSSS> ProratedAllowances { get; set; }
    public DbSet<OtherIncome> Allowances { get; set; }
    public DbSet<DeductionType> DeductionTypes { get; set; }
    public DbSet<Deduction> Deductions { get; set; }
    public DbSet<OtherIncomeApplication> OtherIncomeApplications { get; set; }
    public DbSet<OtherIncomeSchedules> OtherIncomeApplicationDetails { get; set; }
    public DbSet<DeductionApplication> DeductionApplications { get; set; }
    public DbSet<DeductionApplicationDetail> DeductionApplicationDetails { get; set; }
    public DbSet<IncomePayment> IncomePayments { get; set; }
    public DbSet<DeductionPayment> DeductionPayments { get; set; }
    public DbSet<LeaveApplication> leaveApplications { get; set; }

    public DbSet<SSSTable> GovSSSes { get; set; }
    public DbSet<PHICTable> GovPHICs { get; set; }
    public DbSet<HDMFTable> GovHDMFs { get; set; }
    public DbSet<TaxTable> GovTaxes { get; set; }
    public DbSet<AnnualTaxTable> GovAnnualTaxes { get; set; }

    public DbSet<SSSContribution> SSSContributions { get; set; }
    public DbSet<PHICContribution> PHICContributions { get; set; }
    public DbSet<HDMFContribution> HDMFContributions { get; set; }
    public DbSet<WTaxContribution> TaxContributions { get; set; }
    public DbSet<ThirteenthMonthLedger> ThirteenthMonthLedgers { get; set; }
    public DbSet<GeneralSetting> GeneralSettings { get; set; }
    public DbSet<YearLock> YearLocks { get; set; }
    public DbSet<ChangeRestDay> ChangeRestDays { get; set; }
    public DbSet<SalaryAdjustment> SalaryAdjustments { get; set; }

    public DbSet<ApprovalWorkflow> ApprovalWorkflows { get; set; }
    public DbSet<ApprovalWorkflowStep> ApprovalWorkflowSteps { get; set; }
    public DbSet<ApprovalWorkflowStepApprover> ApprovalWorkflowStepApprovers { get; set; }
    public DbSet<ApprovalInstance> ApprovalInstances { get; set; }
    public DbSet<ApprovalAction> ApprovalActions { get; set; }

    #endregion
} 