using Google.GenAI.Types;
using Hrms.Domain.Entities;

namespace DTR.Core;

public class CleanDTRDetailProcessor : IDTRProcessor<DTRDetailModel>
{

    public DTRDetailModel? Process(DTRProcessorPayload payload)
    {
        var calculator = RegularTimeCalculatorFactory.Create(payload);
        var cannonicalTimeRange = calculator.Calculate();
        return MapResultsToDailyRecord(payload, cannonicalTimeRange);
    }

    private DTRDetailModel MapResultsToDailyRecord(DTRProcessorPayload payload, TimeRange cannonicalTimeRange)
    {
        var context = new TimeContext
        {
            Payload = payload,
            CanonicalTimeRange = cannonicalTimeRange
        };

        var pipelineResult = new PipeLineResult
        {
            Regular = new NonHolidayDutyTimePipeline(context).Apply(cannonicalTimeRange),
            LegalHoliday = new HolidayDutyTimePipeline(context, HolidayType.LEGAL).Apply(cannonicalTimeRange),
            SpecialHoliday = new HolidayDutyTimePipeline(context, HolidayType.SPECIAL).Apply(cannonicalTimeRange),
            Travel = new TravelPipeline(context).Apply(cannonicalTimeRange),
            Leave = new LeaveTimePipeline(context).Apply(cannonicalTimeRange),
            Plus8 = new HolidayPlus8TimePipeline(context).Apply(cannonicalTimeRange),
            OT = new OverTimePipeline(context).Apply(cannonicalTimeRange),
            Late = new LateTimePipeline(context).Apply(cannonicalTimeRange),
            UT = new UndertimeTimePipeline(context).Apply(cannonicalTimeRange),
            Overbreak = new OverbreaktimePipeline(context).Apply(cannonicalTimeRange),
        };

        var workType = WorkTypeResolver.Resolve(context);
        var displayContext = new DisplayContext
        {
            TimeContext = context,
            PipeLineResult = pipelineResult
        };

        var evaluated = DTRDetailColumnDisplayProcessor.DisplayRule(displayContext);
        var nightDiffResult = DTRDetailColumnDisplayProcessor.ComputeNightDiff(evaluated, displayContext);

        return DailyRecordBuilder.Build(context, pipelineResult,
            evaluated,
            nightDiffResult,
            workType);
    }
}
public class DailyRecordBuilder
{
    public static DTRDetailModel Build(
        TimeContext context,
        PipeLineResult pipeline,
        EvaluatedColumnResult evaluated,
        NightDiffEvaluationResult NightDiff,
        WorkType workType)
    {
        var currentAtt = context.Payload.Data.CurrentAttendance.FirstOrDefault();
        var emp = context.Payload.Data.Employee;
        var actualAttRange = TimeRangeCalculator.GetTimeRange(context.Payload.Data.CurrentAttendance.Where(x => !x.IsVirtual).ToList());
        var unpaidLeaveRange = ProcessUnPaidLeave(context, pipeline);
        var leaveInfo = new List<LeaveMetaDataModel>();
        SetMetaInfo(leaveInfo, GetLeaveInfo(pipeline.Leave, "PaidLeave"));
        SetMetaInfo(leaveInfo, GetLeaveInfo(unpaidLeaveRange, "UnpaidLeave"));

        var dtr = new DTRDetailModel
        {
            ShiftWorkingHour = context.Payload.Data.CurrentShift.MaxWorkingMinutes / 60,
            HolCount = pipeline.Plus8.GetMetaData<int>("HolidayCount"),
            SPCount = pipeline.Plus8.GetMetaData<int>("SPHolidayCount"),
            FullName = emp.FullName(),
            EmployeeId = emp?.Id ?? Guid.NewGuid(),
            ShiftId = context.Payload.Data.CurrentShift.Id,

            WorkDate = context.Payload.Data.CurrentDate,
            ShiftName = context.Payload.Data.CurrentShift.ShiftName,
            ShiftStartTime = context.Payload.Data.CurrentShift.StartTime,
            ShiftEndTime = context.Payload.Data.CurrentShift.EndTime,
            StartTime = actualAttRange.TimeRecords.MinBy(x => x.StartTime)?.StartTime,
            EndTime = actualAttRange.TimeRecords.MaxBy(x => x.EndTime)?.EndTime,
            WorkType = StringHelpers.AddSpacesBeforeCaps(workType.ToString()).Trim(),
            WorkTypeEnum = workType,

            LateMinutes = pipeline.Late.TotalMinutes,
            UTMinutes = pipeline.UT.TotalMinutes,
            OverMinutes = pipeline.Overbreak.TotalMinutes,
            LateForOTMinutes = 0,
            OBHours = pipeline.Travel.TotalMinutes.ToHour(),

            PaidLeaveHours = pipeline.Leave.TotalMinutes.ToHour(),
            UnpaidLeaveHours = unpaidLeaveRange.TotalMinutes.ToHour(),
            LeavesInfo = leaveInfo,
            AbsentCount = workType == WorkType.Absent ? 1 : 0,

            RegularNetHours = (evaluated.RegWork.TotalMinutes - NightDiff.Regular.TotalMinutes).ToHour(),
            RegularOTHours = (evaluated.RegOT.TotalMinutes - NightDiff.RegOT.TotalMinutes).ToHour(),
            RegularNDHours = NightDiff.Regular.TotalMinutes.ToHour(),
            RegularNDOTHours = NightDiff.RegOT.TotalMinutes.ToHour(),

            RestDayHours = (evaluated.RestWork.TotalMinutes - NightDiff.Rest.TotalMinutes).ToHour(),
            RestDayOTHours = (evaluated.RestOT.TotalMinutes - NightDiff.RestOT.TotalMinutes).ToHour(),
            RestDayNDHours = NightDiff.Rest.TotalMinutes.ToHour(),
            RestDayNDOTHours = NightDiff.RestOT.TotalMinutes.ToHour(),

            LegalHolHours = (evaluated.LegalHoliday.TotalMinutes - NightDiff.Legal.TotalMinutes).ToHour(),
            LegalHolOTHours = (evaluated.LegalOT.TotalMinutes - NightDiff.LegalOT.TotalMinutes).ToHour(),
            LegalHolNightDiffHours = NightDiff.Legal.TotalMinutes.ToHour(),
            LegalHolNightDiffOTHours = NightDiff.LegalOT.TotalMinutes.ToHour(),

            SpecialHolHours = (evaluated.SPHoliday.TotalMinutes - NightDiff.Special.TotalMinutes).ToHour(),
            SpecialHolOTHours = (evaluated.SpecialOT.TotalMinutes - NightDiff.SpecialOT.TotalMinutes).ToHour(),
            SpecialHolNightDiffHours = NightDiff.Special.TotalMinutes.ToHour(),
            SpecialHolNightDiffOTHours = NightDiff.SpecialOT.TotalMinutes.ToHour(),

            RestLegalDayHours = (evaluated.RestLegal.TotalMinutes - NightDiff.RestLegal.TotalMinutes).ToHour(),
            RestLegalDayOTHours = (evaluated.RestLegalOT.TotalMinutes - NightDiff.RestLegalOT.TotalMinutes).ToHour(),
            RestLegalDayNDHours = NightDiff.RestLegal.TotalMinutes.ToHour(),
            RestLegalDayNDOTHours = NightDiff.RestLegalOT.TotalMinutes.ToHour(),

            RestSpecialDayHours = (evaluated.RestSpecial.TotalMinutes - NightDiff.RestSpecial.TotalMinutes).ToHour(),
            RestSpecialDayOTHours = (evaluated.RestSpecialOT.TotalMinutes - NightDiff.RestSpecialOT.TotalMinutes).ToHour(),
            RestSpecialDayNDHours = NightDiff.RestSpecial.TotalMinutes.ToHour(),
            RestSpecialDayNDOTHours = NightDiff.RestSpecialOT.TotalMinutes.ToHour(),

            DoubleLegalHours = (evaluated.DoubleLegalHoliday.TotalMinutes - NightDiff.DoubleLegalHoliday.TotalMinutes).ToHour(),
            DoubleLegalOTHours = (evaluated.DoubleLegalHolidayOT.TotalMinutes - NightDiff.DoubleLegalHolidayOT.TotalMinutes).ToHour(),
            DoubleLegalNDHours = NightDiff.DoubleLegalHoliday.TotalMinutes.ToHour(),
            DoubleLegalNDOTHours = NightDiff.DoubleLegalHolidayOT.TotalMinutes.ToHour(),

            RestDoubleLegalHours = (evaluated.RestDoubleLegal.TotalMinutes - NightDiff.RestDoubleLegal.TotalMinutes).ToHour(),
            RestDoubleLegalOTHours = (evaluated.RestDoubleLegalOT.TotalMinutes - NightDiff.RestDoubleLegalOT.TotalMinutes).ToHour(),
            RestDoubleLegalNDHours = NightDiff.RestDoubleLegal.TotalMinutes.ToHour(),
            RestDoubleLegalNDOTHours = NightDiff.RestDoubleLegalOT.TotalMinutes.ToHour(),

            ClientId = currentAtt?.ClientId ?? emp?.ClientId,
            DepartmentId = currentAtt?.DepartmentId ?? emp?.DepartmentId,
            PayrollGroupId = emp?.PayrollGroupId,
            AreaId = currentAtt?.OperationAreaId ?? emp?.AreaId,
            BranchId = currentAtt?.BranchId ?? emp?.BranchId,
        };

        return dtr;
    }

    private static TimeRange ProcessUnPaidLeave(TimeContext context, PipeLineResult pipeline)
    {
        var leaves = context.Payload.Provider.LeaveProvider
            .GetApplications(context.Payload.Data.CurrentDate);

        var currentLeaves = leaves
            .Where(x => x.PayType == PayType.WithoutPay)
            .ToList();

        var shift = context.Payload.Data.CurrentShift;
        var Trc = new List<TimeRange>();
        var metas = new List<LeaveMetaDataModel>();
        foreach (var application in currentLeaves)
        {
            var attendances = new List<Attendance>();
            attendances.AddRange(VirtualTimeComposer.SetLeaveAttendance(application, context.Payload.Data.Employee, shift, false));
            var result = TimeRangeSetter.SetTimeRangeCollection(attendances);
            var ledger = context.Payload.Ledger.GetByStartWithTag("leave", context);
            var clean = result
                .Exclude(pipeline.Leave.TimeRecords)
                .Exclude(ledger)
                .CapAndCrop(shift);
            if (clean.IsEmpty()) continue;
            context.Payload.Ledger.RecordByTag("leave" + application.Id, context, clean);
            Trc.Add(clean);
            metas.Add(new LeaveMetaDataModel
            {
                LeaveId = application.LeaveId,
                Hours = clean.TotalMinutes.ToHour(),
                StartDateTime = clean.TimeRecords.MinBy(x => x.StartTime)!.StartTime,
                EndDateTime = clean.TimeRecords.MinBy(x => x.StartTime)!.EndTime,
                Name = application.Leave.Description,
                PayType = application.PayType,
            });
        }
        var finalResult = new TimeRecordCollection(
                Trc.SelectMany(x => x.TimeRecords.Select(x => x))).ToTimeRange();
        finalResult.SetMetaData("UnpaidLeave", metas);
        return finalResult;
    }

    private static List<LeaveMetaDataModel>? GetLeaveInfo (TimeRange timeRange,string tag)
    {
       return timeRange.GetMetaData<List<LeaveMetaDataModel>>(tag);
    }

    private static void SetMetaInfo(List<LeaveMetaDataModel> meta, List<LeaveMetaDataModel>? info)
    {
        if (info == null || !info.Any()) return;
        meta.AddRange(info);
    }
}