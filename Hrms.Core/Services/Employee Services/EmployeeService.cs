using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Mapster;
using System.Linq.Expressions;

namespace Hrms.Core.Services;

public class EmployeeService : BaseService<Employee>
{
    private readonly IMapper _mapper;
    private readonly TypeAdapterConfig _config;
    private readonly DepartmentService _departmentService;
    private readonly PayrollGroupService _payrollGroupService;
    private readonly BranchService _branchService;
    private readonly SectionService _sectionService;
    private readonly EmployeeAccountLinkService? _accountLinks;
    private readonly Security.ConfidentialAccessScope? _access;

    // accountLinks and access are optional so the many direct constructions in tests stay valid;
    // the container always supplies them.
    public EmployeeService(IUnitOfWorkService uow,
        IMapper mapper,
        TypeAdapterConfig config,
        DepartmentService departmentService,
        PayrollGroupService payrollGroupService,
        BranchService branchService,
        CostCenterService areaService,
        PositionService positionService,
        SectionService sectionService,
        EmployeeAccountLinkService? accountLinks = null,
        Security.ConfidentialAccessScope? access = null
        ) : base(uow)
    {
        _mapper = mapper;
        _config = config;
        _departmentService = departmentService;
        _payrollGroupService = payrollGroupService;
        _branchService = branchService;
        _sectionService = sectionService;
        _accountLinks = accountLinks;
        _access = access;
    }

    /// <summary>
    /// True when the caller can't open this employee's record: hidden as confidential, or visible
    /// only through a request they're approving (they see that request, never the profile).
    /// </summary>
    public bool IsHiddenFromEmployeeScreens(Guid employeeId) =>
        _access != null && (!_access.CanSeeEmployee(employeeId) || _access.IsApprovalOnlyEmployee(employeeId));

    private bool CanSeePayOf(Guid employeeId) =>
        _access?.CanSee(ConfidentialDataClass.Compensation, employeeId) ?? true;

    /// <summary>
    /// Blanks the pay fields of a confidential employee seen through a partial clearance
    /// (Operations / Entries) -- only full clearance sees a confidential employee's pay.
    /// </summary>
    public T HidePayIfNotCleared<T>(T view) where T : IEmployeePayView
    {
        if (!CanSeePayOf(view.Id)) view.HidePay();
        return view;
    }

    private List<T> HidePayIfNotCleared<T>(List<T> views) where T : IEmployeePayView
    {
        views.ForEach(v => HidePayIfNotCleared(v));
        return views;
    }

    /// <summary>
    /// Every employee, confidential ones included -- for uniqueness and matching checks (Bio ID,
    /// email, import), which must never pass just because the clashing employee is hidden. Same as
    /// GetQueryable when the caller isn't restricted.
    /// </summary>
    public IQueryable<Employee> EmployeesForIntegrityChecks() =>
        _access is { Restricted: true } ? Context.EmployeesForIntegrityChecks() : GetQueryable();

    // Employee screens (list, search, 201 file): the query filter already hides confidential
    // employees; this also leaves out the ones visible only via an approval.
    private IQueryable<Employee> EmployeeScreenQuery(Expression<Func<Employee, bool>>? exp = null)
    {
        var query = exp == null ? GetQueryable() : GetQueryable(exp);
        var approvalOnly = _access is { Restricted: true } ? _access.ApprovalVisibleEmployeeIds : [];
        return approvalOnly.Length == 0 ? query : query.Where(x => !approvalOnly.Contains(x.Id));
    }

    protected override async Task<EvaluationResult> CreateValidatorAsync(Employee model, CancellationToken token)
    {
        if (model.BioId.HasValue || model.BioId > 0)
        {
            // Checked against every employee, confidential ones included -- the message names no one.
            var emp = EmployeesForIntegrityChecks()
                .FirstOrDefault(x => x.Id != model.Id && x.BioId == model.BioId.Value);
            if (emp != null)
            {
                return new EvaluationResult("Bio ID conflicts with another employee");
            }
        }

        // Email uniquely identifies an employee for login resolution (ResolveEmployeeIdAsync
        // matches by Email when UserId isn't set, and the approval engine resolves an approver's
        // own Employee the same way) -- a duplicate email within the tenant would make that
        // resolution ambiguous. GetQueryable is already tenant-scoped (HrmsContext's global query
        // filter), so this only checks within the current tenant.
        if (!string.IsNullOrWhiteSpace(model.Email))
        {
            var normalizedEmail = model.Email.Trim().ToLower();
            var duplicate = EmployeesForIntegrityChecks()
                .FirstOrDefault(x => x.Id != model.Id && x.Email != null && x.Email.ToLower() == normalizedEmail);
            if (duplicate != null)
            {
                return new EvaluationResult("Email is already used by another employee");
            }
        }

        var py = await _payrollGroupService.FineOneAsync(model.PayrollGroupId, token);
        if (py == null)
        {
            return new EvaluationResult("Payroll group is required");
        }

        if (model.DepartmentId.HasValue && model.DepartmentId != Guid.Empty)
        {
            var department = await _departmentService
                .FineOneAsync(model.DepartmentId.Value, token);
            if (department == null)
            {
                return new EvaluationResult("Invalid Department");
            }
        }

        if (model.SectionId.HasValue && model.SectionId != Guid.Empty)
        {
            var section = await _sectionService.FineOneAsync(model.SectionId.Value, token); // Use Section Service!
            if (section == null)
            {
                return new EvaluationResult("Invalid Section");
            }
        }

        if (model.ManagerId.HasValue && model.ManagerId != Guid.Empty)
        {
            if (model.ManagerId == model.Id)
            {
                return new EvaluationResult("An employee cannot be their own manager");
            }
            var managerExists = await EmployeesForIntegrityChecks()
                .AnyAsync(x => x.Id == model.ManagerId.Value, token);
            if (!managerExists)
            {
                return new EvaluationResult("Invalid Manager");
            }
        }
        return await base.CreateValidatorAsync(model, token);
    }

    public async Task AddAsync(Employee model, CancellationToken token)
    {
        CalculateAge(model);
        if (model.HireDate == DateOnly.MinValue)
        {
            model.HireDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }
        var branch = _uow.Repository.FindOne<Branch>(model.BranchId ?? Guid.Empty);
        model.BranchId = branch?.Id;
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }

    private void CalculateAge(Employee? model)
    {
        if (model == null || !model.DOB.HasValue) return;
        var today = DateTime.Today;
        if (model.DOB.Value > today) throw new ArgumentException("Date of birth cannot be in the future.");
        int age = today.Year - model.DOB.Value.Year;
        // Adjust if birthday hasn't occurred yet this year
        if (model.DOB.Value.Date > today.AddYears(-age)) age--;
        model.Age = age;
    }


    public async Task AddOrUpdateRange(List<Employee> models, CancellationToken token = default)
    {
        if (models == null || models.Count == 0) return;
        var newemps = models.Where(x => x.Id == Guid.Empty).ToList();
        var old = models.Where(x => x.Id != Guid.Empty).ToList();
        await ModifyRangeAsync(old, token);
        await CreateRangeAsync(newemps, token);
        //var empBios = GetQueryable().Select(x => x.BioId).ToList();
        //var existingBioIds = new HashSet<int>(empBios);
        //var newEmployees = models
        //    .Where(x => !existingBioIds.Contains(x.BioId))
        //    .ToList();
        //foreach (var emp in newEmployees)
        //{
        //    if (emp.Id == Guid.Empty)
        //    {
        //        emp.DateRegistered = DateTime.Now;
        //    }
        //}
        //if (newEmployees.Count > 0)
        //{
        //    await CreateRangeAsync(newEmployees, token);
        //}
    }

    // canChangeConfidentiality: only a caller with confidential clearance may set or clear the
    // tag; everyone else (and internal callers) keeps the stored value.
    public async Task UpdateAsync(UpdateEmployee payload, CancellationToken token, bool canChangeConfidentiality = false)
    {
        if (IsHiddenFromEmployeeScreens(payload.Id))
        {
            throw new NotFoundException("Record not found");
        }

        await _uow.Context.RestDays
        .Where(x => x.EmployeeId == payload.Id)
        .ExecuteDeleteAsync(token);

        await _uow.Context.EmployeeFixedSchedules
        .Where(x => x.EmployeeId == payload.Id)
        .ExecuteDeleteAsync(token);

        await _uow.SaveChangesAsync(token);


        var existing = await Context.Employees
            //.Include(x => x.RestDays)
            //.Include(x => x.Settings)
            //.Include(x => x.SSSRate)
            //.Include(x => x.PHICRate)
            //.Include(x => x.HDMFRate)
            //.Include(x => x.TaxRate)
            .FirstOrDefaultAsync(x => x.Id == payload.Id, token);

        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }

        var incomingRestDays = payload.RestDays?.ToList() ?? new List<RestDayModel>();
        var incomingFixedSchedule = payload.FixedSchedule?.ToList() ?? new List<EmployeeFixedScheduleDayModel>();
        payload.RestDays = new List<RestDayModel>();
        payload.FixedSchedule = new List<EmployeeFixedScheduleDayModel>();

        var wasConfidential = existing.IsConfidential;
        // The caller got this employee with pay blanked (partial clearance): save the stored pay,
        // never the blanks. The statutory rate rows are hidden from them too, so leave them alone.
        if (!CanSeePayOf(existing.Id))
        {
            existing.CopyPayTo(payload);
            payload.SSSRate = null;
            payload.PHICRate = null;
            payload.HDMFRate = null;
            payload.TaxRate = null;
        }
        payload.Adapt(existing);
        if (!canChangeConfidentiality) existing.IsConfidential = wasConfidential;
        CalculateAge(existing);

        if (existing.HireDate == DateOnly.MinValue || payload.HireDate == null)
        {
            existing.HireDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        existing.Email = string.IsNullOrWhiteSpace(payload.Email) ? null : payload.Email.Trim();

        var branch = _uow.Repository.FindOne<Branch>(existing.BranchId ?? Guid.Empty);
        existing.BranchId = branch?.Id;
        await ModifyAsync(existing, token);

        var newRestDays = incomingRestDays.Select(rd => new RestDay
        {
            Id = Guid.CreateVersion7(),
            EmployeeId = payload.Id,
            DayName = rd.DayName,
        }).ToList();
        await _uow.Repository.AddRangeAsync(newRestDays, token);

        var newFixedSchedule = incomingFixedSchedule.Select(fs => new EmployeeFixedSchedule
        {
            Id = Guid.CreateVersion7(),
            EmployeeId = payload.Id,
            DayName = fs.DayName,
            TimeShiftId = fs.TimeShiftId,
        }).ToList();
        await _uow.Repository.AddRangeAsync(newFixedSchedule, token);

        await _uow.SaveChangesAsync(token);

        await CommitChangesAsync(token);
    }



    public async Task<Employee?> FindOne(Guid id, CancellationToken token)
    {
        var data = await Context.Employees.FindAsync(id, token);
        return data;
    }

    public async Task<List<Employee>> FindByIds(List<Guid> Ids, CancellationToken token)
    {
        if (Ids == null || Ids.Count == 0)
            return new List<Employee>();

        var data = await GetQueryable()
            .Where(x => Ids.Contains(x.Id))
            .Select(x => x)
            .ToListAsync(token);
        return data;
    }

    // Employees eligible for a 13th month pay run (Settings.IsEligibleFor13thMonth), scoped
    // to payrollGroupIds/employeeIds when provided (both null/empty = everyone eligible).
    // Loads PayrollGroup/TaxRate/Settings explicitly — EmployeeModelPayrollRun's mapping
    // (MappingProfile.cs) reads PayrollGroup.PayrollFrequency, so it must be Include()d rather
    // than relying on lazy loading. See PayrollProcessorService.GenerateThirteenthMonthAsync.
    public async Task<List<EmployeeModelPayrollRun>> GetForThirteenthMonthRunAsync(
        List<Guid>? payrollGroupIds, List<Guid>? employeeIds, CancellationToken token)
    {
        var employees = await GetQueryable(x =>
                (employeeIds == null || employeeIds.Count == 0 || employeeIds.Contains(x.Id)) &&
                (payrollGroupIds == null || payrollGroupIds.Count == 0 || payrollGroupIds.Contains(x.PayrollGroupId)) &&
                x.Settings != null && x.Settings.IsEligibleFor13thMonth)
            .Include(x => x.PayrollGroup)
            .Include(x => x.TaxRate)
            .Include(x => x.Settings)
            .ToListAsync(token);
        return _mapper.Map<List<EmployeeModelPayrollRun>>(employees);
    }

    // Employees eligible for a Last Pay run — separated (per the SeparatedStatuses used
    // elsewhere to EXCLUDE these same employees from active DTR/filter queries) with a
    // recorded DateResigned to anchor the proration window. See PayrollProcessorService.GenerateLastPayAsync.
    public async Task<List<EmployeeModelPayrollRun>> GetSeparatedEmployeesForLastPayAsync(
        List<Guid>? employeeIds, CancellationToken token)
    {
        var employees = await GetQueryable(x =>
                (employeeIds == null || employeeIds.Count == 0 || employeeIds.Contains(x.Id)) &&
                SeparatedStatuses.Contains(x.EmploymentStatus) && x.DateResigned != null)
            .Include(x => x.PayrollGroup)
            .Include(x => x.TaxRate)
            .Include(x => x.Settings)
            .ToListAsync(token);
        return _mapper.Map<List<EmployeeModelPayrollRun>>(employees);
    }

    public async Task<List<Employee>> FindAllAsync(CancellationToken token)
    {
        var data = await GetQueryable()
            .ToListAsync(token);
        return data;
    }

    public async Task<List<EmployeeModel>> GetAll(string? keyword, CancellationToken token)
    {
        Expression<Func<Employee, bool>> exp = x => string.IsNullOrWhiteSpace(keyword)
            || x.FirstName.Contains(keyword!)
            || x.LastName.Contains(keyword!)
            || x.MiddleName.Contains(keyword!)
            || x.Suffix.Contains(keyword!)
            || x.EmployeeNo.Contains(keyword!)
            || (x.Email != null && x.Email.Contains(keyword));
        ;

        var query = EmployeeScreenQuery(exp);
        var employees = await query
            .ProjectToType<EmployeeModel>(_config)
            .ToListAsync(token);
        return HidePayIfNotCleared(employees);
    }


    public async Task<PaginatedResult<List<EmployeeModel>>> LoadAll(PaginationPayload payload, CancellationToken token)
    {
        Expression<Func<Employee, bool>> exp = x => string.IsNullOrWhiteSpace(payload.Keyword) || x.FirstName.Contains(payload.Keyword)
            || x.LastName.Contains(payload.Keyword)
            || x.MiddleName.Contains(payload.Keyword)
            || x.Suffix.Contains(payload.Keyword)
            || x.EmployeeNo.Contains(payload.Keyword)
            || (x.Email != null && x.Email.Contains(payload.Keyword));;

        var query = EmployeeScreenQuery(exp);
        var dataQuery = PaginatedQuerable(query, payload.Page, payload.Limit)
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            ;
        var employees = await dataQuery
            .ProjectToType<EmployeeModel>(_config)
            .ToListAsync();
        return new PaginatedResult<List<EmployeeModel>>()
        {
            Data = HidePayIfNotCleared(employees),
            MetaData = new PaginationMetaData(await query.CountAsync(), payload.Page, payload.Limit)
        };
    }
    // Setup → Employee table: filtering, sorting and paging all happen here, in the database,
    // instead of shipping every employee to the browser. Same EmployeeModel projection as
    // LoadAll so the table gets identical fields. See EmployeeListQueryBuilder.
    public async Task<PaginatedResult<List<EmployeeModel>>> SearchAsync(EmployeeListQuery query, CancellationToken token)
    {
        var page = query.NormalizedPage;
        var limit = query.NormalizedLimit;

        var filtered = EmployeeScreenQuery().ApplyFilters(query);
        var total = await filtered.CountAsync(token);

        var employees = await filtered
            .ApplySort(query.SortField, query.SortOrder)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ProjectToType<EmployeeModel>(_config)
            .ToListAsync(token);

        return new PaginatedResult<List<EmployeeModel>>
        {
            Data = HidePayIfNotCleared(employees),
            MetaData = new PaginationMetaData(total, page, limit),
        };
    }

    public async Task<PaginatedResult<List<EmployeeFullModel>>> LoadAllFullAsync(PaginationPayload payload, CancellationToken token)
    {

        Expression<Func<Employee, bool>> exp = x => string.IsNullOrWhiteSpace(payload.Keyword) || x.FirstName.Contains(payload.Keyword)
            || x.LastName.Contains(payload.Keyword)
            || x.MiddleName.Contains(payload.Keyword)
            || x.Suffix.Contains(payload.Keyword);

        var query = EmployeeScreenQuery(exp);
        var dataQuery = PaginatedQuerable(query, payload.Page, payload.Limit)
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            ;

        var employees = await dataQuery
            // One SQL per collection instead of a single JOIN across all six (Skills x Dependents x
            // Educations x Assets x Records x Employments rows). The old Include()s were no-ops --
            // EF ignores Include once ProjectToType shapes the result.
            .AsSplitQuery()
            .ProjectToType<EmployeeFullModel>(_config)
            .ToListAsync(token);

        return new PaginatedResult<List<EmployeeFullModel>>()
        {
            Data = HidePayIfNotCleared(employees),
            MetaData = new PaginationMetaData(await query.CountAsync(), payload.Page, payload.Limit)
        };
    }

    public async Task<EmployeeFullModel?> GetFullByIdAsync(Guid id, CancellationToken token)
    {
        var employee = await EmployeeScreenQuery(x => x.Id == id)
            // One SQL per collection instead of a single JOIN across all six (Skills x Dependents x
            // Educations x Assets x Records x Employments rows). The old Include()s were no-ops --
            // EF ignores Include once ProjectToType shapes the result.
            .AsSplitQuery()
            .ProjectToType<EmployeeFullModel>(_config)
            .FirstOrDefaultAsync(token);
        return employee == null ? null : HidePayIfNotCleared(employee);
    }
    // Resolves the calling user's own Employee record for the self-service portal. UserId is
    // the primary match (backfilled by UserOnboardedWorker when an invitation names an
    // EmployeeId); email is a fallback for employees whose UserId link hasn't been made yet.
    //
    // Resolved through ResolveEmployeeIdAsync so an email-only match gets linked here too: the app
    // shell loads this for every signed-in user, and it used to never link -- which left accounts
    // that never came through an invitation (e.g. the Owner's own employee record) unlinked forever
    // and out of Department/Position approval notifications.
    public async Task<EmployeeFullModel?> GetFullByUserOrEmailAsync(Guid userId, string? email, CancellationToken token)
    {
        var employeeId = await ResolveEmployeeIdAsync(userId, email, token);
        if (employeeId == null) return null;

        return await GetQueryable(x => x.Id == employeeId.Value)
            // One SQL per collection instead of a single JOIN across all six (Skills x Dependents x
            // Educations x Assets x Records x Employments rows). The old Include()s were no-ops --
            // EF ignores Include once ProjectToType shapes the result.
            .AsSplitQuery()
            .ProjectToType<EmployeeFullModel>(_config)
            .FirstOrDefaultAsync(token);
    }

    // The account's own linked employee wins over one that merely shares its email. An employee
    // matched only by email, and not linked to any account yet, gets linked to this one on the
    // spot (see EmployeeAccountLinkService) -- from then on it's matched by UserId everywhere.
    public async Task<Guid?> ResolveEmployeeIdAsync(Guid userId, string? email, CancellationToken token)
    {
        var match = await GetQueryable(x => x.UserId == userId || (email != null && x.Email == email))
            .OrderByDescending(x => x.UserId == userId)
            .Select(x => new { x.Id, x.UserId })
            .FirstOrDefaultAsync(token);
        if (match == null) return null;

        if (match.UserId == null && userId != Guid.Empty)
            _accountLinks?.RequestLink(match.Id, userId);
        return match.Id;
    }

    /// <summary>"First Last" display names for the given employee ids, in one query -- ids with no
    /// matching employee are simply absent from the result.</summary>
    public async Task<Dictionary<Guid, string>> GetDisplayNamesAsync(IEnumerable<Guid> ids, CancellationToken token)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return [];
        return await GetQueryable(x => idList.Contains(x.Id))
            .Select(x => new { x.Id, x.FirstName, x.LastName })
            .ToDictionaryAsync(x => x.Id, x => $"{x.FirstName} {x.LastName}".Trim(), token);
    }

    public async Task<Employee?> FineOneAsync(Guid Id, CancellationToken token)
    {
        var result = await GetQueryable(x => x.Id == Id)
            .Include(x => x.RestDays)
            .FirstOrDefaultAsync(token)
            ;
        return result;
    }
    public async Task<Employee?> FineOneByEmailAsync(string email , CancellationToken token) 
    {
        var result = await GetQueryable(x => x.Email == email)
            .FirstOrDefaultAsync(token)
            ;
        return result;
    }

    private static readonly EmploymentStatus[] SeparatedStatuses =
    [
        EmploymentStatus.Terminated,
        EmploymentStatus.Resigned,
        EmploymentStatus.Retired,
        EmploymentStatus.Deceased,
    ];

    public async Task<List<EmployeeFilterResponseModel>> Filter(EmployeeFilter filter, CancellationToken token)
    {
        var result = await EmployeeScreenQuery(x =>
            !SeparatedStatuses.Contains(x.EmploymentStatus) &&
            (filter.DayName == null || x.RestDays.Any(xx => xx.DayName == filter.DayName)) &&
            (filter.BranchId == null || x.BranchId == filter.BranchId.Value) &&
            (filter.EmployeeId == null || x.Id == filter.EmployeeId.Value) &&
            (filter.DepartmentId == null || x.DepartmentId == filter.DepartmentId.Value) &&
            (filter.ClientId == null || x.ClientId == filter.ClientId.Value) &&
            (filter.PayrollGroupId == null || x.PayrollGroupId == filter.PayrollGroupId.Value) &&
            (filter.OperationAreaId == null || x.AreaId == filter.OperationAreaId.Value) &&
            (filter.ManagerId == null || x.ManagerId == filter.ManagerId.Value))
            .ProjectToType<EmployeeFilterResponseModel>()
            .ToListAsync(token);

        return result;
    }

    // Single source of truth for "which employees does this DTR-shaped request cover" —
    // shared by the live DTR run (CurrentRangeDTRPayloadService) and the Roster Report,
    // so both feed WorkScheduleResolver/RestDayResolver the exact same employee set
    // instead of maintaining two independently-drifting copies of this filter.
    public async Task<List<EmployeeDTRRun>> GetForDTRRunAsync(DTRRequestPayload payload, CancellationToken token)
    {
        return await GetQueryable(x =>
            //!SeparatedStatuses.Contains(x.EmploymentStatus) &&
            (payload.EmployeeId != null
                ? x.Id == payload.EmployeeId.Value
                : (payload.BranchId == null || x.BranchId == payload.BranchId.Value) &&
                  (payload.ClientId == null || x.ClientId == payload.ClientId.Value) &&
                  (payload.PayrollGroupId == null || x.PayrollGroupId == payload.PayrollGroupId.Value) &&
                  (payload.DepartmentId == null || x.DepartmentId == payload.DepartmentId.Value) &&
                  (payload.OperationAreaId == null || x.AreaId == payload.OperationAreaId.Value)))
            .Include(x => x.RestDays)
            .Select(x => new EmployeeDTRRun
            {
                Id = x.Id,
                AreaId = x.AreaId,
                ClientId = x.ClientId,
                FirstName = x.FirstName,
                LastName = x.LastName,
                MiddleName = x.MiddleName,
                Suffix = x.Suffix,
                TimeShiftId = x.TimeShiftId,
                BioId = x.BioId,
                EmpNo = x.EmployeeNo,
                PayrollGroupId = x.PayrollGroupId,
                DepartmentId = x.DepartmentId,
                DepartmentName = x.Department != null ? x.Department.Name : null,
                RestDays = x.RestDays.Select(r => new RestDayModel
                {
                    DayName = r.DayName,
                    Id = r.Id,
                }).ToList()
            }).ToListAsync(token);
    }


    public async Task Delete(Guid Id, CancellationToken token)
    {
        if (IsHiddenFromEmployeeScreens(Id))
        {
            throw new NotFoundException("Record not found");
        }
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }

    // Hard-deletes every employee whose EmployeeNo starts with the given prefix, plus
    // every row in any table that references them — used to clean up
    // EmployeeSeederService-generated test data (including whatever DTR/attendance/payroll
    // records testing against those employees produced). See EntityCascadeCleanupHelper
    // for how dependent tables are discovered and cleared.
    public async Task<int> RemoveByEmployeeNoPrefixAsync(string prefix, CancellationToken token)
    {
        var seededIds = await Context.Employees
            .Where(x => x.EmployeeNo.StartsWith(prefix))
            .Select(x => x.Id)
            .ToListAsync(token);

        return await EntityCascadeCleanupHelper.RemoveWithDependentsAsync<Employee>(Context, seededIds, token);
    }
}

