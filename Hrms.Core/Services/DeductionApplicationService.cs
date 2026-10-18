using Hrms.Core.Policies.OtherIncome.Eligibility;
using Hrms.Core.Services.Approvals;
using Hrms.Core.Services.Schedules;
using Hrms.Core.Validations;
using Hrms.Domain.Entities;
using System.Linq.Expressions;

namespace Hrms.Core.Services;

public class DeductionApplicationService : BaseService<DeductionApplication>
{
    private readonly DeductionService _DeductionService;
    private readonly EmployeeService _employeeService;
    private readonly IMapper _mapper;
    private readonly ApprovalEngineService _approvalEngine;

    public DeductionApplicationService(IUnitOfWorkService uow,
           DeductionService DeductionService,
           EmployeeService employeeService,
           IMapper mapper,
           ApprovalEngineService approvalEngine
        ) : base(uow)
    {
        _DeductionService = DeductionService;
        _employeeService = employeeService;
        _mapper = mapper;
        _approvalEngine = approvalEngine;
    }

    protected override async Task<EvaluationResult> CreateValidatorAsync(DeductionApplication model, CancellationToken token)
    {
        // The validator has MustAsync rules (Deduction/Employee existence checks), so it must be
        // invoked via ValidateAsync — calling the synchronous Validate() here made every create,
        // update, approve, and decline on this service throw AsyncValidatorInvokedSynchronouslyException.
        var validator = await new DeductionApplicationValidator(_DeductionService, _employeeService).ValidateAsync(model, token);
        if (!validator.IsValid)
        {
            return EvaluationResult.Fail(validator.Errors);
        }
        return await base.CreateValidatorAsync(model, token);
    }

    // A loan's installments as payroll has touched them -- see LoanScheduleReconciler.
    private async Task<LoanScheduleReconciler> ReconcilerForAsync(DeductionApplication model, CancellationToken token)
    {
        var ids = model.Breakdown.Select(x => x.Id).ToList();
        var used = await Context.IgnoringConfidentiality<PayrollDeductionDetail>()
            .Where(x => ids.Contains(x.DeductionApplicationDetailId))
            .Select(x => x.DeductionApplicationDetailId)
            .Distinct()
            .ToListAsync(token);
        return new LoanScheduleReconciler(used.ToHashSet());
    }

    private Task<DeductionApplication?> LoadTrackedAsync(Guid id, CancellationToken token)
    {
        return _uow.Repository.FindAll<DeductionApplication>()
            .Include(x => x.Breakdown)
            .FirstOrDefaultAsync(x => x.Id == id, token);
    }

    // Installments are only ever added through the Breakdown navigation: the loan's Id is still
    // Guid.Empty here (EF assigns it on Add), so EF fills in DeductionApplicationId itself. This
    // used to look up "existing" installments by that empty Id -- which matched every other
    // loan's create-made installments and deleted them all.
    public async Task<DeductionApplication> AddAsync(CreateDeductionApplication payload, ApprovalStatus status,
        CancellationToken token, bool isSelfService = false)
    {
        // Only the Employee Portal's own filing path is gated here — an HR-initiated application
        // (isSelfService = false, the default) can still use any Deduction regardless of
        // AllowEmployeeFiling, since that flag only controls what employees can file themselves.
        if (isSelfService)
            await EnsurePortalFileableAsync(payload.DeductionId, token);

        var model = _mapper.Map<DeductionApplication>(payload);
        model.ApprovalStatus = status;
        var rows = (payload.Breakdown ?? []).Select(x => new UpdateDeductionApplicationDetail
        {
            Date = x.Date, Principal = x.Principal, Interest = x.Interest, Amount = x.Amount,
            PeriodStart = x.PeriodStart, PeriodEnd = x.PeriodEnd,
        });
        LoanScheduleReconciler.ForNewLoan.Reconcile(model, model.Breakdown, rows, _ => { });
        await CreateAsync(model, token);
        if (model.ApprovalStatus == ApprovalStatus.ForApproval)
            await _approvalEngine.StartAsync(ApprovalApplicationType.Loan, model.Id, model.EmployeeId, token);
        await CommitChangesAsync(token);
        return model;
    }
    private async Task EnsurePortalFileableAsync(Guid deductionId, CancellationToken token)
    {
        var deduction = await _DeductionService.FineOneAsync(deductionId, token)
            ?? throw new NotFoundException("Deduction not found");

        if (!deduction.AllowEmployeeFiling)
            throw new InvalidOperationException(
                $"\"{deduction.Name}\" cannot be filed through the Employee Portal. Please coordinate with HR.");
    }

    // Plain field edit -- never changes ApprovalStatus; only ApproveAsync/DeclineAsync/WithdrawAsync
    // do. The payload carries no status and is mapped onto the stored loan, so the stored status
    // stays (rebuilding the loan from the payload used to fall back to the entity default,
    // Approved -- editing a pending loan approved it with no approver involved).
    public async Task<DeductionApplication> UpdateAsync(UpdateDeductionApplication payload,
        CancellationToken token)
    {
        var model = await LoadTrackedAsync(payload.Id, token)
            ?? throw new NotFoundException("Record not found");
        var reconciler = await ReconcilerForAsync(model, token);
        Guard.EnsureFalse(reconciler.AnyLocked(model.Breakdown) &&
                          (payload.EmployeeId != model.EmployeeId || payload.DeductionId != model.DeductionId),
            "The employee or deduction can't be changed: part of this schedule was already deducted in a payroll run.");

        _mapper.Map(payload, model);
        // A pre-migration row stores '' in these columns, which the converter reads as the default,
        // so EF saw "no change" when the form sent that default and never repaired the column.
        var entry = Context.Entry(model);
        entry.Property(x => x.PayMode).IsModified = true;
        entry.Property(x => x.AttendanceBasis).IsModified = true;
        entry.Property(x => x.OnMiss).IsModified = true;
        await Guard.ModelGuardAsync<DeductionApplication>(CreateValidatorAsync, model, token);
        reconciler.Reconcile(model, model.Breakdown, payload.Details ?? [], row => _uow.Repository.Remove(row));

        // Changed content goes back to the first approver if anyone already signed off.
        if (model.ApprovalStatus == ApprovalStatus.ForApproval)
            await _approvalEngine.RestartIfProgressedAsync(ApprovalApplicationType.Loan, model.Id, token);

        await CommitChangesAsync(token);
        return (await FineOneAsync(model.Id, token))!;
    }

    // Self-service edit: only the caller's own, still-pending loan, and only for a deduction type
    // employees may file themselves. See MeController.UpdateMyLoanApplication.
    public async Task<DeductionApplication> UpdateOwnAsync(UpdateDeductionApplication payload, Guid employeeId,
        CancellationToken token)
    {
        var existing = await GetQueryable(x => x.Id == payload.Id)
            .Select(x => new { x.EmployeeId, x.ApprovalStatus })
            .FirstOrDefaultAsync(token);
        if (existing == null || existing.EmployeeId != employeeId)
            throw new NotFoundException("Loan application not found.");
        if (existing.ApprovalStatus != ApprovalStatus.ForApproval)
            throw new InvalidOperationException("Only applications still awaiting approval can be edited.");

        await EnsurePortalFileableAsync(payload.DeductionId, token);
        return await UpdateAsync(payload, token);
    }

    public Task<List<DeductionApplication>> FindAllAsync(CancellationToken token)
    {
        return GetQueryable().ToListAsync(token);
    }

    // Self-service "My Loan Ledger" — every status, newest first, scoped to one employee, with
    // the installment schedule included so the ledger can show per-payment detail. See
    // MeController.GetMyLoanApplications.
    public Task<List<DeductionApplication>> FindAllForEmployeeAsync(Guid employeeId, CancellationToken token)
    {
        return GetQueryable(x => x.EmployeeId == employeeId)
            .Include(x => x.Breakdown)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(token);
    }


    public async Task ApproveAsync(
        Guid id, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token)
    {
        var existing = await GetOneAsync(id, token);
        if (existing == null) return;
        if (existing.ApprovalStatus != ApprovalStatus.ForApproval)
            throw new InvalidOperationException("This application is not awaiting approval.");

        var approverId = approverEmployeeId ?? throw new AccountNotLinkedException("this approval action can't be recorded");

        var result = await _approvalEngine.RecordActionAsync(
            ApprovalApplicationType.Loan, existing.Id, existing.EmployeeId,
            approverId, approverHasOverride, ApprovalActionType.Approved, note, token);

        existing.ApprovalStatus = ApprovalEngineService.MapInstanceStatus(result.InstanceStatus);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    public async Task DeclineAsync(
        Guid id, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token)
    {
        var existing = await GetOneAsync(id, token);
        if (existing == null) return;
        if (existing.ApprovalStatus != ApprovalStatus.ForApproval)
            throw new InvalidOperationException("This application is not awaiting approval.");

        var approverId = approverEmployeeId ?? throw new AccountNotLinkedException("this approval action can't be recorded");

        var result = await _approvalEngine.RecordActionAsync(
            ApprovalApplicationType.Loan, existing.Id, existing.EmployeeId,
            approverId, approverHasOverride, ApprovalActionType.Declined, note, token);

        existing.ApprovalStatus = ApprovalEngineService.MapInstanceStatus(result.InstanceStatus);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    // Self-service cancel of the employee's own still-pending loan request. A ForApproval
    // application never touched payroll (see AddAsync's comment on the portal path), so nothing
    // needs to be unwound. See MeController's loan-applications/{id}/withdraw endpoint.
    public async Task WithdrawAsync(Guid id, Guid employeeId, CancellationToken token)
    {
        var existing = await GetOneAsync(id, token);
        if (existing == null || existing.EmployeeId != employeeId)
            throw new NotFoundException("Application not found");
        if (existing.ApprovalStatus != ApprovalStatus.ForApproval)
            throw new InvalidOperationException("Only pending applications can be withdrawn.");

        existing.ApprovalStatus = ApprovalStatus.Withdrawn;
        await ModifyAsync(existing, token);
        // Its approval request stops too -- see ApprovalEngineService.CancelAsync.
        await _approvalEngine.CancelAsync(ApprovalApplicationType.Loan, existing.Id, token);
        await CommitChangesAsync(token);
    }

    public Task<List<DeductionApplicationDetail>> FindDetail(Expression<Func<DeductionApplicationDetail, bool>> expression)
    {
        return _uow.Repository.Find(expression).ToListAsync();
    }

    public async Task<DeductionApplication?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetQueryable()
            .Include(x => x.Breakdown.OrderBy(d => d.RecordOrder).ThenBy(d => d.Date))
            .FirstOrDefaultAsync(x => x.Id == Id, token);
    }

    // The installments go with the loan. Refused once payroll has touched any of them.
    public async Task DeleteParent(Guid Id, CancellationToken token)
    {
        var model = await LoadTrackedAsync(Id, token);
        if (model == null) return;
        var reconciler = await ReconcilerForAsync(model, token);
        Guard.EnsureFalse(reconciler.AnyLocked(model.Breakdown),
            "This loan can't be deleted: part of its schedule was already deducted in a payroll run.");
        foreach (var row in model.Breakdown.ToList())
            _uow.Repository.Remove(row);
        _uow.Repository.Remove(model);
        await CommitChangesAsync(token);
    }
    public async Task DeleteChildAsync(Guid Id, CancellationToken token)
    {
        var item = await _uow.Repository.FindOneAsync<DeductionApplicationDetail>(Id, token);
        if (item == null) return;
        var used = await Context.IgnoringConfidentiality<PayrollDeductionDetail>()
            .AnyAsync(x => x.DeductionApplicationDetailId == Id, token);
        new LoanScheduleReconciler(used ? new HashSet<Guid> { Id } : new HashSet<Guid>()).EnsureNotLocked(item);
        _uow.Repository.Remove(item);
        await CommitChangesAsync(token);
    }
}

public class DeductionAplDtlService : BaseService<DeductionApplicationDetail>
{
    public DeductionAplDtlService(IUnitOfWorkService uow) : base(uow) { }
    public async Task<Dictionary<EmployeeKey, List<DeductionInfo>>>
        LoadAsync(List<Guid> employeeIds, DateOnly fromDate, DateOnly toDate,
        CancellationToken token)
    {
        // "LOAN"-coded DeductionTypes (COLOAN/SSSLOAN/HDMFLOAN/SALLOAN/CALLOAN, seeded in
        // AccountInitService.SetDefaultDeductionTypes) are surfaced as their own payslip line —
        // everything else (cash advance, medical/dental, etc.) stays "Others".
        var rows = await (
            from detail in GetQueryable().AsNoTracking()
            join deduction in Context.Deductions.AsNoTracking()
                on detail.DeductionId equals deduction.Id into deductionJoin
            from deduction in deductionJoin.DefaultIfEmpty()
            join category in Context.DeductionTypes.AsNoTracking()
                on deduction!.CategoryId equals category.Id into categoryJoin
            from category in categoryJoin.DefaultIfEmpty()
            // Self-service loan applications file ForApproval and must not affect payroll until
            // an admin approves them. The loan must exist and be live: allowing a missing parent
            // let every installment saved with an empty ApplicationId (all create-made ones,
            // before the single DeductionApplicationId FK) skip this approval gate, and kept a
            // deleted loan deducting.
            join application in Context.DeductionApplications.AsNoTracking()
                on detail.DeductionApplicationId equals application.Id
            where detail.Date <= toDate && employeeIds.Contains(detail.EmployeeId) && detail.Balance > 0
                && !detail.IsSkipped
                && application.ApprovalStatus == ApprovalStatus.Approved
            select new
            {
                detail.Id,
                detail.EmployeeId,
                detail.Date,
                detail.Balance,
                detail.DeductionId,
                detail.RecordOrder,
                DeductionTypeCode = category != null ? category.Code : null,
            })
            .ToListAsync(token);

        // Attendance-mode installments that don't qualify are left out of this run. Which of them
        // are then skipped for good or deferred to the next run is decided when the run is saved
        // (PayrollInputConsumptionService); a deferred row is never judged again, so it comes back.
        var misses = await DeductionEligibilityEvaluator.FindMissesAsync(Context, employeeIds, toDate, token);
        if (misses.Count > 0)
            rows = rows.Where(x => !misses.ContainsKey(x.Id)).ToList();

        return rows
            .GroupBy(x => new EmployeeKey(x.EmployeeId))
            .ToDictionary(g => g.Key, g => g
                // Oldest-due installment first (an amortization schedule is meant to be paid
                // down in order), RecordOrder as the tiebreaker within the same date -- without
                // this, ScheduledDeductionPolicy would process an employee's multiple loans/
                // other deductions in whatever arbitrary order the database happened to return
                // them, so which one gets paid when there isn't room for all of them (see
                // DeductionValidator.CanApply / minimum take-home floor) was effectively random.
                .OrderBy(x => x.Date).ThenBy(x => x.RecordOrder)
                .Select(x => new DeductionInfo
                {
                    Id = x.Id,
                    PayrollDate = x.Date,
                    Amount = x.Balance,
                    DeductionId = x.DeductionId,
                    Type = x.DeductionTypeCode != null && x.DeductionTypeCode.Contains("LOAN")
                        ? DeductionInfoType.Loan
                        : DeductionInfoType.Others,
                }).ToList());
    }
}
