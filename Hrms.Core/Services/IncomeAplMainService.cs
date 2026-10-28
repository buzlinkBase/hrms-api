using Hrms.Core.Policies.OtherIncome.Eligibility;
using Hrms.Core.Services.Schedules;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using System.Linq.Expressions;

namespace Hrms.Core.Services;

public class IncomeAplMainService : BaseService<OtherIncomeApplication>
{
    private readonly OtherIncomeService _otherIncomeService;
    private readonly EmployeeService _employeeService;
    private readonly IMapper _mapper;

    public IncomeAplMainService(IUnitOfWorkService uow,
           OtherIncomeService otherIncomeService,
           EmployeeService employeeService,
           IMapper mapper
        ) : base(uow)
    {
        _otherIncomeService = otherIncomeService;
        _employeeService = employeeService;
        _mapper = mapper;
    }

    private async Task<bool> ValidateAsync(OtherIncomeApplication model, CancellationToken token)
    {
        var otherIncome = await _otherIncomeService.FineOneAsync(model.IncomeId, token);
        Guard.ThrowIfNull(otherIncome, "Invalid Income Name");
        var employee = await _employeeService.FineOneAsync(model.EmployeeId, token);
        Guard.ThrowIfNull<Employee>(employee, "Invalid employee");
        model.IsTaxable = otherIncome.IsTaxable;
        ValidateRules(model);
        return true;
    }

    // The attendance rule has to make sense for the chosen pay mode.
    private static void ValidateRules(OtherIncomeApplication model)
    {
        Guard.EnsureTrue(model.EndDate >= model.StartDate, "End date can't be before the start date");
        if (model.PayMode == OtherIncomePayMode.FixedSchedule) return;

        Guard.EnsureTrue(model.AttendanceBasis != OtherIncomeAttendanceBasis.None,
            "Choose what counts as present for an attendance-based income");
        Guard.EnsureTrue(model.MinHours is null or (> 0 and <= 24), "Minimum hours must be between 0 and 24");
        Guard.EnsureTrue(model.MaxLateMinutes is null or >= 0, "Late limit can't be negative");
        Guard.EnsureTrue(model.MaxUndertimeMinutes is null or >= 0, "Undertime limit can't be negative");
        Guard.EnsureTrue((model.ApplicableDays & (int)OtherIncomeWeekdays.All) != 0, "Select at least one applicable day");
        Guard.EnsureTrue((model.DayTypes & (int)OtherIncomeDayTypes.All) != 0, "Select at least one day type");
        Guard.EnsureTrue(model.MinQualifyingDays is null or > 0, "Minimum qualifying days must be at least 1");
        if (model.PayMode == OtherIncomePayMode.PeriodicGated)
            Guard.EnsureTrue(model.FrequencyOfPayment is AllowanceFrequency.Weekly or AllowanceFrequency.SemiMonthly or AllowanceFrequency.Monthly,
                "A periodic attendance-gated income must be weekly, semi-monthly or monthly");
    }

    // Rows are only ever added through the Schedule navigation: the parent's Id is still
    // Guid.Empty here (EF assigns it on Add), so EF fills in OtherIncomeApplicationId itself.
    // This used to look up "existing" rows by that empty Id -- which matched every other
    // application's rows and deleted them all.
    public async Task<OtherIncomeApplication> AddAsync(CreateOtherIncomeApplication payload,
        CancellationToken token)
    {
        var model = _mapper.Map<OtherIncomeApplication>(payload);
        await ValidateAsync(model, token);
        var rows = (payload.Schedule ?? []).Select(x => new UpdateOtherIncomeSchedule
        {
            Date = x.Date,
            Amount = x.Amount,
            PeriodStart = x.PeriodStart,
            PeriodEnd = x.PeriodEnd,
        });
        OtherIncomeScheduleReconciler.Instance.Reconcile(model, model.Schedule, rows, _ => { });
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
        return model;
    }

    // Works on the tracked application and its live rows, so nothing the form doesn't send
    // (CreatedAt, ProcessBy, ...) is overwritten and no client-sent Id is ever INSERTed again.
    public async Task<OtherIncomeApplication> UpdateAsync(UpdateOtherIncomeApplication payload,
        CancellationToken token)
    {
        var model = await LoadTrackedAsync(payload.Id, token);
        Guard.ThrowIfNull(model, "Other income application not found");
        var reconciler = OtherIncomeScheduleReconciler.Instance;
        Guard.EnsureFalse(reconciler.AnyLocked(model!.Schedule) &&
                          (payload.EmployeeId != model.EmployeeId || payload.IncomeId != model.IncomeId),
            "The employee or income type can't be changed: part of this schedule was already paid in a saved payroll run.");

        _mapper.Map(payload, model);
        // A pre-migration row stores '' in these columns, which the converter reads as the default,
        // so EF saw "no change" when the form sent that default and never repaired the column.
        var entry = Context.Entry(model);
        entry.Property(x => x.PayMode).IsModified = true;
        entry.Property(x => x.AttendanceBasis).IsModified = true;
        await ValidateAsync(model, token);
        // A fixed schedule has no attendance rule, so a skip verdict left over from when it was
        // attendance-based is released first: the row is then unlocked and payroll can pay it.
        if (model.PayMode == OtherIncomePayMode.FixedSchedule)
            await ReleaseStaleSkipsAsync(model.Id, token);
        reconciler.Reconcile(model, model.Schedule, payload.Schedule ?? [], row => _uow.Repository.Remove(row));
        await CommitChangesAsync(token);
        return (await FineOneAsync(model.Id, token))!;
    }

    // Rows an unposted (draft) payroll run stamped as skipped. Posted runs keep their verdict.
    // Done in SQL, so the tracked rows are refreshed to match before the reconcile reads them.
    private async Task ReleaseStaleSkipsAsync(Guid applicationId, CancellationToken token)
    {
        var released = await Context.IgnoringConfidentiality<OtherIncomeSchedules>()
            .Where(x => x.OtherIncomeApplicationId == applicationId
                        && x.IsSkipped
                        && x.ConsumedByPayrollId != null
                        && !Context.Payrolls.IgnoreQueryFilters()
                            .Any(p => p.Id == x.ConsumedByPayrollId && p.IsPosted))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.ConsumedByPayrollId, (Guid?)null)
                .SetProperty(x => x.IsSkipped, false)
                .SetProperty(x => x.SkipReason, (string?)null), token);
        if (released == 0) return;

        foreach (var entry in Context.ChangeTracker.Entries<OtherIncomeSchedules>()
                     .Where(e => e.Entity.OtherIncomeApplicationId == applicationId).ToList())
            await entry.ReloadAsync(token);
    }

    private Task<OtherIncomeApplication?> LoadTrackedAsync(Guid id, CancellationToken token)
    {
        return _uow.Repository.FindAll<OtherIncomeApplication>()
            .Include(x => x.Schedule)
            .FirstOrDefaultAsync(x => x.Id == id, token);
    }

    public Task<List<OtherIncomeApplication>> FindAllAsync(CancellationToken token)
    {
        return GetQueryable().ToListAsync(token);
    }

    public Task<List<OtherIncomeSchedules>> FindDetail(Expression<Func<OtherIncomeSchedules,
        bool>> expression,
        CancellationToken token)
    {
        return _uow.Repository.Find(expression).ToListAsync(token);
    }

    public async Task<OtherIncomeApplication?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetQueryable()
            .Include(x => x.Schedule.OrderBy(s => s.Date))
            .FirstOrDefaultAsync(x => x.Id == Id, token);
    }

    // The rows go with the application -- payroll reads rows without looking at the parent, so a
    // live row under a deleted application would still be paid.
    public async Task DeleteParent(Guid Id, CancellationToken token)
    {
        var model = await LoadTrackedAsync(Id, token);
        if (model == null) return;
        Guard.EnsureFalse(OtherIncomeScheduleReconciler.Instance.AnyLocked(model.Schedule),
            "This application can't be deleted: part of its schedule was already paid in a saved payroll run.");
        foreach (var row in model.Schedule.ToList())
            _uow.Repository.Remove(row);
        _uow.Repository.Remove(model);
        await CommitChangesAsync(token);
    }
    public async Task DeleteChild(Guid Id, CancellationToken token)
    {
        var item = await _uow.Repository.FindOneAsync<OtherIncomeSchedules>(Id, token);
        if (item == null) return;
        OtherIncomeScheduleReconciler.Instance.EnsureNotLocked(item);
        _uow.Repository.Remove(item);
        await CommitChangesAsync(token);
    }
}

public class IncomeAplDtlService : BaseService<OtherIncomeSchedules>
{
    public IncomeAplDtlService(IUnitOfWorkService uow) : base(uow) { }

    // ConsumedByPayrollId == null is the real gatekeeper now — a row already stamped by a
    // saved payroll run never matches again, regardless of date range, closing the
    // double-application gap the plain date-range match used to allow.
    // IncomeClass is projected in SQL (a LEFT JOIN to Allowances) and the rows are materialized
    // before grouping: reading the lazy Income navigation inside a client-side selector fires a
    // second query while this one's reader is still open, which MySQL (no MARS) rejects with
    // "This MySqlConnection is already in use".
    public async Task<Dictionary<EmployeeKey, List<OtherIncomeInfo>>> LoadAsync(List<Guid> employeeIds, DateOnly fromDate, DateOnly toDate, CancellationToken token)
    {
        var rows = await GetQueryable()
            .AsNoTracking()
            .Where(x => x.Status == "Active"
                        && x.Date >= fromDate && x.Date <= toDate
                        && employeeIds.Contains(x.EmployeeId)
                        && x.ConsumedByPayrollId == null)
            .Select(x => new
            {
                x.Id,
                x.EmployeeId,
                x.Date,
                x.Amount,
                x.IncomeId,
                x.IsTaxable,
                IncomeClass = (IncomeClassType?)x.Income!.IncomeClass,
                IncomeName = x.Income!.Name,
            })
            .ToListAsync(token);

        // Attendance-mode rows (per qualifying day / periodic gate) are paid only if the DTR
        // qualifies them; the rest are left out here and stamped as skipped when the run is saved.
        var skipped = await OtherIncomeEligibilityEvaluator.FindSkippedAsync(
            Context, employeeIds, fromDate, toDate, token);
        if (skipped.Count > 0)
            rows = rows.Where(x => !skipped.ContainsKey(x.Id)).ToList();

        return rows
            .GroupBy(x => new EmployeeKey(x.EmployeeId))
            .ToDictionary(g => g.Key, g => g
                .Select(x => new OtherIncomeInfo
                {
                    Id = x.Id,
                    PayrollDate = x.Date,
                    Amount = x.Amount,
                    IncomeId = x.IncomeId,
                    Taxable = x.IsTaxable,
                    Type = x.IncomeClass,
                    Name = x.IncomeName,
                }).ToList());
    }

    // What one saved payroll paid in Other Income, per income type, for the payslip -- the rows
    // the save stamped with this payroll's Id (PayrollInputConsumptionService). Reads past the
    // confidential-employee filter (the caller already authorised this payroll) and the
    // soft-delete filter on the income type, so a type deleted since still prints its name.
    public async Task<List<PayslipIncomeLine>> GetPayslipLinesAsync(Guid payrollId, CancellationToken token)
    {
        var rows = await (
            from row in Context.IgnoringConfidentiality<OtherIncomeSchedules>()
            join income in Context.Allowances.IgnoreQueryFilters()
                on row.IncomeId equals income.Id into incomeJoin
            from income in incomeJoin.DefaultIfEmpty()
            where row.ConsumedByPayrollId == payrollId && !row.IsSkipped
            select new { row.IncomeId, Name = income != null ? income.Name : null, row.Amount })
            .AsNoTracking()
            .ToListAsync(token);

        return rows
            .GroupBy(x => x.IncomeId)
            .Select(g => new PayslipIncomeLine(
                string.IsNullOrWhiteSpace(g.First().Name) ? "Other Income" : g.First().Name!,
                g.Sum(x => x.Amount)))
            .OrderBy(x => x.Label)
            .ToList();
    }

    // Payroll Summary's per-type "Other Income" columns, for many payrolls in one query. Same
    // source and bypasses as GetPayslipLinesAsync, but only the IncomeClassType.Others types:
    // the other classes have their own Payroll Summary columns (Allowances, Bonuses, ...), and
    // the "Other Income" column is Payroll.TotalOtherIncome.
    public async Task<Dictionary<Guid, List<PayrollBreakdownItem>>> GetOtherIncomeItemsAsync(
        List<Guid> payrollIds, CancellationToken token)
    {
        if (payrollIds.Count == 0) return new();

        var rows = await (
            from row in Context.IgnoringConfidentiality<OtherIncomeSchedules>()
            join income in Context.Allowances.IgnoreQueryFilters()
                on row.IncomeId equals income.Id into incomeJoin
            from income in incomeJoin.DefaultIfEmpty()
            where row.ConsumedByPayrollId != null && payrollIds.Contains(row.ConsumedByPayrollId.Value)
                && !row.IsSkipped
                && (income == null || income.IncomeClass == IncomeClassType.Others)
            select new { PayrollId = row.ConsumedByPayrollId!.Value, row.IncomeId, Name = income != null ? income.Name : null, row.Amount })
            .AsNoTracking()
            .ToListAsync(token);

        return rows
            .GroupBy(x => x.PayrollId)
            .ToDictionary(g => g.Key, g => g
                .GroupBy(x => x.IncomeId)
                .Select(t => new PayrollBreakdownItem(
                    t.Key,
                    string.IsNullOrWhiteSpace(t.First().Name) ? "Other Income" : t.First().Name!,
                    t.Sum(x => x.Amount)))
                .ToList());
    }

    // Loads the specific rows HR confirmed in the Last Pay review step — re-checks
    // ConsumedByPayrollId == null so a row consumed by something else between the review
    // screen loading and Generate being clicked is silently skipped rather than re-applied.
    public async Task<List<OtherIncomeSchedules>> FindByIdsAsync(List<Guid> ids, CancellationToken token)
    {
        return await GetQueryable()
            .Where(x => ids.Contains(x.Id) && x.ConsumedByPayrollId == null)
            .ToListAsync(token);
    }

    // For Last Pay's review step — every not-yet-consumed schedule row for these employees,
    // regardless of date, so HR can see (and explicitly confirm) exactly what's pending before
    // any of it is applied. See LastPayrollService.GetAvailableOtherIncomeAsync.
    public async Task<List<OtherIncomeSchedules>> FindAvailableAsync(List<Guid> employeeIds, CancellationToken token)
    {
        return await GetQueryable()
            .AsNoTracking()
            .Where(x => x.Status == "Active"
                        && employeeIds.Contains(x.EmployeeId)
                        && x.ConsumedByPayrollId == null)
            .OrderBy(x => x.Date)
            .ToListAsync(token);
    }
}
