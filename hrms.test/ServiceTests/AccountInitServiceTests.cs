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
}
