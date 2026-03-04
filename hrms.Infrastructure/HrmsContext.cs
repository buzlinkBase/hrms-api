using BuzlinkRepository;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
namespace Hrms.Infrastructure;

public class HrmsContext : DbContext
{
    public Guid? TenantId { get; private set; }
    public HrmsContext(DbContextOptions<HrmsContext> options, ITenantProvider provider) : base(options)
    {
        TenantId = provider.TenantId;
    }

    public HrmsContext(DbContextOptions<HrmsContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        if (TenantId != null)
        {
            modelBuilder.UseSoftDelete(TenantId.Value);
        }
    }
    #region "Hrms"
    //public DbSet<AuditEntryEntity> AuditEntries { get; set; }
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
    public DbSet<ChangeRestDay>  ChangeRestDays { get; set; } 
    #endregion
}

public class HrmsContextFactory : IDesignTimeDbContextFactory<HrmsContext>
{
    public HrmsContext CreateDbContext(string[] args)
    {
        string basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "hrms-api");
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            //.AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json")
            .Build();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var optionsBuilder = new DbContextOptionsBuilder<HrmsContext>();
        optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        return new HrmsContext(optionsBuilder.Options);
    }
}
