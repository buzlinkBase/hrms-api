using Microsoft.AspNetCore.Components.Web;

namespace DTR.Core.DTR.Rules.Policies;

public class LeavePolicy : ConditionalPolicyBase
{
    public LeavePolicy(IRuleSpecification spec) : base(spec, SpecFailureBehavior.ReturnEmpty) { }

    protected override TimeRange ApplyIfSatisfied(TimeRange input, TimeContext context)
    {
        var applications = context.Payload.Data.CurrentLeaves;
        if (!applications.Any()) return TimeRange.Empty;

        var Trc = new List<TimeRange>();
        var metas = new List<LeaveMetaDataModel>();
        foreach (var application in applications)
        {
            if (application.PayType == PayType.WithoutPay) continue;
            var strategy = LeaveTimeRangeStrategyFactory.Create(application);
            var result = strategy.ComputeTimeRange(application, context);
            if (result.IsEmpty()) continue;

            // OneTime payout leaves are released as a lump sum during a specific payroll run
            // (see LeaveApplication.PayoutMode/ReleasePayrollDate), not per day — so this day
            // must not contribute paid hours to PaidLeaveHours (built from Trc below), but the
            // metadata entry still carries the FULL entitlement Hours (not zeroed) because
            // LeavesInfo.Hours is also what LeaveDtrReconciliationService sums to consume the
            // employee's leave-credit reservation — that consumption is real regardless of how
            // the pay is released. WorkTypeResolver independently flags the day as on-leave off
            // PayType, so attendance/reporting is unaffected either way.
            var isOneTime = application.PayoutMode == PayoutMode.OneTime;
            if (!isOneTime)
            {
                context.Payload.Ledger.RecordByTag("leave" + application.Id, context, result);
                Trc.Add(result);
            }

            metas.Add(new LeaveMetaDataModel
            {
                LeaveId = application.LeaveId,
                Hours = result.TotalMinutes.ToHour(),
                StartDateTime = result.TimeRecords.MinBy(x => x.StartTime)!.StartTime,
                EndDateTime = result.TimeRecords.MinBy(x => x.StartTime)!.EndTime,
                Name = application.Leave.Description,
                PayType=application.PayType,
            });
        }

        var finalResult = new TimeRecordCollection(
                Trc.SelectMany(x => x.TimeRecords.Select(x => x))).ToTimeRange();
        finalResult.SetMetaData("PaidLeave", metas); 
        return finalResult;
    }
}
 