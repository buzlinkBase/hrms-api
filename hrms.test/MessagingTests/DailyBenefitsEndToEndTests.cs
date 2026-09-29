using Hrms.Core.Messaging.BenefitWorkers;
using Hrms.Core.Messaging.DailyBenefits;
using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Onepunch.Common.Lib;

namespace hrms.test.MessagingTests;

/// <summary>
/// A whole fiscal-year-start run through the real steps against SQLite, each in its own scope
/// exactly as in production. Proves the ordering fixes: someone with unused days gets both the
/// carried days and the new year's grant; an accrual-based type accrues without anyone having
/// created a record by hand; and running the same day again changes nothing.
/// </summary>
public class DailyBenefitsEndToEndTests : IDisposable
{
    // Empty: the SQLite test context has no tenant-stamping interceptor, so rows are written
    // without a company and an empty tenant applies no company filter. Company scoping itself is
    // covered by DailyBenefitsTests.
    private static readonly Guid TenantId = Guid.Empty;
    private readonly SqliteHrmsContext _db = new();

    public void Dispose() => _db.Dispose();

    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ITenantProvider, FakeTenantProvider>();
        services.AddScoped<TenantConnectionStringInfo>();
        services.AddScoped(sp => _db.NewContext(sp.GetRequiredService<ITenantProvider>()));
        services.AddScoped<IUnitOfWorkService>(sp => new UnitOfWorkService(sp.GetRequiredService<HrmsContext>()));
        services.AddSingleton(Substitute.For<IPublishEndpoint>());
        services.AddScoped(sp =>
        {
            var uow = sp.GetRequiredService<IUnitOfWorkService>();
            return new DailyRecordService(uow, new TypeAdapterConfig(), Substitute.For<IMapper>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DailyRecordService>>(),
                new LeaveDtrReconciliationService(uow, sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LeaveDtrReconciliationService>>()),
                new PayrollBatchService(uow), new DTRBatchService(uow),
                new ApprovalEngineService(uow, sp.GetRequiredService<IPublishEndpoint>()));
        });
        services.AddScoped<UniformAllowanceFundService>();
        services.AddDailyBenefits();
        return services.BuildServiceProvider();
    }

    private async Task RunDayAsync(ServiceProvider services, DateOnly date)
    {
        using var scope = services.CreateScope();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
        tenant.SetTenantId(TenantId);
        var connection = scope.ServiceProvider.GetRequiredService<TenantConnectionStringInfo>();
        connection.TenantId = TenantId;

        var worker = new DailyBenefitsWorker(
            services.GetRequiredService<IServiceScopeFactory>(), tenant, connection,
            services.GetServices<IDailyBenefitStep>(),
            services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DailyBenefitsWorker>>());
        var context = Substitute.For<ConsumeContext<RunDailyBenefits>>();
        context.Message.Returns(new RunDailyBenefits(date, 1));
        await worker.Consume(context);
    }

    [Fact]
    public async Task FiscalYearStart_CarriesOverThenGrants_AndAccrualTypesAccrueOnTheirOwn()
    {
        Guid employeeId, vacationId, sickId;
        await using (var seed = _db.NewContext(new FakeTenantProvider(TenantId)))
        {
            var payrollGroup = new PayrollGroup { Id = Guid.NewGuid() };
            seed.PayrollGroups.Add(payrollGroup);
            var employee = new Employee
            {
                Id = Guid.NewGuid(), PayrollGroupId = payrollGroup.Id,
                EmploymentStatus = EmploymentStatus.Regular, HireDate = new DateOnly(2020, 1, 1),
            };
            seed.Employees.Add(employee);
            // Lump-sum 5/year, unused days carry over without limit.
            var vacation = new Leave
            {
                Id = Guid.NewGuid(), Code = "VL", Description = "Vacation Leave",
                AccrualBasis = AccrualBasis.None, Credits = 5, CarryOverType = CarryOverType.Unlimited,
            };
            // 1.25 per month; nobody ever creates its record by hand.
            var sick = new Leave
            {
                Id = Guid.NewGuid(), Code = "SL", Description = "Sick Leave",
                AccrualBasis = AccrualBasis.Monthly, AccrualRate = 1.25, CarryOverType = CarryOverType.Forfeit,
            };
            seed.Leaves.AddRange(vacation, sick);
            seed.LeaveCredits.Add(new LeaveCredits
            {
                Id = Guid.NewGuid(), EmployeeId = employee.Id, LeaveId = vacation.Id, PeriodYear = 2025,
                FromDate = new DateTime(2025, 1, 1), ToDate = new DateTime(2025, 12, 31, 23, 59, 59),
                Granted = 5, Used = 2, Balance = 3,
            });
            await seed.SaveChangesAsync();
            (employeeId, vacationId, sickId) = (employee.Id, vacation.Id, sick.Id);
        }

        await using var services = BuildServices();
        await RunDayAsync(services, new DateOnly(2026, 1, 1));
        await RunDayAsync(services, new DateOnly(2026, 1, 1)); // a retried/caught-up repeat

        await using var verify = _db.NewContext(new FakeTenantProvider(TenantId));
        var vacation2026 = await verify.LeaveCredits.SingleAsync(x => x.LeaveId == vacationId && x.PeriodYear == 2026);
        vacation2026.Balance.Should().Be(8m, "3 carried over + 5 granted -- the grant used to skip anyone who carried days");
        var sick2026 = await verify.LeaveCredits.SingleAsync(x => x.LeaveId == sickId && x.PeriodYear == 2026);
        sick2026.Balance.Should().Be(1.25m, "the record was opened, then accrued -- once");
        (await verify.LeaveLedgers.CountAsync(x => x.EmployeeId == employeeId && x.EntryType == LedgerEntryType.CarryOver)).Should().Be(1);
        (await verify.LeaveLedgers.CountAsync(x => x.EmployeeId == employeeId && x.EntryType == LedgerEntryType.Grant)).Should().Be(1);
        (await verify.LeaveLedgers.CountAsync(x => x.EmployeeId == employeeId && x.EntryType == LedgerEntryType.Accrual)).Should().Be(1);
    }

    private sealed class FakeTenantProvider : ITenantProvider
    {
        public FakeTenantProvider() { }
        public FakeTenantProvider(Guid tenantId) => TenantId = tenantId;
        public Guid TenantId { get; private set; }
        public void SetTenantId(Guid tenantId) => TenantId = tenantId;
    }
}
