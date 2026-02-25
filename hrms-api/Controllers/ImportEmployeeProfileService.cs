using Ganss.Excel;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure.EntityConfig;

public class ImportEmployeeProfileService
{
    private readonly EmployeeService _employeeService;
    private readonly TimeShiftService _timeShiftService;
    private readonly BranchService _branchService;

    public event Action<string> OnMessage;
    public ImportEmployeeProfileService(
        EmployeeService employeeService,
        TimeShiftService timeShiftService,
        BranchService branchService)
    {
        _employeeService = employeeService;
        _timeShiftService = timeShiftService;
        _branchService = branchService;
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
    public void Upload(string path, int maxEmpCount, CancellationToken token)
    {
        var mapper = new ExcelMapper(path)
        {
            HeaderRowNumber = 0,
            MinRowNumber = 1,
        };

        MapFields(mapper);
        var data = mapper.Fetch<EmployeeImportModel>().ToList();
        SetDefaults(data);

        var branches = ExtractBranchesAsync(data, token);
        var shifts = ExtractShifts(data);
        var clients = ExtractClients(data);
        var pyGroups = ExtractPayrollGroups(data);
        var departments = ExtractDepartments(data, branches);
        var restDays = ExtractRestDay(data);

        StoreShift(shifts);
        StoreClients(clients);
        StorePayrollGroups(pyGroups);
        StoreDepartments(departments);

        List<Employee> employees = new List<Employee>();
        foreach (var item in data)
        {
            if (string.IsNullOrWhiteSpace(item.RestDay1)) item.RestDay1 = "";
            if (string.IsNullOrWhiteSpace(item.RestDay2)) item.RestDay2 = "";
        }
        var rests = new List<RestDay>();
        var progressPercent = data.Count / 100 - 1;

        foreach (var item in data)
        {
            var startTime = GetStartTime(item);
            var endTime = GetEndTime(item);
            var lunchOut = GetLunchOut(item);
            var lunchIn = GetLunchIn(item);
            var shiftKey = new ShiftKey(item.ShiftName, startTime, endTime, lunchOut, lunchIn);
            shifts.TryGetValue(shiftKey, out CreateTimeShift timeShift);
            clients.TryGetValue(item.ClientName, out Client client);
            pyGroups.TryGetValue(item.PayrollGroup, out PayrollGroup pg);
            departments.TryGetValue(item.DepartmentName, out Department? department);
            //int.TryParse(item.BioId, out int bioId);
            int bioId = item.BioId;

            var employee = new Employee()
            {
                FirstName = item?.FirstName ?? "",
                LastName = item?.LastName ?? "",
                MiddleName = item?.MiddleName ?? "",
                Suffix = item?.Suffix ?? "",
                Gender = item?.Gender ?? "Male",
                TimeShiftId = timeShift?.Id ?? Guid.Empty,
                ClientId = client?.Id ?? Guid.Empty,
                PayrollGroupId = pg?.Id ?? Guid.Empty,
                DepartmentId = department?.Id ?? Guid.Empty,
                BioId = bioId,
            };

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
            progressPercent += 1;
        }
        var Service = _employeeService;

        Service.AddRange(employees);
    }

    private List<Employee> CleanUp(List<Employee> employees)
    {
        if (employees == null) return [];
        return employees
            .GroupBy(x => x.BioId)
            .Select(x => x.First())
            .ToList();
    }

    public void StoreShift(Dictionary<ShiftKey, CreateTimeShift> shifts)
    {
        var models = shifts.Values.ToList();
        if (models == null || models.Count == 0) return;

        var existing = _timeShiftService.FindAll()
         .GroupBy(x => x.ShiftName)
         .ToDictionary(x => x.Key.ToLowerInvariant(), x => x.First().Id);

        var newRecords = models
            .Where(x => !existing.ContainsKey(x.ShiftName.Trim().ToLowerInvariant()))
            .ToList();

        foreach (var item in models)
        {
            if (existing.TryGetValue(item.ShiftName.Trim().ToLowerInvariant(), out Guid curId))
            {
                item.Id = curId;
            }
        }
        if (newRecords.Count > 0)
        {
            _timeShiftService.AddRange(newRecords);
        }
    }

    public void StoreClients(Dictionary<string, CreateClient> clients)
    {
        var codeCount = 1;
        foreach (var item in clients.Values)
        {
            item.Code = codeCount.FormatCode();
            codeCount += 1;
        }
        var models = clients.Values.ToList();
        if (models == null || models.Count == 0) return;

        var Service = new ClientService(_uow);
        var existing = Service.FindAll()
         .GroupBy(x => x.Name)
         .ToDictionary(x => x.Key.ToLowerInvariant(), x => x.First().Id);

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
            Service.AddRange(newRecords);
        }
    }
    public void StorePayrollGroups(Dictionary<string, PayrollGroup> pr)
    {
        var codeCount = 1;
        foreach (var item in pr.Values)
        {
            item.Code = codeCount.FormatCode();
            codeCount += 1;
        }

        var models = pr.Values.ToList();
        if (models == null || models.Count == 0) return;

        var Service = new PayrollGroupService(_uow);
        var existing = Service.FindAll()
         .GroupBy(x => x.Name)
         .ToDictionary(x => x.Key.ToLowerInvariant(), x => x.First().Id);


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
            Service.AddRange(newRecords);
        }

    }
    public void StoreDepartments(Dictionary<string, Department> depts)
    {
        var models = depts.Values.ToList();
        if (models == null || models.Count == 0) return;

        var codeCount = 1;
        foreach (var item in depts.Values)
        {
            item.Code = codeCount.FormatCode();
            codeCount += 1;
        }
        var Service = new DepartmentService(_uow);
        var existing = Service.FindAll()
         .GroupBy(x => x.Name)
         .ToDictionary(x => x.Key.ToLowerInvariant(), x => x.First().Id);

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
            Service.AddRange(newRecords);
        }
    }

    public async Task<List<(Guid Id,string Code)>> ExtractBranchesAsync(List<EmployeeImportModel> data, CancellationToken token)
    {
        HashSet<string> uniqueIds = data
            .Where(x=>!string.IsNullOrWhiteSpace(x.BranchCode))
            .Select(x => x.BranchCode)
            .Select(g => g)
            .ToHashSet();

        if (!uniqueIds.Any())
            return new List<(Guid Id, string Code)>();
        return await _branchService.GetByCodes(uniqueIds, token);
    }

    public Dictionary<ShiftKey, CreateTimeShift> ExtractShifts(List<EmployeeImportModel> data)
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
                    Value = new CreateTimeShift
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
    public Dictionary<string, CreateClient> ExtractClients(List<EmployeeImportModel> data)
    {
        return data
            .GroupBy(x => x.ClientName)
            .Select(g => new CreateClient { Name = string.IsNullOrWhiteSpace(g.Key) ? "--" : g.Key })
            .ToDictionary(x => x.Name, x => x);
        ;
    }
    public Dictionary<string, CreatePayrollGroup> ExtractPayrollGroups(List<EmployeeImportModel> data)
    {
        return data
            .GroupBy(x => new { x.PayrollGroup, x.CutoffDate1, x.CutoffDate2, x.PayrollFrequency })
            .Select(g => new CreatePayrollGroup
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

    private List<CutoffModel> ResolveCutoff(EmployeeImportModel item)
    {
        var pg = EnumParserConfig.SafeParseEnum(item.PayrollFrequency, PayrollFrequency.MONTHLY);
        if (pg == PayrollFrequency.DAILY) return new List<CutoffModel>();
        if (pg == PayrollFrequency.WEEKLY) return new List<CutoffModel>() { new CutoffModel() { Day = item.CutoffDate1, IsEndOfMonth = item.IsEndOfMonth1 } };
        if (pg == PayrollFrequency.MONTHLY) return new List<CutoffModel>() { new CutoffModel() { Day = item.CutoffDate1, IsEndOfMonth = item.IsEndOfMonth1 } };
        return new List<CutoffModel>()
        {
            new CutoffModel(){ Day= item.CutoffDate1, IsEndOfMonth= item.IsEndOfMonth1},
            new CutoffModel(){ Day= item.CutoffDate2, IsEndOfMonth= item.IsEndOfMonth2},
        };
    }

    public Dictionary<string, CreateDepartment> ExtractDepartments(
        List<EmployeeImportModel> data,
        List<Guid> branches)
    {
        var branchSet = branches.ToHashSet();
        return data
            .GroupBy(x => new
            {
                Name = string.IsNullOrWhiteSpace(x.DepartmentName) ? "--" : x.DepartmentName,
                BranchId = GetBranch(x, branchSet)
            })
            .Select(g => new CreateDepartment
            {
                Name = g.Key.Name,
                BranchId = g.Key.BranchId
            })
            .ToDictionary(x => $"{x.Name}_{x.BranchId}", x => x);
    }

    private Guid? GetBranch(EmployeeImportModel item, HashSet<Guid> branchSet)
    {
        if (Guid.TryParse(item.BranchId, out var parsedGuid) && branchSet.Contains(parsedGuid))
        {
            return parsedGuid;
        }
        return null;
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

