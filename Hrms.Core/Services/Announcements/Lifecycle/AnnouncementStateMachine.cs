using Hrms.Domain.Entities.Announcements;

namespace Hrms.Core.Services.Announcements.Lifecycle;

public enum AnnouncementAction
{
    Edit,
    Schedule,
    Unschedule,
    PublishNow,
    Unpublish,
    Republish,
    Remind,
    Pin,
    Unpin,
    Archive,
    Restore,
    Duplicate,
    Delete,
    RestoreDeleted,
    // System-only transitions, run by AnnouncementLifecycleScheduler -- never offered to a user.
    Expire,
    Purge,
}

/// <summary>
/// The one place that decides what can happen to an announcement in each state (State pattern,
/// table-driven). Every AnnouncementService action checks <see cref="EnsureAllowed"/> first, and
/// the API returns <see cref="AllowedActions"/> on each announcement so the UI's action menu is
/// rendered from these same rules instead of repeating them.
///
/// Follows the usual internal-comms/policy-acknowledgement lifecycle: only a never-sent Draft or
/// Scheduled announcement is removed for good; a sent one carries read/acknowledgement receipts
/// (compliance records), so it is unpublished, archived or binned (restorable for
/// <see cref="BinRetention"/>), never destroyed on the spot.
/// </summary>
public static class AnnouncementStateMachine
{
    public static readonly TimeSpan BinRetention = TimeSpan.FromDays(30);

    private static readonly AnnouncementAction[] SystemActions = [AnnouncementAction.Expire, AnnouncementAction.Purge];

    // State -> the user actions it allows (extra per-action conditions live in IsAllowed).
    private static readonly IReadOnlyDictionary<AnnouncementState, AnnouncementAction[]> Allowed =
        new Dictionary<AnnouncementState, AnnouncementAction[]>
        {
            [AnnouncementState.Draft] =
            [
                AnnouncementAction.Edit, AnnouncementAction.Schedule, AnnouncementAction.PublishNow,
                AnnouncementAction.Duplicate, AnnouncementAction.Delete,
            ],
            [AnnouncementState.Scheduled] =
            [
                AnnouncementAction.Edit, AnnouncementAction.Unschedule, AnnouncementAction.PublishNow,
                AnnouncementAction.Duplicate, AnnouncementAction.Delete,
            ],
            [AnnouncementState.Published] =
            [
                AnnouncementAction.Edit, AnnouncementAction.Remind, AnnouncementAction.Pin, AnnouncementAction.Unpin,
                AnnouncementAction.Unpublish, AnnouncementAction.Archive, AnnouncementAction.Duplicate,
                AnnouncementAction.Delete, AnnouncementAction.Expire,
            ],
            [AnnouncementState.Unpublished] =
            [
                AnnouncementAction.Republish, AnnouncementAction.Archive, AnnouncementAction.Duplicate,
                AnnouncementAction.Delete,
            ],
            [AnnouncementState.Archived] =
            [
                AnnouncementAction.Restore, AnnouncementAction.Duplicate, AnnouncementAction.Delete,
            ],
            [AnnouncementState.Deleted] =
            [
                AnnouncementAction.RestoreDeleted, AnnouncementAction.Purge,
            ],
        };

    public static bool IsPinnedNow(Announcement a, DateTime nowUtc) =>
        a.IsPinned && (a.PinnedUntilUtc == null || a.PinnedUntilUtc > nowUtc);

    public static bool IsExpired(Announcement a, DateTime nowUtc) =>
        a.ExpiresAtUtc != null && a.ExpiresAtUtc <= nowUtc;

    public static bool IsAllowed(Announcement a, AnnouncementAction action, DateTime nowUtc)
    {
        if (!Allowed.TryGetValue(a.State, out var actions) || !actions.Contains(action)) return false;

        return action switch
        {
            AnnouncementAction.Pin => !IsPinnedNow(a, nowUtc),
            AnnouncementAction.Unpin => IsPinnedNow(a, nowUtc),
            AnnouncementAction.Expire => IsExpired(a, nowUtc),
            AnnouncementAction.RestoreDeleted => a.BinnedAtUtc != null && a.BinnedAtUtc.Value + BinRetention > nowUtc,
            AnnouncementAction.Purge => a.BinnedAtUtc != null && a.BinnedAtUtc.Value + BinRetention <= nowUtc,
            _ => true,
        };
    }

    public static void EnsureAllowed(Announcement a, AnnouncementAction action, DateTime nowUtc)
    {
        if (!IsAllowed(a, action, nowUtc))
            throw new ValidationException($"Can't {Describe(action)} an announcement that is {a.State.ToString().ToLowerInvariant()}.");
    }

    /// <summary>The user-facing actions allowed right now (system transitions left out).</summary>
    public static IReadOnlyList<AnnouncementAction> AllowedActions(Announcement a, DateTime nowUtc) =>
        Allowed.TryGetValue(a.State, out var actions)
            ? actions.Where(x => !SystemActions.Contains(x) && IsAllowed(a, x, nowUtc)).ToList()
            : [];

    /// <summary>
    /// The state after <paramref name="action"/>, or null when the announcement is removed for
    /// good (deleting a never-sent Draft/Scheduled one, or purging from the bin).
    /// </summary>
    public static AnnouncementState? Next(Announcement a, AnnouncementAction action) => action switch
    {
        AnnouncementAction.Schedule => AnnouncementState.Scheduled,
        AnnouncementAction.Unschedule => AnnouncementState.Draft,
        AnnouncementAction.PublishNow or AnnouncementAction.Republish => AnnouncementState.Published,
        AnnouncementAction.Unpublish => AnnouncementState.Unpublished,
        AnnouncementAction.Archive or AnnouncementAction.Expire => AnnouncementState.Archived,
        AnnouncementAction.Restore => a.StateBeforeArchive ?? AnnouncementState.Published,
        AnnouncementAction.Delete => IsNeverSent(a.State) ? null : AnnouncementState.Deleted,
        AnnouncementAction.RestoreDeleted => a.StateBeforeDelete ?? AnnouncementState.Published,
        AnnouncementAction.Purge => null,
        _ => a.State,
    };

    public static bool IsNeverSent(AnnouncementState state) =>
        state is AnnouncementState.Draft or AnnouncementState.Scheduled;

    private static string Describe(AnnouncementAction action) => action switch
    {
        AnnouncementAction.PublishNow => "publish",
        AnnouncementAction.RestoreDeleted => "restore",
        _ => action.ToString().ToLowerInvariant(),
    };
}
