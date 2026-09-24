using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class NotificationPreferenceService : BaseService<NotificationPreference>
{
    public NotificationPreferenceService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public async Task<List<NotificationPreference>> GetForEmployeeAsync(Guid employeeId, CancellationToken token) =>
        await Uow.Repository.Find<NotificationPreference>(x => x.EmployeeId == employeeId)
            .AsNoTracking()
            .ToListAsync(token);

    public async Task UpsertAsync(
        Guid employeeId, ApprovalApplicationType applicationType, bool emailEnabled, bool pushEnabled, CancellationToken token)
    {
        var existing = await Uow.Repository
            .Find<NotificationPreference>(x => x.EmployeeId == employeeId && x.ApplicationType == applicationType)
            .FirstOrDefaultAsync(token);

        if (existing == null)
        {
            await Uow.Repository.AddAsync(new NotificationPreference
            {
                EmployeeId = employeeId,
                ApplicationType = applicationType,
                EmailEnabled = emailEnabled,
                PushEnabled = pushEnabled,
            }, token);
        }
        else
        {
            existing.EmailEnabled = emailEnabled;
            existing.PushEnabled = pushEnabled;
            Uow.Repository.Update(existing);
        }

        await CommitChangesAsync(token);
    }

    // No row = both channels on -- see NotificationPreference's own doc comment for why this is
    // an opt-out model rather than requiring a backfill row per employee/category.
    public async Task<(bool Email, bool Push)> ResolveDeliveryFlagsAsync(
        Guid employeeId, ApprovalApplicationType applicationType, CancellationToken token)
    {
        var preference = await Uow.Repository
            .Find<NotificationPreference>(x => x.EmployeeId == employeeId && x.ApplicationType == applicationType)
            .AsNoTracking()
            .FirstOrDefaultAsync(token);

        return preference == null ? (true, true) : (preference.EmailEnabled, preference.PushEnabled);
    }
}
