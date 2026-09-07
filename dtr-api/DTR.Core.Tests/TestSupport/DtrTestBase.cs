using Hrms.Domain.Entities;

namespace DTR.Core.Tests.TestSupport;

/// <summary>
/// Shared builder for a TimeContext/DisplayContext built entirely from plain POCOs — no DB,
/// no DI container, matching hrms.test's own TestContextBase convention. Provider.UTProvider
/// is always wired to an empty UnderTimeServiceProvider — AppliedUnderTimeHandler
/// (RegularHourPolicy) reads it unconditionally, so leaving it null NREs even for tests that
/// have nothing to do with undertime. CurrentShift.MaxWorkingMinutes defaults to the shift's
/// own span (shiftEnd - shiftStart) unless overridden, so a plain CreateContext call already
/// gives a "clean passthrough" shift — RegularHourPolicy.CapAndCrop only truncates when
/// MaxWorkingMinutes is smaller than the actual attendance span.
/// </summary>
public abstract class DtrTestBase
{
    protected static Guid NewEmployeeId() => Guid.NewGuid();

    /// <summary>
    /// Builds a TimeContext for a single shift. `holidays` seeds the fake HolidayProvider —
    /// keyed by (EmployeeId, PayrollDate), exactly like the real DefaultHolidayProvider reads
    /// it (see HolidayProvider.cs) — no database involved.
    /// </summary>
    protected static TimeContext CreateContext(
        DateTime shiftStart,
        DateTime shiftEnd,
        HolidayTimeBasis holidayTimeBasis = HolidayTimeBasis.BasedOnTimeInDayType,
        Dictionary<Holidaykey, List<HolidayInfo>>? holidays = null,
        Guid? employeeId = null,
        Guid? areaId = null,
        double? maxWorkingMinutes = null,
        double gracePeriodMinutes = 0)
    {
        var empId = employeeId ?? NewEmployeeId();
        var employee = new EmployeeDTRRun { Id = empId, AreaId = areaId };
        var shift = new CurrentShift
        {
            StartTime = shiftStart,
            EndTime = shiftEnd,
            ShiftDate = DateOnly.FromDateTime(shiftStart),
            ShiftType = TimeShiftType.FIXED,
            LunchBreakOption = BreakMode.NONE,
            WithAMBreak = BreakMode.NONE,
            WithPMBreakTime = BreakMode.NONE,
            MaxWorkingMinutes = maxWorkingMinutes ?? (shiftEnd - shiftStart).TotalMinutes,
            GracePeriodMinutes = gracePeriodMinutes,
        };
        var holidayProvider = HolidayProviderFactory.Create(
            employee, holidays ?? new Dictionary<Holidaykey, List<HolidayInfo>>());
        var companyPolicy = new CompanyPolicyRule { HolidayTimeBasis = holidayTimeBasis };

        // WorkTypeResolver.Resolve reads Provider.AttendanceProvider.CurrentShiftAttendance()
        // unconditionally as its first line — wire a real CurrentShiftProvider/AttendanceProvider
        // pair (same construction as CurrentDayDTRPayload.SetPayload) so it never NREs. A
        // same-time-next-day placeholder shift is seeded too: AttendanceProvider.GetAttByShift
        // needs BOTH the current shift AND a resolvable "next shift" to compute its attendance
        // window ([currentShift.Start, nextShift.Start)) — with only today's shift seeded,
        // GetNextShift always misses and CurrentShiftAttendance silently returns empty.
        var nextDayShift = shift.Clone();
        nextDayShift.StartTime = shiftStart.AddDays(1);
        nextDayShift.EndTime = shiftEnd.AddDays(1);
        nextDayShift.ShiftDate = shift.ShiftDate.AddDays(1);
        var contextModel = new DTRContextModel
        {
            AllShifts = new Dictionary<CurrentTimeShiftKey, CurrentShift>
            {
                [new CurrentTimeShiftKey(empId, shift.ShiftDate)] = shift,
                [new CurrentTimeShiftKey(empId, nextDayShift.ShiftDate)] = nextDayShift,
            },
            CleanAttendance = new Dictionary<AttendanceEmpId, List<Attendance>>(),
            CompanyPolicy = companyPolicy,
        };
        var shiftProvider = ShiftProviderFactory.Create(contextModel, employee, shift.ShiftDate);
        var attendanceProvider = new AttendanceProvider(new GetCurrentAttendancePayload(contextModel, employee), shiftProvider);

        var payload = new DTRProcessorPayload
        {
            Data = new DataPayload
            {
                Employee = employee,
                CurrentShift = shift,
                CurrentDate = DateOnly.FromDateTime(shiftStart),
                CompanyPolicy = companyPolicy,
                CurrentAttendance = new List<Attendance>(),
                CurrentDayoffs = new Dictionary<ResDaykey, CurrentRestDay>(),
                CurrentLeaves = new List<LeaveApplication>(),
            },
            Provider = new ProvidersPayload
            {
                HolidayProvider = holidayProvider,
                UTProvider = new UnderTimeServiceProvider(new Dictionary<UTKey, UnderTimeApplication?>(), employee),
                OTProvider = new OverTimeServiceProvider(new Dictionary<OTKey, OverTimeApplication?>(), employee),
                LeaveProvider = new LeaveApplicationProvider(new Dictionary<Leavekey, List<LeaveApplication>>(), employee),
                ClientPolicyProvider = new ClientPolicyProvider(new Dictionary<ClientPolicyKey, ClientPolicyRule>()),
                AttendanceProvider = attendanceProvider,
                CurrentShiftProvider = shiftProvider,
                DtrContextModel = contextModel,
            },
            ContextModel = contextModel,
        };

        var canonicalRange = TimeRange.Set((shiftEnd - shiftStart).TotalMinutes, shiftStart, shiftEnd);

        return new TimeContext
        {
            Payload = payload,
            CanonicalTimeRange = canonicalRange,
        };
    }

    // Seeds raw punch times into the same DTRContextModel.CleanAttendance dictionary the
    // context's AttendanceProvider reads from — mirrors production punch data closely enough
    // for AttendanceProvider.CurrentShiftAttendance()/WorkTypeResolver's hasAttendance checks.
    protected static void ApplyAttendance(TimeContext context, params DateTime[] punchTimes)
    {
        var empId = context.Payload.Data.Employee.Id;
        var attendances = punchTimes
            .Select(t => new Attendance { EmployeeId = empId, WorkDateTime = t })
            .ToList();
        context.Payload.ContextModel.CleanAttendance[new AttendanceEmpId(empId)] = attendances;
        context.Payload.Data.CurrentAttendance = attendances;
    }

    // AppliedUnderTimeHandler (inside RegularHourPolicy) reads Provider.UTProvider.HasUTApplication
    // — seed a matching UnderTimeApplication to simulate a manually-filed undertime for this
    // employee/date, so RegularHourPolicy borrows exactly `utMinutes` off the max working time.
    protected static void ApplyUnderTime(TimeContext context, double utMinutes)
    {
        var shift = context.Payload.Data.CurrentShift;
        var key = new UTKey(context.Payload.Data.Employee.Id, shift.ShiftDate);
        context.Payload.Provider.UTProvider = new UnderTimeServiceProvider(
            new Dictionary<UTKey, UnderTimeApplication?>
            {
                [key] = new UnderTimeApplication { EmployeeId = context.Payload.Data.Employee.Id, PayrollDate = shift.ShiftDate, UTMinutes = utMinutes },
            },
            context.Payload.Data.Employee);
    }

    // OT policies/handlers read Provider.OTProvider.GetOT/HasOTApplication unconditionally —
    // seed a matching OverTimeApplication to simulate a manually-filed or approved OT request.
    protected static void ApplyOvertime(TimeContext context, OverTimeApplication application)
    {
        var shift = context.Payload.Data.CurrentShift;
        var key = new OTKey(context.Payload.Data.Employee.Id, shift.ShiftDate);
        context.Payload.Provider.OTProvider = new OverTimeServiceProvider(
            new Dictionary<OTKey, OverTimeApplication?> { [key] = application },
            context.Payload.Data.Employee);
    }

    // Wires a leave application into BOTH places the Leave subsystem reads it from:
    // Data.CurrentLeaves (LeavePolicy/WorkTypeResolver read this list directly) and
    // Provider.LeaveProvider (IsLeaveWithPay calls GetApplications(date) instead — a separate,
    // independently-seeded provider in production too, not derived from CurrentLeaves).
    protected static void ApplyLeave(TimeContext context, params LeaveApplication[] applications)
    {
        var empId = context.Payload.Data.Employee.Id;
        context.Payload.Data.CurrentLeaves = applications.ToList();
        context.Payload.Provider.LeaveProvider = new LeaveApplicationProvider(
            new Dictionary<Leavekey, List<LeaveApplication>> { [new Leavekey(empId)] = applications.ToList() },
            context.Payload.Data.Employee);
    }

    protected static LeaveApplication BuildLeaveApplication(
        DurationType durationType,
        PayType payType = PayType.WithPay,
        PaySource paySource = PaySource.Company,
        PayoutMode payoutMode = PayoutMode.PerDay,
        DayFraction dayFraction = DayFraction.FullDay,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        bool isManualEntry = false,
        double totalMinutes = 0,
        bool allowHalfDay = true) =>
        new LeaveApplication
        {
            Id = Guid.NewGuid(),
            LeaveId = Guid.NewGuid(),
            Leave = new Leave { Description = "Test Leave", PaySource = paySource, AllowHalfDay = allowHalfDay },
            DurationType = durationType,
            DayFraction = dayFraction,
            PayType = payType,
            PayoutMode = payoutMode,
            LeaveDateFrom = fromDate ?? default,
            LeaveDateTo = toDate ?? fromDate ?? default,
            StartTime = startTime,
            EndTime = endTime,
            IsManualEntry = isManualEntry,
            TotalMinutes = totalMinutes,
        };

    protected static DisplayContext CreateDisplayContext(TimeContext context, PipeLineResult? pipelineResult = null) =>
        new DisplayContext
        {
            TimeContext = context,
            PipeLineResult = pipelineResult ?? new PipeLineResult(),
        };

    protected static HolidayInfo Holiday(
        HolidayType type,
        DateOnly payrollDate,
        HolidayWorkType workType = HolidayWorkType.NonWorking,
        Guid? areaId = null,
        Guid? employeeId = null,
        bool isPaid = true) =>
        new HolidayInfo
        {
            HolidayId = Guid.NewGuid(),
            HolType = type,
            PayrollDate = payrollDate,
            WorkType = workType,
            AreaId = areaId,
            EmployeeId = employeeId ?? Guid.Empty,
            IsPaid = isPaid,
        };

    protected static Dictionary<Holidaykey, List<HolidayInfo>> Holidays(Guid employeeId, params HolidayInfo[] infos)
    {
        var dict = new Dictionary<Holidaykey, List<HolidayInfo>>();
        foreach (var info in infos)
        {
            var key = new Holidaykey(employeeId, info.PayrollDate);
            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<HolidayInfo>();
                dict[key] = list;
            }
            list.Add(info);
        }
        return dict;
    }

    protected static TimeRange Range(DateTime start, DateTime end) =>
        TimeRange.Set((end - start).TotalMinutes, start, end);

    // RestDayChecker.IsRestDay reads CurrentDayoffs keyed by (EmployeeId, CurrentDate) —
    // marks today as a rest day for whatever employee/date the context is already set to.
    protected static void MarkAsRestDay(TimeContext context)
    {
        var key = new ResDaykey(context.Payload.Data.Employee.Id, context.Payload.Data.CurrentDate);
        context.Payload.Data.CurrentDayoffs[key] = new CurrentRestDay
        {
            Employee = context.Payload.Data.Employee,
            PayrollDate = context.Payload.Data.CurrentDate,
        };
    }
}
