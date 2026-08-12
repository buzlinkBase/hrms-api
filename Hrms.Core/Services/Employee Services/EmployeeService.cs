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

    public EmployeeService(IUnitOfWorkService uow,
        IMapper mapper,
        TypeAdapterConfig config,
        DepartmentService departmentService,
        PayrollGroupService payrollGroupService,
        BranchService branchService,
        CostCenterService areaService,
        PositionService positionService,
        SectionService sectionService

        ) : base(uow)
    {
        _mapper = mapper;
        _config = config;
        _departmentService = departmentService;
        _payrollGroupService = payrollGroupService;
        _branchService = branchService;
        _sectionService = sectionService;
    }

    protected override async Task<EvaluationResult> CreateValidatorAsync(Employee model, CancellationToken token)
    {
        if (model.BioId.HasValue || model.BioId > 0)
        {
            var emp = GetQueryable(x => x.Id != model.Id && x.BioId == model.BioId.Value).FirstOrDefault();
            if (emp != null)
            {
                return new EvaluationResult("Bio ID conflicts with another employee");
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

    public async Task UpdateAsync(UpdateEmployee payload, CancellationToken token)
    {
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
        existing.RestDays.Clear();
        payload.Adapt(existing);
        CalculateAge(existing);
        foreach (var res in existing.RestDays)
        {
            res.Id = Guid.Empty;
        }
        if (existing.HireDate == DateOnly.MinValue || payload.HireDate == null)
        {
            existing.HireDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        var branch = _uow.Repository.FindOne<Branch>(existing.BranchId ?? Guid.Empty);
        existing.BranchId = branch?.Id;
        await ModifyAsync(existing, token);
        await _uow.SaveChangesAsync(token);
        var incomingDayNames = payload.RestDays.Select(rd => rd.DayName).ToHashSet();
        await _uow.Context.RestDays
            .Where(x => x.EmployeeId == payload.Id && !incomingDayNames.Contains(x.DayName))
            .ExecuteDeleteAsync(token);

        await CommitChangesAsync(token);
    }

    //public Employee? FindBio(int bioId)
    //{
    //    return GetQueryable(x => x.BioId == bioId).FirstOrDefault();
    //}

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

    public async Task<List<Employee>> FindAllAsync(CancellationToken token)
    {
        var data = await GetQueryable()
            .ToListAsync(token);
        return data;
    }

    public async Task<List<EmployeeModel>> GetAll(CancellationToken token)
    {
        var query = GetQueryable();
        var employees = await query
            .ProjectToType<EmployeeModel>(_config)
            .ToListAsync(token);
        return employees;
    }


    public async Task<PaginatedResult<List<EmployeeModel>>> LoadAll(PaginationPayload payload, CancellationToken token)
    {
        Expression<Func<Employee, bool>> exp = x => string.IsNullOrWhiteSpace(payload.Keyword) || x.FirstName.Contains(payload.Keyword)
            || x.LastName.Contains(payload.Keyword)
            || x.MiddleName.Contains(payload.Keyword)
            || x.Suffix.Contains(payload.Keyword);

        var query = GetQueryable(exp);
        var dataQuery = PaginatedQuerable(query, payload.Page, payload.Limit)
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            ;
        var employees = await dataQuery
            .ProjectToType<EmployeeModel>(_config)
            .ToListAsync();
        return new PaginatedResult<List<EmployeeModel>>()
        {
            Data = employees,
            MetaData = new PaginationMetaData(await query.CountAsync(), payload.Page, payload.Limit)
        };
    }
    public async Task<PaginatedResult<List<EmployeeFullModel>>> LoadAllFullAsync(PaginationPayload payload, CancellationToken token)
    {

        Expression<Func<Employee, bool>> exp = x => string.IsNullOrWhiteSpace(payload.Keyword) || x.FirstName.Contains(payload.Keyword)
            || x.LastName.Contains(payload.Keyword)
            || x.MiddleName.Contains(payload.Keyword)
            || x.Suffix.Contains(payload.Keyword);

        var query = GetQueryable(exp);
        var dataQuery = PaginatedQuerable(query, payload.Page, payload.Limit)
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            ;

        var employees = await dataQuery
            .Include(x => x.Skills)
            .Include(x => x.Dependents)
            .Include(x => x.Educations)
            .Include(x => x.Assets)
            .Include(x => x.EmployeeRecords)
            .Include(x => x.Employments)
            .ProjectToType<EmployeeFullModel>(_config)
            .ToListAsync(token);

        return new PaginatedResult<List<EmployeeFullModel>>()
        {
            Data = employees,
            MetaData = new PaginationMetaData(await query.CountAsync(), payload.Page, payload.Limit)
        };
    }
    public async Task<Employee?> FineOneAsync(Guid Id, CancellationToken token)
    {
        var result = await GetQueryable(x => x.Id == Id)
            .Include(x => x.RestDays)
            .FirstOrDefaultAsync(token)
            ;
        return result;
    }

    public async Task<List<EmployeeFilterResponseModel>> Filter(EmployeeFilter filter, CancellationToken token)
    {
        var result = await GetQueryable(x =>
            (filter.DayName == null || x.RestDays.Any(xx => xx.DayName == filter.DayName)) &&
            (filter.BranchId == null || x.BranchId == filter.BranchId.Value) &&
            (filter.EmployeeId == null || x.Id == filter.EmployeeId.Value) &&
            (filter.DepartmentId == null || x.DepartmentId == filter.DepartmentId.Value) &&
            (filter.ClientId == null || x.ClientId == filter.ClientId.Value) &&
            (filter.PayrollGroupId == null || x.PayrollGroupId == filter.PayrollGroupId.Value) &&
            (filter.OperationAreaId == null || x.AreaId == filter.OperationAreaId.Value))
            .ProjectToType<EmployeeFilterResponseModel>()
            .ToListAsync(token);

        return result;
    }


    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
}

