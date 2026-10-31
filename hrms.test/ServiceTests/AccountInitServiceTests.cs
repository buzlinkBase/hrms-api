using Hrms.Core.Services;
using Hrms.Core.Services.Compliance;
using Hrms.Domain;
using Hrms.Domain.Entities;
using hrms.test.TestSupport;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// AccountInitService's new-tenant seed steps: starter departments, minimum wage rates and the
/// Attendance &amp; Payroll Policy defaults, including the OT/ND method chosen on Create Company.
/// Made internal (not private) specifically so they are testable without a database.
/// </summary>
public class AccountInitServiceTests
{
    [Fact]
    public async Task SetDefaultDepartments_SeedsDistinctlyCodedDepartments()
    {
        var repo = Substitute.For<IRepository>();
        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);

        // GeneralSettingService is only used by AccountInitService's PayrollSettings step,
        // not SetDefaultDepartments -- safe to leave unconstructed here.
        var sut = new AccountInitService(uow, null!, null!);

        await sut.SetDefaultDepartments(CancellationToken.None);

        await repo.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<Department>>(departments =>
                departments.Count() > 0
                && departments.All(d => !string.IsNullOrWhiteSpace(d.Code) && !string.IsNullOrWhiteSpace(d.Name))
                && departments.Select(d => d.Code).Distinct().Count() == departments.Count()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetDefaultAttendancePayrollPolicy_SeedsTheDoleStandardOtNdMethod()
    {
        var repo = Substitute.For<IRepository>();
        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);

        var sut = new AccountInitService(uow, null!, null!);

        await sut.SetDefaultAttendancePayrollPolicy(TenantInitOptions.Defaults, CancellationToken.None);

        // A new company starts on Compounded, so it isn't seeded into a DOLE deviation it never chose.
        await repo.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<GeneralSetting>>(settings => settings.Any(s =>
                s.Description == Hrms.Domain.SettingKey.OtNdCalculationMethod.ToString()
                && s.Value == Hrms.Domain.OtNdCalculationMethod.Compounded.ToString())),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null, OtNdCalculationMethod.Compounded)]
    [InlineData("Compounded", OtNdCalculationMethod.Compounded)]
    // Additive is a DOLE deviation, but the Terms of Service acceptance covers it: no extra confirmation.
    [InlineData("Additive", OtNdCalculationMethod.Additive)]
    [InlineData("nonsense", OtNdCalculationMethod.Compounded)]
    public void TenantInitOptions_ResolvesTheChosenOtNdMethod(string? requested, OtNdCalculationMethod expected)
    {
        new TenantInitOptions(requested).ResolveOtNdMethod().Should().Be(expected);
    }

    [Fact]
    public async Task SetDefaultAttendancePayrollPolicy_Additive_SeedsItAndLogsTheOwner()
    {
        var repo = Substitute.For<IRepository>();
        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        using var db = new SqliteHrmsContext();
        await using var context = db.NewProductionLikeContext(tenantId);
        var compliance = new ComplianceAcknowledgmentService(new UnitOfWorkService(context), new LiveTerms());

        var sut = new AccountInitService(uow, null!, compliance);
        await sut.SetDefaultAttendancePayrollPolicy(new TenantInitOptions("Additive", ownerId), CancellationToken.None);
        await compliance.CommitChangesAsync();

        await repo.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<GeneralSetting>>(settings => settings.Any(s =>
                s.Description == SettingKey.OtNdCalculationMethod.ToString()
                && s.Value == OtNdCalculationMethod.Additive.ToString())),
            Arg.Any<CancellationToken>());
        await using var read = db.NewContext(tenantId);
        var ack = (await read.ComplianceAcknowledgments.IgnoreQueryFilters().AsNoTracking().ToListAsync())
            .Should().ContainSingle().Subject;
        ack.Scope.Should().Be(ComplianceAcknowledgmentService.CompanyScope);
        ack.SettingKey.Should().Be("policy.ot-nd-method");
        ack.UserId.Should().Be(ownerId);
    }

    private sealed class LiveTerms : ITermsLiveness
    {
        public Task<bool> IsLiveAsync(CancellationToken token = default) => Task.FromResult(true);
    }

    [Fact]
    public async Task SetDefaultMinimumWageRates_SeedsOneGeneralRatePerDistinctRegion()
    {
        var repo = Substitute.For<IRepository>();
        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);

        var sut = new AccountInitService(uow, null!, null!);

        await sut.SetDefaultMinimumWageRates(CancellationToken.None);

        await repo.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<MinimumWageRate>>(rates =>
                rates.Count() > 0
                && rates.All(r => !string.IsNullOrWhiteSpace(r.RegionCode) && !string.IsNullOrWhiteSpace(r.RegionName))
                && rates.All(r => r.DailyRate > 0)
                // WageOrderClass = null is the fallback rate MinimumWageEarnerResolver's
                // ResolveRegionRate falls back to when no exact-class rate exists for a
                // region -- every seeded row must provide that fallback.
                && rates.All(r => r.WageOrderClass == null)
                && rates.Select(r => r.RegionCode).Distinct().Count() == rates.Count()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetDefaultAttendancePayrollPolicy_SeedsHourlyRateDivisorAsFixed8Hours()
    {
        var repo = Substitute.For<IRepository>();
        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);

        var sut = new AccountInitService(uow, null!, null!);

        await sut.SetDefaultAttendancePayrollPolicy(TenantInitOptions.Defaults, CancellationToken.None);

        var key = SettingKey.HourlyRateDivisor.ToString();
        await repo.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<GeneralSetting>>(settings =>
                settings.Count(s => s.Description == key) == 1
                && settings.Single(s => s.Description == key).IdentityType == "Company"
                && settings.Single(s => s.Description == key).Value == HourlyRateDivisor.Fixed8Hours.ToString()),
            Arg.Any<CancellationToken>());
    }
}
