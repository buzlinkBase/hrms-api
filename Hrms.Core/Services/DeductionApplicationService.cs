using Hrms.Core.Services.Approvals;
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

    private async Task AddOrDeleteChildAsync(DeductionApplication model, CancellationToken token)
    {
        var existing = await _uow.Context.DeductionApplicationDetails
         .Where(x => x.ApplicationId == model.Id)
         .ToListAsync(token);

        foreach (var detail in model.Breakdown)
        {
            var match = existing.FirstOrDefault(x => x.Id == detail.Id);
            if (match != null)
            {
                // Update fields
                match.Date = detail.Date;
                match.Amount = detail.Amount;
                match.Balance = detail.Balance;
                match.Notes = detail.Notes;
            }
            else
            {
                await _uow.Context.DeductionApplicationDetails.AddAsync(detail, token);
            }
        }
        // remove deleted ones
        var toRemove = existing.Where(x => !model.Breakdown.Any(d => d.Id == x.Id));
        _uow.Context.DeductionApplicationDetails.RemoveRange(toRemove);
    }
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
        foreach (var item in payload.Breakdown)
        {
            var detail = new DeductionApplicationDetail
            {
                Date = item.Date,
                Interest = item.Interest,
                Principal = item.Principal,
                Amount = item.Amount,
                Balance = item.Amount,
                ApplicationId = model.Id,
                DeductionId = model.DeductionId,
                EmployeeId = model.EmployeeId,
            };
            model.Breakdown.Add(detail);
        }
        await AddOrDeleteChildAsync(model, token);
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

    public async Task<DeductionApplication> UpdateAsync(UpdateDeductionApplication payload,
        CancellationToken token)
    {
        var model = _mapper.Map<DeductionApplication>(payload);
        foreach (var item in payload.Details)
        {
            var detail = new DeductionApplicationDetail
            {
                Id = item.Id,
                Date = item.Date,
                Interest = item.Interest,
                Principal = item.Principal,
                Amount = item.Amount,
                Balance = item.Amount,
                ApplicationId = model.Id,
                DeductionId = model.DeductionId,
                EmployeeId = model.EmployeeId,
            };
            model.Breakdown.Add(detail);
        }
        await AddOrDeleteChildAsync(model, token);
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);
        return model;
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

        var approverId = approverEmployeeId ?? throw new InvalidOperationException(
            "Your account isn't linked to an Employee record, so this approval action can't be recorded. Contact an admin to link your account.");

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

        var approverId = approverEmployeeId ?? throw new InvalidOperationException(
            "Your account isn't linked to an Employee record, so this approval action can't be recorded. Contact an admin to link your account.");

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
        await CommitChangesAsync(token);
    }

    public Task<List<DeductionApplicationDetail>> FindDetail(Expression<Func<DeductionApplicationDetail, bool>> expression)
    {
        return _uow.Repository.Find(expression).ToListAsync();
    }

    public async Task<DeductionApplication?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task DeleteParent(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);

        var models = _uow.Repository
            .Find<DeductionApplicationDetail>(x => x.ApplicationId == Id);
        _uow.Repository.RemoveRange(models);
        await CommitChangesAsync(token);

    }
    public async Task DeleteChildAsync(Guid Id, CancellationToken token)
    {
        var item = _uow.Repository.FindOne<DeductionApplicationDetail>(Id);
        if (item == null) return;
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
            // an admin approves them — ApplicationId is a required FK so `application == null`
            // should never actually happen, kept only for consistency with the defensive
            // null-safety style already used for the deduction/category joins above.
            join application in Context.DeductionApplications.AsNoTracking()
                on detail.ApplicationId equals application.Id into applicationJoin
            from application in applicationJoin.DefaultIfEmpty()
            where detail.Date <= toDate && employeeIds.Contains(detail.EmployeeId) && detail.Balance > 0
                && (application == null || application.ApprovalStatus == ApprovalStatus.Approved)
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
