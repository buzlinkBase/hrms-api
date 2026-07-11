using BuzlinkRepository;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.Extensions.Configuration;
namespace Hrms.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

public class HrmsContext : DbContext, IDbContext
{
    private readonly TenantConnectionStringInfo _tenantConnectionInfo;
    private readonly IConfiguration _configuration;

    public HrmsContext(
         DbContextOptions<HrmsContext> options,
         TenantConnectionStringInfo tenantConnectionInfo,
         IConfiguration configuration) : base(options)
    {
        _tenantConnectionInfo = tenantConnectionInfo;
        _configuration = configuration;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured) return; 
        var connectionString = _configuration.GetConnectionString("HrmsConnection");
        if (_tenantConnectionInfo.ConnectionString != null)
        {
            connectionString = _tenantConnectionInfo.ConnectionString;
        }
        if (!string.IsNullOrEmpty(connectionString))
        {
            var serverVersion = new MySqlServerVersion(new Version(9, 2, 0));
            optionsBuilder.UseMySql(connectionString, serverVersion, x => x.UseNetTopologySuite());
            optionsBuilder.UseLazyLoadingProxies(true);
            optionsBuilder.AddInterceptors(new SoftDeleteInterceptor());
            optionsBuilder.ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        //generate sortable GUID
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var idProperty = entityType.FindProperty("Id");
            if (idProperty != null && idProperty.ClrType == typeof(Guid))
            {
                idProperty.SetValueGeneratorFactory((_, __) => new Version7GuidValueGenerator());
            }
        }
        modelBuilder.UseDateFilter();
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }

    #region "dbsets" 
    public DbSet<Company> Companies { get; set; }
    public DbSet<Branch> Branches { get; set; }
    public DbSet<Client> Clients { get; set; }
    public DbSet<CostCenters> Areas { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<Payroll> Payrolls { get; set; }
    public DbSet<PayrollGroup> PayrollGroups { get; set; }
    public DbSet<Section> Sections { get; set; }
    public DbSet<RateTable> PremiumRates { get; set; }
    public DbSet<EmployeeSetting> EmployeeSettings { get; set; }

    public DbSet<DailyRecord> DailyTimeRecords { get; set; }
    public DbSet<Attendance> Attendances { get; set; }
    public DbSet<Employee> Employees { get; set; }
    public DbSet<Skill> Skills { get; set; }
    public DbSet<Education> Educations { get; set; }
    public DbSet<Dependent> Dependents { get; set; }
    public DbSet<EmploymentHistory> Employments { get; set; }
    public DbSet<EmployeeRecord> EmployeeRecords { get; set; }
    public DbSet<AssignAsset> AssignAssets { get; set; }
    public DbSet<SSSRate> SSSRates { get; set; }
    public DbSet<PHICRate> PHICRates { get; set; }
    public DbSet<HDMFRate> HDMFRates { get; set; }
    public DbSet<TaxRate> TaxRates { get; set; }
    public DbSet<Position> Positions { get; set; }
    public DbSet<RestDay> RestDays { get; set; }
    public DbSet<RestDayDate> RestDayDates { get; set; }

    public DbSet<Leave> Leaves { get; set; }
    public DbSet<LeaveLedger> LeaveLedgers { get; set; }
    public DbSet<Holiday> Holidays { get; set; }
    public DbSet<ChangeHoliday> ChangeHolidays { get; set; }
    public DbSet<ClientHoliday> ClientHolidays { get; set; }
    public DbSet<ManualBatchEntryLog> ManualAttendance { get; set; }
    public DbSet<TimeShift> TimeShifts { get; set; }
    public DbSet<WorkSchedulePlan> WorkSchedulePlans { get; set; }
    public DbSet<OverTimeApplication> OTApplications { get; set; }
    public DbSet<UnderTimeApplication> UTApplications { get; set; }

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
    public DbSet<LeaveApplicationDetail> LeaveApplicationDetails { get; set; }

    public DbSet<SSSTable> GovSSSes { get; set; }
    public DbSet<PHICTable> GovPHICs { get; set; }
    public DbSet<HDMFTable> GovHDMFs { get; set; }
    public DbSet<TaxTable> GovTaxes { get; set; }

    public DbSet<SSSContribution> SSSContributions { get; set; }
    public DbSet<PHICContribution> PHICContributions { get; set; }
    public DbSet<HDMFContribution> HDMFContributions { get; set; }
    public DbSet<WTaxContribution> TaxContributions { get; set; }
    public DbSet<ThirteenthMonthLedger> ThirteenthMonthLedgers { get; set; }
    public DbSet<GeneralSetting> GeneralSettings { get; set; }
    public DbSet<ChangeRestDay> ChangeRestDays { get; set; }



    #endregion
}

public class Version7GuidValueGenerator : ValueGenerator<Guid>
{
    public override bool GeneratesTemporaryValues => false;
    public override Guid Next(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        return Guid.CreateVersion7();
    }
}