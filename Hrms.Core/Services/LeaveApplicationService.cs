using Hrms.Core.Messaging.LeaveWorkers;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Mapster;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Hrms.Core.Services;

public class LeaveApplicationService : BaseService<LeaveApplication>
{
    private readonly TypeAdapterConfig _config;
    private readonly IMapper _mapper;
    private readonly IPublishEndpoint _publisher;
    private readonly ILogger<LeaveApplicationService> _logger;

    public LeaveApplicationService(IUnitOfWorkService uow,
        TypeAdapterConfig config,
        IMapper mapper,
        IPublishEndpoint publisher,
        ILogger<LeaveApplicationService> logger) : base(uow)
    {
        _config    = config;
        _mapper    = mapper;
        _publisher = publisher;
        _logger    = logger;
    }

    protected override Task<EvaluationResult> CreateValidatorAsync(LeaveApplication model, CancellationToken token = default)
    {
        Guard.ThrowIfNull(model, nameof(LeaveApplication));
        return base.CreateValidatorAsync(model, token);
    }

    public async Task<LeaveApplicationModel?> AddAsync(CreateLeaveApplication payload, CancellationToken token)
    {
        await EnsurePolicyAsync(payload, token);
        var model = _mapper.Map<LeaveApplication>(payload);
        if (model == null) return null;
        await CreateAsync(model, token);

        // A leave application can be filed directly in a non-default status (e.g. backfilling
        // an already-approved leave) — apply the same credit/DTR side effects that would fire
        // had it been created ForApproval and then transitioned via UpdateAsync.
        await ApplyStatusTransitionSideEffectsAsync(model, ApprovalStatus.ForApproval, token);

        await CommitChangesAsync(token);

        await _publisher.Publish(new LeaveApplicationCreated(model.Id, model.EmployeeId, model.LeaveId), token);

        return _mapper.Map<LeaveApplicationModel>(model);
    }

    private async Task EnsurePolicyAsync(CreateLeaveApplication payload, CancellationToken token)
    {
        var leave = await _uow.Repository
            .Find<Leave>(x => x.Id == payload.LeaveId)
            .AsNoTracking()
            .FirstOrDefaultAsync(token)
            ?? throw new NotFoundException("Leave type not found");

        if (leave.GenderRestriction != GenderRestriction.None)
        {
            var employee = await _uow.Repository
                .Find<Employee>(x => x.Id == payload.EmployeeId)
                .AsNoTracking()
                .FirstOrDefaultAsync(token)
                ?? throw new NotFoundException("Employee not found");

            var requiredGender = leave.GenderRestriction == GenderRestriction.MaleOnly ? "Male" : "Female";
            if (!string.Equals(employee.Gender, requiredGender, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"\"{leave.Description}\" is restricted to {requiredGender.ToLower()} employees only.");
        }

        if (!leave.AllowHalfDay &&
            (payload.DayFraction == DayFraction.AM || payload.DayFraction == DayFraction.PM))
            throw new InvalidOperationException(
                $"Half-day filing is not allowed for \"{leave.Description}\".");

        if (!leave.AllowPartial && payload.DurationType == DurationType.Partial)
            throw new InvalidOperationException(
                $"Time-based (partial) filing is not allowed for \"{leave.Description}\".");

        if (leave.MaxConsecutiveDays.HasValue)
        {
            var span = payload.LeaveDateTo.DayNumber - payload.LeaveDateFrom.DayNumber + 1;
            if (span > leave.MaxConsecutiveDays.Value)
                throw new InvalidOperationException(
                    $"\"{leave.Description}\" allows a maximum of {leave.MaxConsecutiveDays} consecutive days per application.");
        }

        var newDays = ComputeDays(payload.DurationType, payload.LeaveDateFrom, payload.LeaveDateTo, payload.DayFraction);

        if (leave.MaxDaysPerYear.HasValue)
        {
            var yearStart = new DateOnly(payload.LeaveDateFrom.Year, 1, 1);
            var yearEnd   = new DateOnly(payload.LeaveDateFrom.Year, 12, 31);

            var existing = await _uow.Repository
                .Find<LeaveApplication>(x =>
                    x.EmployeeId      == payload.EmployeeId &&
                    x.LeaveId         == payload.LeaveId &&
                    x.ApprovalStatus  == ApprovalStatus.Approved &&
                    x.LeaveDateFrom   >= yearStart &&
                    x.LeaveDateTo     <= yearEnd)
                .AsNoTracking()
                .ToListAsync(token);

            var usedDays = existing.Sum(x =>
                ComputeDays(x.DurationType, x.LeaveDateFrom, x.LeaveDateTo, x.DayFraction));

            if (usedDays + newDays > leave.MaxDaysPerYear.Value)
                throw new InvalidOperationException(
                    $"This application would exceed the annual cap of {leave.MaxDaysPerYear} day(s) for \"{leave.Description}\". " +
                    $"Used: {usedDays:0.##}, Applying: {newDays:0.##}.");
        }

        // Credits balance check — only for paid applications on leave types that disallow negative balance
        if (!leave.AllowNegativeBalance && payload.PayType != PayType.WithoutPay)
        {
            var credits = await _uow.Repository
                .Find<LeaveCredits>(x =>
                    x.EmployeeId == payload.EmployeeId &&
                    x.LeaveId    == payload.LeaveId    &&
                    x.PeriodYear == payload.LeaveDateFrom.Year)
                .AsNoTracking()
                .FirstOrDefaultAsync(token);

            if (credits != null)
            {
                if ((double)credits.AvailableToFile < newDays)
                    throw new InvalidOperationException(
                        $"Insufficient leave credits for \"{leave.Description}\". " +
                        $"Available to file: {credits.AvailableToFile:0.##} day(s)" +
                        $" (Balance: {credits.Balance:0.##}, Reserved: {credits.Reserved:0.##})," +
                        $" Applying: {newDays:0.##} day(s).");
            }
        }
    }

    private static double ComputeDays(
        DurationType durationType,
        DateOnly from,
        DateOnly to,
        DayFraction fraction) =>
        durationType == DurationType.MultiDay
            ? (double)(to.DayNumber - from.DayNumber + 1)
            : fraction == DayFraction.FullDay ? 1.0 : 0.5;

    public async Task<LeaveApplicationModel?> AddAsync(LeaveApplication model,
        CancellationToken token)
    {
        await CreateAsync(model, token);
        return _mapper.Map<LeaveApplicationModel>(model);
    }

    public async Task UpdateAsync(UpdateLeaveApplication payload, CancellationToken token)
    {
        var existing = await Context.leaveApplications
            .Include(x => x.Leave)
            .FirstOrDefaultAsync(x => x.Id == payload.Id, token)
            ?? throw new NotFoundException("Record not found");

        var previousStatus = existing.ApprovalStatus;
        payload.Adapt(existing);

        await ApplyStatusTransitionSideEffectsAsync(existing, previousStatus, token);

        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    private async Task ApplyStatusTransitionSideEffectsAsync(
        LeaveApplication existing, ApprovalStatus previousStatus, CancellationToken token)
    {
        var statusChanged = existing.ApprovalStatus != previousStatus;

        if (existing.ApprovalStatus == ApprovalStatus.Approved &&
            previousStatus != ApprovalStatus.Approved)
        {
            await DeductCreditsAsync(existing, token);
        }
        else if (previousStatus == ApprovalStatus.Approved &&
                 existing.ApprovalStatus is ApprovalStatus.Cancelled or ApprovalStatus.Declined)
        {
            await RestoreCreditsAsync(existing, token);
        }

        // Any status change that affects leave hours (approve, cancel, decline) invalidates
        // any DTR rows in the leave's date range that were already posted, so they get
        // re-run and reflect the updated leave state.
        if (statusChanged && existing.ApprovalStatus != ApprovalStatus.ForApproval)
            await InvalidatePostedDtrAsync(existing, token);
    }

    private async Task InvalidatePostedDtrAsync(LeaveApplication app, CancellationToken token)
    {
        var records = await _uow.Repository
            .Find<DailyRecord>(x =>
                x.EmployeeId == app.EmployeeId &&
                x.WorkDate   >= app.LeaveDateFrom &&
                x.WorkDate   <= app.LeaveDateTo   &&
                x.Posted)
            .ToListAsync(token);

        if (records.Count == 0) return;

        foreach (var r in records)
        {
            r.Posted         = false;
            r.PaidLeaveHours = 0;
        }

        _logger.LogInformation(
            "Leave status → {Status}: unposted {Count} DTR record(s) for employee {EmployeeId} ({From}–{To})",
            app.ApprovalStatus, records.Count, app.EmployeeId, app.LeaveDateFrom, app.LeaveDateTo);
    }

    // Phase 1: soft-reserve the estimated days on approval.
    // The authoritative Deduction only happens in Phase 2 when the DTR batch is posted
    // and actual PaidLeaveHours is confirmed by the DTR engine.
    private async Task DeductCreditsAsync(LeaveApplication app, CancellationToken token)
    {
        var leave = app.Leave
            ?? await _uow.Repository.Find<Leave>(x => x.Id == app.LeaveId).FirstOrDefaultAsync(token);
        if (leave == null) return;

        if (app.PayType == PayType.WithoutPay) return;
        if (leave.PaySource == PaySource.Government) return;

        var days = ComputeDays(app.DurationType, app.LeaveDateFrom, app.LeaveDateTo, app.DayFraction);

        var credits = await _uow.Repository
            .Find<LeaveCredits>(x =>
                x.EmployeeId == app.EmployeeId &&
                x.LeaveId    == app.LeaveId    &&
                x.PeriodYear == app.LeaveDateFrom.Year)
            .FirstOrDefaultAsync(token);

        if (credits == null) return;

        if (!leave.AllowNegativeBalance && (double)credits.AvailableToFile < days)
            throw new InvalidOperationException(
                $"Cannot approve: insufficient leave credits for \"{leave.Description}\". " +
                $"Available to file: {credits.AvailableToFile:0.##} day(s)" +
                $" (Balance: {credits.Balance:0.##}, Reserved: {credits.Reserved:0.##})," +
                $" Required: {days:0.##} day(s).");

        credits.Reserved += (decimal)days;

        await Context.Set<LeaveLedger>().AddAsync(new LeaveLedger
        {
            EmployeeId             = app.EmployeeId,
            LeaveId                = app.LeaveId,
            EntryType              = LedgerEntryType.Reserved,
            EntryDate              = DateOnly.FromDateTime(DateTime.UtcNow),
            Less                   = (decimal)days,
            Add                    = 0m,
            Balance                = credits.Balance,
            Particulars            = $"Leave approved — reserved {days:0.##} day(s) pending DTR confirmation ({app.LeaveDateFrom:MMM dd}–{app.LeaveDateTo:MMM dd, yyyy})",
            LeaveCreditsId         = credits.Id,
            ReferenceApplicationId = app.Id,
        }, token);
    }

    private async Task RestoreCreditsAsync(LeaveApplication app, CancellationToken token)
    {
        var leave = app.Leave
            ?? await _uow.Repository.Find<Leave>(x => x.Id == app.LeaveId).FirstOrDefaultAsync(token);
        if (leave == null) return;

        if (app.PayType == PayType.WithoutPay) return;
        if (leave.PaySource == PaySource.Government) return;

        var today       = DateTime.UtcNow;
        var statusLabel = app.ApprovalStatus == ApprovalStatus.Cancelled ? "Cancelled" : "Declined";

        var credits = await _uow.Repository
            .Find<LeaveCredits>(x =>
                x.EmployeeId == app.EmployeeId &&
                x.LeaveId    == app.LeaveId    &&
                x.PeriodYear == app.LeaveDateFrom.Year)
            .FirstOrDefaultAsync(token);

        if (credits == null) return;

        // Determine which phase we're in by inspecting the ledger for this application:
        // net reserved > 0  → Phase 1 (DTR not yet posted) — just release the soft hold
        // net reserved == 0 → Phase 2 (DTR already posted)  — reverse the actual deduction
        var reservedSum = await _uow.Repository
            .Find<LeaveLedger>(x =>
                x.ReferenceApplicationId == app.Id &&
                x.EntryType              == LedgerEntryType.Reserved)
            .SumAsync(x => x.Less, token);

        var releasedSum = await _uow.Repository
            .Find<LeaveLedger>(x =>
                x.ReferenceApplicationId == app.Id &&
                x.EntryType              == LedgerEntryType.Released)
            .SumAsync(x => x.Add, token);

        var netReserved = reservedSum - releasedSum;

        if (netReserved > 0)
        {
            // Phase 1: reservation still active — release the soft hold
            credits.Reserved -= netReserved;

            await Context.Set<LeaveLedger>().AddAsync(new LeaveLedger
            {
                EmployeeId             = app.EmployeeId,
                LeaveId                = app.LeaveId,
                EntryType              = LedgerEntryType.Released,
                EntryDate              = DateOnly.FromDateTime(today),
                Add                    = netReserved,
                Less                   = 0m,
                Balance                = credits.Balance,
                Particulars            = $"Reservation released — leave {statusLabel}",
                LeaveCreditsId         = credits.Id,
                ReferenceApplicationId = app.Id,
            }, token);

            _logger.LogInformation(
                "Leave {Status}: released {Days} day(s) reservation for employee {EmployeeId}",
                statusLabel, netReserved, app.EmployeeId);
        }
        else
        {
            // Phase 2: DTR already posted — reverse the confirmed deduction.
            // Guard against double-reversal (e.g., unpost already reversed it first).
            var alreadyReversed = await _uow.Repository
                .Find<LeaveLedger>(x =>
                    x.ReferenceApplicationId == app.Id &&
                    x.EntryType              == LedgerEntryType.Reversal)
                .AnyAsync(token);

            if (alreadyReversed) return;

            var deductedAmount = await _uow.Repository
                .Find<LeaveLedger>(x =>
                    x.ReferenceApplicationId == app.Id &&
                    x.EntryType              == LedgerEntryType.Deduction)
                .SumAsync(x => x.Less, token);

            if (deductedAmount <= 0) return;

            credits.Used    -= deductedAmount;
            credits.Balance += deductedAmount;

            await Context.Set<LeaveLedger>().AddAsync(new LeaveLedger
            {
                EmployeeId             = app.EmployeeId,
                LeaveId                = app.LeaveId,
                EntryType              = LedgerEntryType.Reversal,
                EntryDate              = DateOnly.FromDateTime(today),
                Add                    = deductedAmount,
                Less                   = 0m,
                Balance                = credits.Balance,
                Particulars            = $"Leave {statusLabel} — reversal of DTR-confirmed {deductedAmount:0.##} day(s)",
                LeaveCreditsId         = credits.Id,
                ReferenceApplicationId = app.Id,
            }, token);

            _logger.LogInformation(
                "Leave {Status}: reversed DTR-confirmed {Days} day(s) deduction for employee {EmployeeId}",
                statusLabel, deductedAmount, app.EmployeeId);
        }
    }




    public Task<List<LeaveApplicationModel>> RangeAsync(DateEmployeeRequestPayload payload,
        CancellationToken token)
    {
        return GetQueryable()
            .Where(x => x.LeaveDateFrom >= payload.FromDate
                    && x.LeaveDateTo <= payload.ToDate
                    && payload.EmployeeIds.Contains(x.EmployeeId))
            .ProjectToType<LeaveApplicationModel>(_config)
            .ToListAsync(token);
    }

    public Task<List<LeaveApplicationModel>> FindAllAsync(CancellationToken token, DateOnly? from = null, DateOnly? to = null)
    {
        var query = GetQueryable();
        if (from.HasValue) query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt) >= from.Value);
        if (to.HasValue) query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt) <= to.Value);
        return query.ProjectToType<LeaveApplicationModel>(_config).ToListAsync(token);
    }
    public async Task<LeaveApplicationModel?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return _mapper.Map<LeaveApplicationModel>(await GetOneAsync(Id, token));
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
    }

    public async Task<Dictionary<Leavekey, List<LeaveApplication>>>
        FindByDateRangeAsync(DateOnly fromDate,
        DateOnly toDate,
        HashSet<Guid> employeeIds,
        CancellationToken token)
    {
        var data = await _uow.Repository
        .Find<LeaveApplication>(x => x.LeaveDateFrom >= fromDate
            && x.ApprovalStatus == ApprovalStatus.Approved
            && x.LeaveDateTo <= toDate
            && employeeIds.Contains(x.EmployeeId))
        .Include(x => x.Leave)
        .GroupBy(a => new Leavekey(a.EmployeeId))
        .ToDictionaryAsync(g => g.Key, g => g.OrderBy(x => x.LeaveDateFrom).ToList(), token);
        ;
        return data;
    }
}
public readonly record struct Leavekey(Guid EmpId);
public class ApprovalResult
{
    public string Status { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }
    public string Message { get; set; }
}