using System.Linq.Expressions;
using Hrms.Core.Services;
using Hrms.Domain.Entities;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// ResolveDeliveryFlagsAsync's opt-out default -- no row means both channels stay on, so
/// introducing NotificationPreference needs no backfill for existing employees. See
/// ApprovalEngineServiceTests for the same behavior exercised through the engine's own
/// (separately-implemented, for constructor-dependency reasons) internal copy of this lookup.
/// </summary>
public class NotificationPreferenceServiceTests
{
    private static NotificationPreferenceService BuildService(List<NotificationPreference> preferences)
    {
        var repo = Substitute.For<IRepository>();
        repo.Find<NotificationPreference>(Arg.Any<Expression<Func<NotificationPreference, bool>>>())
            .Returns(call => preferences.Where(call.Arg<Expression<Func<NotificationPreference, bool>>>().Compile()).ToList().BuildMockDbSet());

        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);
        return new NotificationPreferenceService(uow);
    }

    [Fact]
    public async Task ResolveDeliveryFlagsAsync_NoRow_DefaultsToBothChannelsOn()
    {
        var service = BuildService([]);

        var (email, push) = await service.ResolveDeliveryFlagsAsync(Guid.NewGuid(), ApprovalApplicationType.Leave, CancellationToken.None);

        email.Should().BeTrue();
        push.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveDeliveryFlagsAsync_ExplicitRow_RespectsBothFlagsIndependently()
    {
        var employeeId = Guid.NewGuid();
        var service = BuildService([
            new NotificationPreference
            {
                EmployeeId = employeeId,
                ApplicationType = ApprovalApplicationType.Leave,
                EmailEnabled = false,
                PushEnabled = true,
            },
        ]);

        var (email, push) = await service.ResolveDeliveryFlagsAsync(employeeId, ApprovalApplicationType.Leave, CancellationToken.None);

        email.Should().BeFalse();
        push.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveDeliveryFlagsAsync_RowForADifferentApplicationType_DoesNotApply()
    {
        var employeeId = Guid.NewGuid();
        var service = BuildService([
            new NotificationPreference
            {
                EmployeeId = employeeId,
                ApplicationType = ApprovalApplicationType.PayrollPosting,
                EmailEnabled = false,
                PushEnabled = false,
            },
        ]);

        var (email, push) = await service.ResolveDeliveryFlagsAsync(employeeId, ApprovalApplicationType.Leave, CancellationToken.None);

        email.Should().BeTrue();
        push.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertAsync_NoExistingRow_CreatesOne()
    {
        var preferences = new List<NotificationPreference>();
        var repo = Substitute.For<IRepository>();
        repo.Find<NotificationPreference>(Arg.Any<Expression<Func<NotificationPreference, bool>>>())
            .Returns(call => preferences.Where(call.Arg<Expression<Func<NotificationPreference, bool>>>().Compile()).ToList().BuildMockDbSet());
        repo.AddAsync(Arg.Any<NotificationPreference>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask())
            .AndDoes(call => preferences.Add(call.Arg<NotificationPreference>()));

        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);
        var service = new NotificationPreferenceService(uow);
        var employeeId = Guid.NewGuid();

        await service.UpsertAsync(employeeId, ApprovalApplicationType.Dtr, emailEnabled: false, pushEnabled: true, CancellationToken.None);

        var saved = preferences.Should().ContainSingle().Subject;
        saved.EmployeeId.Should().Be(employeeId);
        saved.ApplicationType.Should().Be(ApprovalApplicationType.Dtr);
        saved.EmailEnabled.Should().BeFalse();
        saved.PushEnabled.Should().BeTrue();
    }
}
