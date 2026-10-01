using hrms.test.TestSupport;
using Hrms.Core.Services.Subscriptions;
using Hrms.Domain.Entities.EmployeeEntities;
using Mapster;
using MapsterMapper;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Onepunch.Common.Lib.Subscriptions;

namespace hrms.test.SubscriptionTests;

/// <summary>
/// The plan's employees.max is checked wherever employees are added -- one at a time (AddAsync)
/// or in a batch (AddOrUpdateRange: import and the seeder) -- before anything is saved, and only
/// for employees who aren't separated.
/// </summary>
public class EmployeeLimitTests
{
    private sealed class LimitReached() : Exception("limit reached");

    private static (EmployeeService Service, IPlanLimitGuard Limits, IRepository Repo) Build()
    {
        var repo = Substitute.For<IRepository>();
        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);
        _ = TestMapping.GlobalSettings;

        var limits = Substitute.For<IPlanLimitGuard>();
        limits.EnsureCanAddAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new LimitReached());

        var service = new EmployeeService(
            uow,
            Substitute.For<IMapper>(),
            TypeAdapterConfig.GlobalSettings,
            new DepartmentService(uow),
            new PayrollGroupService(uow),
            new BranchService(uow),
            new CostCenterService(uow),
            new PositionService(uow),
            new SectionService(uow),
            limits: limits);
        return (service, limits, repo);
    }

    [Fact]
    public async Task Batch_CountsOnlyNewEmployeesWhoArentSeparated_AndSavesNothingWhenRefused()
    {
        var (service, limits, repo) = Build();
        var batch = new List<Employee>
        {
            new() { EmploymentStatus = EmploymentStatus.Regular },
            new() { EmploymentStatus = EmploymentStatus.Probationary },
            new() { EmploymentStatus = EmploymentStatus.Resigned },
            new() { Id = Guid.NewGuid(), EmploymentStatus = EmploymentStatus.Regular },
        };

        var act = () => service.AddOrUpdateRange(batch);

        await act.Should().ThrowAsync<LimitReached>();
        await limits.Received(1).EnsureCanAddAsync(EntitlementKeys.EmployeesMax, 2, Arg.Any<CancellationToken>());
        repo.ReceivedCalls().Should().BeEmpty("nothing is added or updated when the batch doesn't fit");
    }

    [Fact]
    public void SeparatedStatuses_AreExactlyTheFourEndings() =>
        EmployeeService.SeparatedStatuses.Should().BeEquivalentTo(
            [EmploymentStatus.Terminated, EmploymentStatus.Resigned, EmploymentStatus.Retired, EmploymentStatus.Deceased]);
}
