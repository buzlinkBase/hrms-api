using AutoMapper;
using AutoMapper.QueryableExtensions;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using System.Linq.Expressions;

namespace Hrms.Core.Services;

public class EmployeeService : BaseService<Employee>
{
    private readonly IMapper _mapper;
    private readonly DepartmentService _departmentService;
    private readonly PayrollGroupService _payrollGroupService;
    private readonly BranchService _branchService;
    private readonly SectionService _sectionService;

    public EmployeeService(IUnitOfWorkService uow,
        IMapper mapper,
        DepartmentService departmentService,
        PayrollGroupService payrollGroupService,
        BranchService branchService,
        AreaService areaService,
        PositionService positionService,
        SectionService sectionService

        ) : base(uow)
    {
        _mapper = mapper;
        _departmentService = departmentService;
        _payrollGroupService = payrollGroupService;
        _branchService = branchService;
        _sectionService = sectionService;
    }

    protected override async Task<EvaluationResult> CreateValidatorAsync(Employee model,
        CancellationToken token)
    {
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
        if (model.HireDate == DateOnly.MinValue)
        {
            model.HireDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }
        var branch = _uow.Repository.FindOne<Branch>(model.BranchId ?? Guid.Empty);
        if (branch != null)
        {
            model.BranchId = branch.Id;
        }
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task UpdateAsync(Employee model, CancellationToken token)
    {
        if (model.HireDate == DateOnly.MinValue)
        {
            model.HireDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }
        var branch = _uow.Repository.FindOne<Branch>(model.BranchId ?? Guid.Empty);
        if (branch != null)
        {
            model.BranchId = branch.Id;
        }
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);

    }

    //public async Task AddOrUpdateAsync(Employee model,
    //    CancellationToken token)
    //{
    //    if (model.HireDate == DateOnly.MinValue)
    //    {
    //        model.HireDate = DateOnly.FromDateTime(DateTime.UtcNow);
    //    }
    //    var branch = _uow.Repository.FindOne<Branch>(model.BranchId ?? Guid.Empty);
    //    if (branch != null)
    //    {

    //        model.BranchId = branch.Id;
    //    }
    //    await CreateOrUpdateAsync(model, token);
    //}

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

    public async Task<List<Employee>> FindAll(CancellationToken token)
    {
        var data = await GetQueryable()
            .ToListAsync(token);
        return data;
    }

    public async Task<List<EmployeeModel>> GetAll( CancellationToken token) 
    {
        var query = GetQueryable();
        var employees = await query
            .ProjectTo<EmployeeModel>(_mapper.ConfigurationProvider)
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
        var dataQuery = PaginatedQuerable(query, payload.Page, payload.Limit);
        var employees = await dataQuery
            .ProjectTo<EmployeeModel>(_mapper.ConfigurationProvider)
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
        var dataQuery = PaginatedQuerable(query, payload.Page, payload.Limit);

        var employees = await dataQuery
            .Include(x => x.Skills)
            .Include(x => x.Dependents)
            .Include(x => x.Educations)
            .Include(x => x.Assets)
            .Include(x => x.EmployeeRecords)
            .Include(x => x.Employments)
            .ProjectTo<EmployeeFullModel>(_mapper.ConfigurationProvider)
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

    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
}

