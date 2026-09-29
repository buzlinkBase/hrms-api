using Hrms.Core.Messaging.LeaveWorkers;
using Hrms.Domain.Entities;
using MassTransit;

namespace Hrms.Core.Services;

/// <summary>
/// The manual "grant this year's leave credits" action (Leave Setup). Two things it used to get
/// wrong: it always assumed a calendar fiscal year, ignoring the company's FiscalYearStartMonth;
/// and it published without saving, so with the bus outbox enabled the message was never
/// actually sent.
/// </summary>
public class LeavePeriodGrantTriggerService : BaseService<GeneralSetting>
{
    private readonly IPublishEndpoint _publisher;

    public LeavePeriodGrantTriggerService(IUnitOfWorkService uow, IPublishEndpoint publisher) : base(uow)
    {
        _publisher = publisher;
    }

    /// <returns>The fiscal year that was granted.</returns>
    public async Task<int> TriggerAsync(int? fiscalYear, CancellationToken token)
    {
        var startMonth = await FiscalYearSettings.GetStartMonthAsync(GetQueryable(), token);
        var year = fiscalYear ?? FiscalYearHelper.FiscalYearOf(DateOnly.FromDateTime(DateTime.UtcNow), startMonth);

        await _publisher.Publish(new RunLeavePeriodGrant(year, startMonth), token);
        await CommitChangesAsync(token);
        return year;
    }
}
