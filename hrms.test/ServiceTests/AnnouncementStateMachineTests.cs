using Hrms.Core.Services.Announcements.Lifecycle;
using Hrms.Domain.Entities.Announcements;

namespace hrms.test.ServiceTests;

/// <summary>
/// The announcement lifecycle table -- what each state allows and where each action leads.
/// Sent announcements carry receipts, so nothing sent is ever removed outright.
/// </summary>
public class AnnouncementStateMachineTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 8, 0, 0, DateTimeKind.Utc);

    private static Announcement InState(AnnouncementState state) => new() { State = state };

    [Theory]
    [InlineData(AnnouncementState.Draft, new[] { AnnouncementAction.Edit, AnnouncementAction.Schedule, AnnouncementAction.PublishNow, AnnouncementAction.Duplicate, AnnouncementAction.Delete })]
    [InlineData(AnnouncementState.Scheduled, new[] { AnnouncementAction.Edit, AnnouncementAction.Unschedule, AnnouncementAction.PublishNow, AnnouncementAction.Duplicate, AnnouncementAction.Delete })]
    [InlineData(AnnouncementState.Published, new[] { AnnouncementAction.Edit, AnnouncementAction.Remind, AnnouncementAction.Pin, AnnouncementAction.Unpublish, AnnouncementAction.Archive, AnnouncementAction.Duplicate, AnnouncementAction.Delete })]
    [InlineData(AnnouncementState.Unpublished, new[] { AnnouncementAction.Republish, AnnouncementAction.Archive, AnnouncementAction.Duplicate, AnnouncementAction.Delete })]
    [InlineData(AnnouncementState.Archived, new[] { AnnouncementAction.Restore, AnnouncementAction.Duplicate, AnnouncementAction.Delete })]
    public void AllowedActions_MatchTheLifecycleTable(AnnouncementState state, AnnouncementAction[] expected)
    {
        AnnouncementStateMachine.AllowedActions(InState(state), Now).Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void AllowedActions_NeverOfferSystemTransitions()
    {
        var expired = new Announcement { State = AnnouncementState.Published, ExpiresAtUtc = Now.AddMinutes(-1) };
        AnnouncementStateMachine.AllowedActions(expired, Now).Should().NotContain(AnnouncementAction.Expire);

        var binnedLongAgo = new Announcement { State = AnnouncementState.Deleted, BinnedAtUtc = Now.AddDays(-60) };
        AnnouncementStateMachine.AllowedActions(binnedLongAgo, Now).Should().BeEmpty("it can only be purged now, by the system");
    }

    [Fact]
    public void PinAndUnpin_AreOfferedAccordingToTheCurrentPin()
    {
        var pinned = new Announcement { State = AnnouncementState.Published, IsPinned = true };
        AnnouncementStateMachine.AllowedActions(pinned, Now).Should().Contain(AnnouncementAction.Unpin).And.NotContain(AnnouncementAction.Pin);

        var pinExpired = new Announcement { State = AnnouncementState.Published, IsPinned = true, PinnedUntilUtc = Now.AddHours(-1) };
        AnnouncementStateMachine.AllowedActions(pinExpired, Now).Should().Contain(AnnouncementAction.Pin).And.NotContain(AnnouncementAction.Unpin);
    }

    [Fact]
    public void Deleted_CanBeRestoredOnlyWithinRetention()
    {
        var recent = new Announcement { State = AnnouncementState.Deleted, BinnedAtUtc = Now.AddDays(-29) };
        AnnouncementStateMachine.IsAllowed(recent, AnnouncementAction.RestoreDeleted, Now).Should().BeTrue();
        AnnouncementStateMachine.IsAllowed(recent, AnnouncementAction.Purge, Now).Should().BeFalse();

        var old = new Announcement { State = AnnouncementState.Deleted, BinnedAtUtc = Now.AddDays(-31) };
        AnnouncementStateMachine.IsAllowed(old, AnnouncementAction.RestoreDeleted, Now).Should().BeFalse();
        AnnouncementStateMachine.IsAllowed(old, AnnouncementAction.Purge, Now).Should().BeTrue();
    }

    [Theory]
    [InlineData(AnnouncementState.Draft, null)]
    [InlineData(AnnouncementState.Scheduled, null)]
    [InlineData(AnnouncementState.Published, AnnouncementState.Deleted)]
    [InlineData(AnnouncementState.Unpublished, AnnouncementState.Deleted)]
    [InlineData(AnnouncementState.Archived, AnnouncementState.Deleted)]
    public void Delete_RemovesOnlyNeverSentOnes_AndBinsTheRest(AnnouncementState state, AnnouncementState? expected)
    {
        AnnouncementStateMachine.Next(InState(state), AnnouncementAction.Delete).Should().Be(expected);
    }

    [Fact]
    public void Restore_ReturnsToThePreviousState()
    {
        var archivedWhileUnpublished = new Announcement
        {
            State = AnnouncementState.Archived,
            StateBeforeArchive = AnnouncementState.Unpublished,
        };
        AnnouncementStateMachine.Next(archivedWhileUnpublished, AnnouncementAction.Restore).Should().Be(AnnouncementState.Unpublished);

        var binnedWhileArchived = new Announcement
        {
            State = AnnouncementState.Deleted,
            StateBeforeDelete = AnnouncementState.Archived,
        };
        AnnouncementStateMachine.Next(binnedWhileArchived, AnnouncementAction.RestoreDeleted).Should().Be(AnnouncementState.Archived);
    }

    [Theory]
    [InlineData(AnnouncementState.Deleted, AnnouncementAction.Edit)]
    [InlineData(AnnouncementState.Unpublished, AnnouncementAction.Edit)]
    [InlineData(AnnouncementState.Published, AnnouncementAction.Schedule)]
    [InlineData(AnnouncementState.Draft, AnnouncementAction.Unpublish)]
    [InlineData(AnnouncementState.Archived, AnnouncementAction.Remind)]
    [InlineData(AnnouncementState.Draft, AnnouncementAction.Archive)]
    public void EnsureAllowed_RejectsActionsTheStateDoesNotAllow(AnnouncementState state, AnnouncementAction action)
    {
        var act = () => AnnouncementStateMachine.EnsureAllowed(InState(state), action, Now);
        act.Should().Throw<ValidationException>();
    }
}
