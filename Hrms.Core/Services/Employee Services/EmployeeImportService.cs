using AutoMapper;
using EFCore.BulkExtensions;
using Ganss.Excel;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure.EntityConfig;

namespace Hrms.Core.Services;

public class EmployeeImportService
{
    private readonly IMapper _mapper;
    private readonly EmployeeService _employeeService;
    private readonly TimeShiftService _timeShiftService;
    private readonly DepartmentService _departmentService;
    private readonly PayrollGroupService _payrollGroupService;
    private readonly ClientService _clientService;
    private readonly BranchService _branchService;

    public event Action<string> OnMessage;
    public EmployeeImportService(
        IMapper mapper,
        IUnitOfWorkService uow,
        EmployeeService employeeService,
        TimeShiftService timeShiftService,
        DepartmentService departmentService,
        PayrollGroupService payrollGroupService,
        ClientService clientService,
        BranchService branchService)
    {
        _mapper = mapper;
        _employeeService = employeeService;
        _timeShiftService = timeShiftService;
        _departmentService = departmentService;
        _payrollGroupService = payrollGroupService;
        _clientService = clientService;
        _branchService = branchService;
    }
    public async Task Upload(Stream fileStream, CancellationToken token)
    {
        var mapper = new ExcelMapper(fileStream)
        {
            HeaderRowNumber = 0,
            MinRowNumber = 1,
        };
        MapFields(mapper);
        var data = mapper.Fetch<EmployeeImportModel>().ToList();
        var allEmployees = await _employeeService.GetQueryable()
           .Select(x => new BasicEmployeeInfo
           {
               Id = x.Id,
               BioId = x.BioId,
               FirstName = x.FirstName,
               MiddleName = x.MiddleName,
               LastName = x.LastName,
               Suffix = x.Suffix
           }).ToListAsync(token);

        ValidateImportData(data, allEmployees);
        SetDefaults(data);

        var branches = await ExtractBranchesAsync(data, token);
        var shifts = ExtractShifts(data);
        var clients = ExtractClients(data);
        var pyGroups = ExtractPayrollGroups(data);
        var departments = ExtractDepartments(data);
        var restDays = ExtractRestDay(data);

        await StoreShiftAsync(shifts, token);
        await StoreClientsAsync(clients, token);
        await StorePayrollGroupsAsync(pyGroups, token);
        await StoreDepartmentsAsync(departments, token);
        await _departmentService.SaveChangesAsync(token);

        //store employees
        List<Employee> employees = new List<Employee>();
        foreach (var item in data)
        {
            if (string.IsNullOrWhiteSpace(item.RestDay1)) item.RestDay1 = "";
            if (string.IsNullOrWhiteSpace(item.RestDay2)) item.RestDay2 = "";
        }
        var rests = new List<RestDay>();

        foreach (var item in data)
        {
            var startTime = GetStartTime(item);
            var endTime = GetEndTime(item);
            var lunchOut = GetLunchOut(item);
            var lunchIn = GetLunchIn(item);

            var shiftKey = new ShiftKey(item.ShiftName, startTime, endTime, lunchOut, lunchIn);
            shifts.TryGetValue(shiftKey, out TimeShift? timeShift);
            clients.TryGetValue(item.ClientName, out Client? client);
            pyGroups.TryGetValue(item.PayrollGroup, out PayrollGroup? pg);
            departments.TryGetValue(item.DepartmentName, out Department? department);
            var branch = branches.FirstOrDefault(x => x.Code == item.BranchCode);
            Guid? BranchId = branch.Equals(default) ? branches.FirstOrDefault().Id : branch.Id;
            if (pg == null) pg = pyGroups.Values.FirstOrDefault();

            int bioId = item.BioId;
            var employee = new Employee()
            {
                FirstName = item?.FirstName ?? "",
                LastName = item?.LastName ?? "",
                MiddleName = item?.MiddleName ?? "",
                Suffix = item?.Suffix ?? "",
                Gender = item?.Gender ?? "Male",
                TimeShiftId = timeShift?.Id,
                ClientId = client?.Id,
                PayrollGroupId = pg?.Id ?? Guid.Empty,
                DepartmentId = department?.Id,
                BioId = bioId,
                BranchId = BranchId,
            };

            var existing = allEmployees
                .FirstOrDefault(x => x.FirstName == employee.FirstName &&
                x.MiddleName == employee.MiddleName &&
                x.LastName == employee.LastName &&
                x.Suffix == employee.Suffix)
                ;
            if (existing != null)
            {
                employee.Id = existing.Id;
            }
            employee.RestDays.Clear();
            if (!string.IsNullOrWhiteSpace(item.RestDay1) && restDays.TryGetValue(item.RestDay1, out var r1))
            {
                employee.RestDays.Add(new RestDay
                {
                    DayName = r1.DayName,
                    EmployeeId = employee.Id
                });
            }
            if (!string.IsNullOrWhiteSpace(item.RestDay2) && restDays.TryGetValue(item.RestDay2, out var r2))
            {
                employee.RestDays.Add(new RestDay
                {
                    DayName = r2.DayName,
                    EmployeeId = employee.Id
                });
            }
            employees.Add(employee);
        }

        await _employeeService.BulkInsertOrUpdateAsync(employees, true, (x) =>
                x.UpdateByProperties = new List<string> { nameof(Employee.BioId) }
            , token);

        var allBioIds = employees.Select(e => e.BioId).ToList();
        var savedEmployees = await _employeeService.GetQueryable()
            .Where(e => allBioIds.Contains(e.BioId))
            .Select(e => new { e.Id, e.BioId })
            .ToListAsync(token);
        var bioIdToGuidMap = savedEmployees.ToDictionary(k => k.BioId, v => v.Id);

        var allNewRestDays = new List<RestDay>();
        foreach (var emp in employees)
        {
            if (bioIdToGuidMap.TryGetValue(emp.BioId, out var empGuid))
            {
                foreach (var rd in emp.RestDays)
                {
                    rd.EmployeeId = empGuid;
                    allNewRestDays.Add(rd);
                }
            }
        }

        var employeeIds = bioIdToGuidMap.Values.ToList();
        await _employeeService.Context.RestDays
          .Where(rd => employeeIds.Contains(rd.EmployeeId))
          .ExecuteDeleteAsync(token);

        await _employeeService.Context.BulkInsertAsync(allNewRestDays,null,null,null, token);
        await _employeeService.CommitChangesAsync(token);
    }

    private void SetDefaults(List<EmployeeImportModel> data)
    {
        foreach (var item in data)
        {
            if (string.IsNullOrWhiteSpace(item.ShiftName))
            {
                item.ShiftName = "Day Shift";
            }
            if (string.IsNullOrWhiteSpace(item.ShiftType))
            {
                item.ShiftType = "Fix";
            }
            if (string.IsNullOrWhiteSpace(item.AMIn))
            {
                item.AMIn = "8:00:00";
                item.PMOut = "17:00:00";
            }
            if (string.IsNullOrWhiteSpace(item.DepartmentName))
            {
                item.DepartmentName = "--";
            }
            if (string.IsNullOrWhiteSpace(item.PayrollGroup))
            {
                item.PayrollGroup = "--";
            }
            if (string.IsNullOrWhiteSpace(item.ClientName))
            {
                item.ClientName = "--";
            }
            if (string.IsNullOrWhiteSpace(item.RestDay1))
            {
                item.RestDay1 = "";
            }
            if (string.IsNullOrWhiteSpace(item.RestDay2))
            {
                item.RestDay2 = "";
            }
        }
    }
    private void MapFields(ExcelMapper mapper)
    {
        mapper.AddMapping<EmployeeImportModel>("BioId", p => p.BioId);
        mapper.AddMapping<EmployeeImportModel>("BranchCode", p => p.BranchCode);
        mapper.AddMapping<EmployeeImportModel>("LastName", p => p.LastName);
        mapper.AddMapping<EmployeeImportModel>("FirstName", p => p.FirstName);
        mapper.AddMapping<EmployeeImportModel>("MiddleName", p => p.MiddleName);
        mapper.AddMapping<EmployeeImportModel>("Suffix", p => p.Suffix);
        mapper.AddMapping<EmployeeImportModel>("Gender", p => p.Gender);
        mapper.AddMapping<EmployeeImportModel>("Rest Day 1", p => p.RestDay1);
        mapper.AddMapping<EmployeeImportModel>("Rest Day 2", p => p.RestDay2);
        mapper.AddMapping<EmployeeImportModel>("DepartmentName", p => p.DepartmentName);
        mapper.AddMapping<EmployeeImportModel>("ClientName", p => p.ClientName);
        mapper.AddMapping<EmployeeImportModel>("Payroll Group", p => p.PayrollGroup);
        mapper.AddMapping<EmployeeImportModel>("Shift Name", p => p.ShiftName);
        mapper.AddMapping<EmployeeImportModel>("Type", p => p.ShiftType);
        mapper.AddMapping<EmployeeImportModel>("AMIN", p => p.AMIn);
        mapper.AddMapping<EmployeeImportModel>("Noon BreakOut", p => p.NoonBreakOut);
        mapper.AddMapping<EmployeeImportModel>("Break-IN", p => p.NoonBreakIn);
        mapper.AddMapping<EmployeeImportModel>("PM OUT", p => p.PMOut);
        mapper.AddMapping<EmployeeImportModel>("Break Duration", p => p.BreakDuration);
        mapper.AddMapping<EmployeeImportModel>("Max Working Minutes", p => p.MaxWorkingMinutes);
        mapper.AddMapping<EmployeeImportModel>("PaidLunchBreak", p => p.PaidLunchBreak);
        mapper.AddMapping<EmployeeImportModel>("PayrollFrequency", p => p.PayrollFrequency);
        mapper.AddMapping<EmployeeImportModel>("CutoffDate1", p => p.CutoffDate1);
        mapper.AddMapping<EmployeeImportModel>("CutoffDate2", p => p.CutoffDate2);
    }
    private void ValidateImportData(List<EmployeeImportModel> data, List<BasicEmployeeInfo> dbEmployees)
    {
        var errors = new List<string>();

        var internalDuplicates = data
            .GroupBy(x => x.BioId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (internalDuplicates.Any())
        {
            errors.Add($"The Excel file contains duplicate BioIds: {string.Join(", ", internalDuplicates)}");
        }
        var dbEmployeeMap = dbEmployees.ToDictionary(x => x.BioId);
        foreach (var item in data)
        {
            if (dbEmployeeMap.TryGetValue(item.BioId, out var existing))
            {
                // 3. Compare Names: If they are different, it's a conflict
                if (!NamesMatch(item, existing))
                {
                    errors.Add($"BioId {item.BioId} is already registered to '{existing.FirstName} {existing.LastName}', " +
                               $"but the import file lists it as '{item.FirstName} {item.LastName}'.");
                }
            }
        }

        if (errors.Any())
        {
            throw new ValidationException($"Import Validation Failed:\n{string.Join("\n", errors)}");
        }
    }
    //private void GuardBiodId(List<BasicEmployeeInfo> dbEmployees, List<Employee> newEmployees)
    //{
    //    var newBioIds = CleanUp(newEmployees)
    //        .Select(x => x.BioId)
    //        .ToHashSet();

    //    var duplicateEmployees = dbEmployees
    //        .Where(x => newBioIds.Contains(x.BioId))
    //        .ToList();

    //    if (duplicateEmployees.Any())
    //    {
    //        var existingIds = string.Join(", ", duplicateEmployees.Select(x => x.BioId));
    //        throw new Exception($"The following BioIds already exist in the database: {existingIds}");
    //    }
    //}
    //private List<Employee> CleanUp(List<Employee> employees)
    //{
    //    if (employees == null) return new List<Employee>();
    //    return employees
    //        .GroupBy(x => x.BioId)
    //        .Select(g => g.First())
    //        .ToList();
    //}

    private bool NamesMatch(EmployeeImportModel import, BasicEmployeeInfo db)
    {

        return string.Equals(import.FirstName?.Trim(), db.FirstName?.Trim(), StringComparison.OrdinalIgnoreCase) &&
               string.Equals(import.LastName?.Trim(), db.LastName?.Trim(), StringComparison.OrdinalIgnoreCase) &&
               string.Equals(import.MiddleName?.Trim(), db.MiddleName?.Trim(), StringComparison.OrdinalIgnoreCase) &&
               string.Equals(import.Suffix?.Trim(), db.Suffix?.Trim(), StringComparison.OrdinalIgnoreCase);
    }



    private async Task StoreShiftAsync(Dictionary<ShiftKey, TimeShift> shifts, CancellationToken token)
    {
        if (shifts == null || !shifts.Any()) return;
        var models = _mapper.Map<List<TimeShift>>(shifts.Values.ToList());
        if (models == null || models.Count == 0) return;

        var existing = await _timeShiftService.GetQueryable()
         .GroupBy(x => x.ShiftName)
         .ToDictionaryAsync(x => x.Key.ToLowerInvariant(), x => x.First().Id, token);

        var newRecords = models
            .Where(x => !existing.ContainsKey(x.ShiftName.Trim().ToLowerInvariant()))
            .ToList();

        foreach (var item in models)
        {
            if (existing.TryGetValue(item.ShiftName.Trim().ToLowerInvariant(), out Guid curId))
            {
                item.Id = curId;    //set the ids for tagging at the next process
            }
        }
        if (newRecords.Count > 0)
        {
            await _timeShiftService.Repository.AddRangeAsync(newRecords, token);
        }
    }
    private async Task StoreClientsAsync(Dictionary<string, Client> clients, CancellationToken token)
    {
        var codeCount = 1;
        foreach (var item in clients.Values)
        {
            item.Code = codeCount.FormatCode();
            codeCount += 1;
        }
        var models = clients.Values.ToList();
        if (models == null || models.Count == 0) return;

        var existing = await _clientService.GetQueryable()
         .GroupBy(x => x.Name)
         .ToDictionaryAsync(x => x.Key.ToLowerInvariant(), x => x.First().Id, token);

        var newRecords = models
            .Where(x => !existing.ContainsKey(x.Name.Trim().ToLowerInvariant()))
            .ToList();


        foreach (var item in models)
        {
            if (existing.TryGetValue(item.Name.Trim().ToLowerInvariant(), out Guid curId))
            {
                item.Id = curId;
            }
        }

        if (newRecords.Count > 0)
        {
            await _clientService.AddRangeAsync(newRecords, token);
        }

    }
    private async Task StorePayrollGroupsAsync(Dictionary<string, PayrollGroup> pr, CancellationToken token)
    {
        var codeCount = 1;
        foreach (var item in pr.Values)
        {
            item.Code = codeCount.FormatCode();
            codeCount += 1;
        }

        var models = pr.Values.ToList();
        if (models == null || models.Count == 0)
        {
            throw new Exception("Payroll group is required");
        }

        var existing = await _payrollGroupService.GetQueryable()
         .GroupBy(x => x.Name)
         .ToDictionaryAsync(x => x.Key.ToLowerInvariant(), x => x.First().Id, token);

        var newRecords = models
            .Where(x => !existing.ContainsKey(x.Name.Trim().ToLowerInvariant()))
            .ToList();

        foreach (var item in models)
        {
            if (existing.TryGetValue(item.Name.Trim().ToLowerInvariant(), out Guid curId))
            {
                item.Id = curId;
            }
        }
        if (newRecords.Count > 0)
        {
            await _payrollGroupService.Repository.AddRangeAsync(newRecords, token);
        }

    }
    private async Task StoreDepartmentsAsync(Dictionary<string, Department> depts, CancellationToken token)
    {
        var models = depts.Values.ToList();
        if (models == null || models.Count == 0) return;

        var codeCount = 1;
        foreach (var item in depts.Values)
        {
            item.Code = codeCount.FormatCode();
            codeCount += 1;
        }
        var existing = await _departmentService.GetQueryable()
         .GroupBy(x => x.Name)
         .ToDictionaryAsync(x => x.Key.ToLowerInvariant(), x => x.First().Id, token);

        var newRecords = models
            .Where(x => !existing.ContainsKey(x.Name.Trim().ToLowerInvariant()))
            .ToList();

        foreach (var item in models)
        {
            if (existing.TryGetValue(item.Name.Trim().ToLowerInvariant(), out Guid curId))
            {
                item.Id = curId;
            }
        }
        if (newRecords.Count > 0)
        {
            await _departmentService.Repository.AddRangeAsync(newRecords, token);
        }
    }
    private async Task<List<(Guid Id, string Code)>> ExtractBranchesAsync(List<EmployeeImportModel> data, CancellationToken token)
    {
        HashSet<string> uniqueIds = data
            .Where(x => !string.IsNullOrWhiteSpace(x.BranchCode))
            .Select(x => x.BranchCode)
            .Select(g => g)
            .ToHashSet();

        if (!uniqueIds.Any())
            return new List<(Guid Id, string Code)>();
        return await _branchService.GetByCodes(uniqueIds, token);
    }
    private Dictionary<ShiftKey, TimeShift> ExtractShifts(List<EmployeeImportModel> data)
    {
        return data
            .GroupBy(x => new { x.ShiftName, x.AMIn, x.PMOut, x.NoonBreakOut, x.NoonBreakIn })
            .Select(group =>
            {
                var x = group.First();
                var startTime = GetStartTime(x);
                var endTime = GetEndTime(x);
                var lunchOut = GetLunchOut(x);
                var lunchIn = GetLunchIn(x);

                var shiftType = (x.ShiftType?.Contains("Fix") == true || x.ShiftType?.Contains("Fixed") == true) ? TimeShiftType.FIXED : TimeShiftType.FLEXI;
                var hasBreak = lunchOut != TimeSpan.Zero && lunchIn != TimeSpan.Zero;
                var maxWorkingMinutes = x.MaxWorkingMinutes;
                var breakDuration = x.BreakDuration > 0
                        ? x.BreakDuration
                        : hasBreak
                            ? Math.Max(0, lunchIn.Subtract(lunchOut).TotalMinutes)
                            : 0;

                return new
                {
                    Key = new ShiftKey(x.ShiftName, startTime, endTime, lunchOut, lunchIn),
                    Value = new TimeShift
                    {
                        ShiftType = string.IsNullOrEmpty(x.ShiftType) ? TimeShiftType.FIXED : shiftType,
                        ShiftName = $"{x.ShiftName}-{x.AMIn}-{x.PMOut}" + (hasBreak ? "-WB" : ""),
                        StartTime = startTime,
                        EndTime = endTime,
                        LunchStartTime = lunchOut,
                        LunchEndTime = lunchIn,
                        BreakDurationMinutes = breakDuration,
                        WithLunchBreak = x.PaidLunchBreak ? BreakMode.PAID_BREAK : BreakMode.UNPAID_BREAK,
                        MaxWorkingMinutes = maxWorkingMinutes,
                    }
                };
            })
            .GroupBy(x => x.Key)
            .Select(g => g.First())
            .ToDictionary(x => x.Key, x => x.Value);
    }
    //UTILITY
    private TimeSpan GetStartTime(EmployeeImportModel x)
    {
        return TimeSpan.TryParse(x.AMIn, out var amIn) ? amIn : new TimeSpan(8, 0, 0);
    }
    private TimeSpan GetEndTime(EmployeeImportModel x)
    {
        return TimeSpan.TryParse(x.PMOut, out var pmOut) ? pmOut : new TimeSpan(17, 0, 0);
    }
    //private TimeSpan GetEndTime(EmployeeImportModel x)
    //{
    //    TimeSpan.TryParse(x.PMOut, out var pmOut);
    //    return string.IsNullOrEmpty(x.ShiftType) ? new TimeSpan(17, 0, 0) : pmOut;
    //}
    private TimeSpan GetLunchOut(EmployeeImportModel x)
    {
        TimeSpan.TryParse(x.NoonBreakOut, out var lunchOut);
        return lunchOut;
    }
    private TimeSpan GetLunchIn(EmployeeImportModel x)
    {
        TimeSpan.TryParse(x.NoonBreakIn, out var lunchOut);
        return lunchOut;
    }
    private Dictionary<string, Client> ExtractClients(List<EmployeeImportModel> data)
    {
        return data
            .GroupBy(x => x.ClientName)
            .Select(g => new Client { Name = string.IsNullOrWhiteSpace(g.Key) ? "--" : g.Key })
            .ToDictionary(x => x.Name, x => x);
        ;
    }
    private Dictionary<string, PayrollGroup> ExtractPayrollGroups(List<EmployeeImportModel> data)
    {
        return data
            .GroupBy(x => new { x.PayrollGroup, x.CutoffDate1, x.CutoffDate2, x.PayrollFrequency })
            .Select(g => new PayrollGroup
            {
                PayrollFrequency = ResolveFrequency(g.First()),
                CutoffDays = ResolveCutoff(g.First()),
                Name = string.IsNullOrWhiteSpace(g.First().PayrollGroup) ? "--" : g.First().PayrollGroup
            })
            .ToDictionary(x => x.Name, x => x);
        ;
    }
    private PayrollFrequency ResolveFrequency(EmployeeImportModel item)
    {
        return EnumParserConfig.SafeParseEnum(item.PayrollFrequency, PayrollFrequency.MONTHLY);
    }
    private List<CutoffDay> ResolveCutoff(EmployeeImportModel item)
    {
        var pg = EnumParserConfig.SafeParseEnum(item.PayrollFrequency, PayrollFrequency.MONTHLY);
        if (pg == PayrollFrequency.DAILY) return new List<CutoffDay>();
        if (pg == PayrollFrequency.WEEKLY) return new List<CutoffDay>() { new CutoffDay() { Day = item.CutoffDate1, IsEndOfMonth = item.IsEndOfMonth1 } };
        if (pg == PayrollFrequency.MONTHLY) return new List<CutoffDay>() { new CutoffDay() { Day = item.CutoffDate1, IsEndOfMonth = item.IsEndOfMonth1 } };
        return new List<CutoffDay>()
        {
            new CutoffDay(){ Day= item.CutoffDate1, IsEndOfMonth= item.IsEndOfMonth1},
            new CutoffDay(){ Day= item.CutoffDate2, IsEndOfMonth= item.IsEndOfMonth2},
        };
    }
    private Dictionary<string, Department> ExtractDepartments(List<EmployeeImportModel> data)
    {
        return data
            .GroupBy(x => string.IsNullOrWhiteSpace(x.DepartmentName) ? "--" : x.DepartmentName.Trim())
            .Select(g => new Department
            {
                Name = g.Key,
            })
            .ToDictionary(x => x.Name, x => x);
    }

    private Guid? GetBranch(EmployeeImportModel item, Dictionary<string, Guid> branchLookup)
    {
        if (string.IsNullOrWhiteSpace(item.BranchCode))
            return null;

        // TryGetValue is the safest and fastest way to check a dictionary
        return branchLookup.TryGetValue(item.BranchCode.Trim(), out var branchId)
            ? branchId
            : null;
    }
    public Dictionary<string, RestDay> ExtractRestDay(List<EmployeeImportModel> data)
    {
        var r1 = data
            .Where(x => !string.IsNullOrWhiteSpace(x.RestDay1))
            .Select(x => x.RestDay1)
            .ToList();

        var r2 = data
            .Where(x => !string.IsNullOrWhiteSpace(x.RestDay2))
            .Select(x => x.RestDay2)
            .ToList();

        // Combine all rest days into r3
        var r3 = new List<string>();
        r3.AddRange(r1);
        r3.AddRange(r2);

        var restDays = r3
            .Where(x => x != null)
            .Distinct()
            .ToDictionary(
                day => day,
                day =>
                {
                    DayName result = Enum.TryParse(day, true, out DayName parsedDay)
                        ? parsedDay
                        : DayName.Saturday;

                    return new RestDay
                    {
                        DayName = result
                    };
                });

        return restDays;
    }
}
public class BasicEmployeeInfo
{
    public Guid Id { get; set; }
    public int BioId { get; set; }
    public string FirstName { get; set; }
    public string MiddleName { get; set; }
    public string LastName { get; set; }
    public string Suffix { get; set; }
}
public class EmployeeImportModel
{
    public int BioId { get; set; }
    public string BranchCode { get; set; }
    public string FirstName { get; set; }
    public string MiddleName { get; set; }
    public string LastName { get; set; }
    public string Suffix { get; set; }
    public string Gender { get; set; }
    public string RestDay1 { get; set; }
    public string RestDay2 { get; set; }

    public string DepartmentName { get; set; }
    public string ClientName { get; set; }
    public string PayrollGroup { get; set; }
    public string ShiftName { get; set; }
    public string ShiftType { get; set; }
    //public string CrossDate { get; set; }
    public string AMIn { get; set; }
    public string PMOut { get; set; }
    public string NoonBreakOut { get; set; }
    public string NoonBreakIn { get; set; }
    public bool PaidLunchBreak { get; set; }

    public double BreakDuration { get; set; }
    public double MaxWorkingMinutes { get; set; }

    //for payrollgroup
    public string PayrollFrequency { get; set; }
    public int CutoffDate1 { get; set; }
    public bool IsEndOfMonth1 { get; set; }
    public int CutoffDate2 { get; set; }
    public bool IsEndOfMonth2 { get; set; }
}
public record struct ShiftKey(string ShiftName, TimeSpan am, TimeSpan pm, TimeSpan? l1, TimeSpan? l2);
