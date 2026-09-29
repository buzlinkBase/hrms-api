using hrms.test.LeaveWorkerTests.TestSupport;
using MassTransit;
using NSubstitute;

namespace hrms.test.LeaveWorkerTests;

/// <summary>
/// The manual "grant this year's leave credits" action: it used to always assume a calendar
/// fiscal year, and publish without saving -- which the bus outbox never sends.
/// </summary>
public class LeavePeriodGrantTriggerServiceTests : LeaveWorkerTestBase
{
    private static GeneralSetting FiscalStart(string month) => new()
    {
        Id = NewId(),
        IdentityType = FiscalYearSettings.IdentityType,
        Description = FiscalYearSettings.Key,
        Value = month,
    };

    [Fact]
    public async Task UsesTheCompanysFiscalYear_AndCommitsSoTheOutboxSendsIt()
    {
        var repo = CreateRepo();
        var uow = CreateUow(repo);
        SeedFindAll(repo, FiscalStart("4"));
        var publisher = Substitute.For<IPublishEndpoint>();
        var sut = new LeavePeriodGrantTriggerService(uow, publisher);

        var year = await sut.TriggerAsync(2026, CancellationToken.None);

        year.Should().Be(2026);
        await publisher.Received(1).Publish(
            Arg.Is<RunLeavePeriodGrant>(m => m.Year == 2026 && m.FiscalYearStartMonth == 4), Arg.Any<CancellationToken>());
        await uow.Received(1).CommitChangesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithoutAYear_DefaultsToTheCurrentFiscalYear()
    {
        var repo = CreateRepo();
        var uow = CreateUow(repo);
        SeedFindAll(repo, FiscalStart("1"));
        var publisher = Substitute.For<IPublishEndpoint>();

        var year = await new LeavePeriodGrantTriggerService(uow, publisher).TriggerAsync(null, CancellationToken.None);

        year.Should().Be(DateTime.UtcNow.Year);
    }
}
