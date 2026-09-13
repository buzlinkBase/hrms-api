using Hrms.Core.Services;
using Hrms.Domain.Entities;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// AccountInitService.SetDefaultDepartments -- the new-tenant seed step that gives every
/// tenant a starter set of departments instead of an empty Setup > Department list. Made
/// internal (not private) specifically so this is testable without a database. The other
/// AccountInitService.Create() seed steps aren't covered here (pre-existing, out of scope).
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
        var sut = new AccountInitService(uow, null!);

        await sut.SetDefaultDepartments(CancellationToken.None);

        await repo.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<Department>>(departments =>
                departments.Count() > 0
                && departments.All(d => !string.IsNullOrWhiteSpace(d.Code) && !string.IsNullOrWhiteSpace(d.Name))
                && departments.Select(d => d.Code).Distinct().Count() == departments.Count()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetDefaultMinimumWageRates_SeedsOneGeneralRatePerDistinctRegion()
    {
        var repo = Substitute.For<IRepository>();
        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);

        var sut = new AccountInitService(uow, null!);

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
}
