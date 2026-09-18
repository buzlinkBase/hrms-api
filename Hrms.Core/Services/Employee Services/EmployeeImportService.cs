using ClosedXML.Excel;
using Ganss.Excel;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Mapster;
using Microsoft.AspNetCore.Hosting;


namespace Hrms.Core.Services;

public class EmployeeImportService
{
    private readonly IUnitOfWorkService _uow;
    private readonly EmployeeService _employeeService;
    private readonly TimeShiftService _timeShiftService;
    private readonly DepartmentService _departmentService;
    private readonly PayrollGroupService _payrollGroupService;
    private readonly ClientService _clientService;
    private readonly BranchService _branchService;

    public event Action<string> OnMessage;
    public EmployeeImportService(
        IUnitOfWorkService uow,
        EmployeeService employeeService,
        TimeShiftService timeShiftService,
        DepartmentService departmentService,
        PayrollGroupService payrollGroupService,
        ClientService clientService,
        BranchService branchService)
    {
        _uow = uow;
        _employeeService = employeeService;
        _timeShiftService = timeShiftService;
        _departmentService = departmentService;
        _payrollGroupService = payrollGroupService;
        _clientService = clientService;
        _branchService = branchService;
    }
    public async Task Upload(Stream fileStream, CancellationToken token)
    {
        var (data, allEmployees) = await ParseAndDefaultAsync(fileStream, token);
        ValidateImportData(data, allEmployees);
        var shifts = ExtractShifts(data);
        var branches = await ExtractBranchesAsync(data, token);
        var clients = ExtractClients(data);
        var pyGroups = ExtractPayrollGroups(data);
        var departments = ExtractDepartments(data);
        var restDays = ExtractRestDay(data);

        await StoreShiftAsync(shifts, token);
        await StoreClientsAsync(clients, token);
        await StorePayrollGroupsAsync(pyGroups, token);
        await StoreDepartmentsAsync(departments, token);
        await _uow.SaveChangesAsync(token);

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
            if (item == null) continue;
            var startTime = GetStartTime(item);
            var endTime = GetEndTime(item);
            var lunchOut = GetLunchOut(item);
            var lunchIn = GetLunchIn(item);

            var sanitizedShiftName = item.ShiftName?.Trim() ?? string.Empty;
            var shiftKey = new ShiftKey(sanitizedShiftName, startTime, endTime, lunchOut, lunchIn);
            shifts.TryGetValue(shiftKey, out TimeShift? timeShift);
            // SetDefaults already guarantees these are "--" rather than blank/null by this point.
            clients.TryGetValue(item.ClientName!, out Client? client);
            pyGroups.TryGetValue(item.PayrollGroup!, out PayrollGroup? pg);
            departments.TryGetValue(item.DepartmentName!, out Department? department);
            var branch = branches.FirstOrDefault(x => x.Code == item.BranchCode);
            Guid? BranchId = !branches.Any() ? null : branch.Equals(default) ? branches.FirstOrDefault().Id : branch.Id;
            if (pg == null) pg = pyGroups.Values.FirstOrDefault();

            int.TryParse(item.BioId, out var bioIdNo);
            int? bioId = bioIdNo == 0 ? null : bioIdNo;

            var employee = new Employee()
            {
                FirstName = item.FirstName ?? "",
                LastName = item.LastName ?? "",
                MiddleName = item.MiddleName ?? "",
                Suffix = item.Suffix ?? "",
                Gender = item.Gender ?? "Male",
                TimeShiftId = timeShift?.Id,
                ClientId = client?.Id,
                PayrollGroupId = pg?.Id ?? Guid.Empty,
                DepartmentId = department?.Id,
                BioId = bioId,
                BranchId = BranchId,
                SSSNo = item.SSS ?? "",
                PHICNo = item.PHIC ?? "",
                HDMFNo = item.HDMF ?? "",
                DailyRate = item.DailyRate,
                HireDate = item.HireDate == null ? DateOnly.FromDateTime(DateTime.UtcNow) : item.HireDate.Value,
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
        var empIds = employees.Select(x => x.Id).ToList();
        var rds = _employeeService.Context.RestDays
            .Where(x => empIds.Contains(x.EmployeeId))
            .ToList();
        _employeeService.Repository.RemoveRange(rds);
        await _employeeService.AddOrUpdateRange(employees, token);
        await _employeeService.SaveChangesAsync(token);
        await _uow.CommitChangesAsync("", token);
    }

    private async Task<(List<EmployeeImportModel> Data, List<BasicEmployeeInfo> DbEmployees)> ParseAndDefaultAsync(Stream fileStream, CancellationToken token)
    {
        var mapper = new ExcelMapper(fileStream)
        {
            HeaderRowNumber = 1,
            MinRowNumber = 2,
        };
        MapFields(mapper);
        var data = mapper.Fetch<EmployeeImportModel>().ToList();
        var allEmployees = await GetAllEmployees(token);
        SetDefaults(data);
        return (data, allEmployees);
    }

    /// <summary>
    /// Read-only counterpart to <see cref="Upload"/>: parses and defaults the file exactly the
    /// same way, but never writes anything -- no reference-entity creation, no SaveChanges, no
    /// commit. Lets the caller show the user what will be imported (and any per-row conflicts)
    /// before they commit to <see cref="Upload"/>.
    /// </summary>
    public async Task<List<EmployeeImportPreviewRow>> PreviewAsync(Stream fileStream, CancellationToken token)
    {
        var (data, allEmployees) = await ParseAndDefaultAsync(fileStream, token);

        var internalDuplicateBioIds = data
            .Where(x => !string.IsNullOrWhiteSpace(x.BioId) && x.BioId != "0")
            .GroupBy(x => x.BioId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        var dbEmployeeMap = allEmployees
            .Where(x => x.BioId.HasValue && x.BioId.Value > 0)
            .GroupBy(x => x.BioId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

        var rows = new List<EmployeeImportPreviewRow>();
        for (var i = 0; i < data.Count; i++)
        {
            var item = data[i];
            var row = item.Adapt<EmployeeImportPreviewRow>();
            row.RowNumber = i + 1;
            row.Errors = BuildRowErrors(item, dbEmployeeMap, internalDuplicateBioIds);
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>
    /// Per-row version of the conflict checks <see cref="ValidateImportData"/> already does in
    /// aggregate for the batch-throw path -- used only by <see cref="PreviewAsync"/>, so it can
    /// attribute an error to the specific row(s) it affects instead of one combined message.
    /// </summary>
    private List<string> BuildRowErrors(EmployeeImportModel item, Dictionary<int, BasicEmployeeInfo> dbEmployeeMap, HashSet<string?> internalDuplicateBioIds)
    {
        var errors = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.BioId) && item.BioId != "0" && internalDuplicateBioIds.Contains(item.BioId))
        {
            errors.Add($"BioId {item.BioId} is used by more than one row in this file.");
        }
        if (int.TryParse(item.BioId, out var bioIdNo) && bioIdNo > 0 &&
            dbEmployeeMap.TryGetValue(bioIdNo, out var existing) && !NamesMatch(item, existing))
        {
            errors.Add($"BioId {bioIdNo} is already registered to '{existing.FirstName} {existing.LastName}', " +
                       $"but this file lists it as '{item.FirstName} {item.LastName}'.");
        }
        return errors;
    }

    private async Task<List<BasicEmployeeInfo>> GetAllEmployees(CancellationToken token)
    {
        return await _employeeService.GetQueryable()
           .Select(x => new BasicEmployeeInfo
           {
               Id = x.Id,
               BioId = x.BioId,
               FirstName = x.FirstName,
               MiddleName = x.MiddleName,
               LastName = x.LastName,
               Suffix = x.Suffix,
           }).ToListAsync(token);
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
            if (item.HireDate == null)
            {
                item.HireDate = DateOnly.FromDateTime(DateTime.UtcNow);
            }
        }
    }
    private void MapFields(ExcelMapper mapper)
    {
        mapper.AddMapping<EmployeeImportModel>("BioId", p => p.BioId);
        mapper.AddMapping<EmployeeImportModel>("Branch Code", p => p.BranchCode);
        mapper.AddMapping<EmployeeImportModel>("FirstName", p => p.FirstName);
        mapper.AddMapping<EmployeeImportModel>("MiddleName", p => p.MiddleName);
        mapper.AddMapping<EmployeeImportModel>("LastName", p => p.LastName);
        mapper.AddMapping<EmployeeImportModel>("Suffix", p => p.Suffix);
        mapper.AddMapping<EmployeeImportModel>("Gender", p => p.Gender);
        mapper.AddMapping<EmployeeImportModel>("Email", p => p.Email);

        mapper.AddMapping<EmployeeImportModel>("Rest Day 1", p => p.RestDay1);
        mapper.AddMapping<EmployeeImportModel>("Rest Day 2", p => p.RestDay2);
        mapper.AddMapping<EmployeeImportModel>("Department Name", p => p.DepartmentName);
        mapper.AddMapping<EmployeeImportModel>("Client Name", p => p.ClientName);
        mapper.AddMapping<EmployeeImportModel>("Payroll Group", p => p.PayrollGroup);
        mapper.AddMapping<EmployeeImportModel>("Shift Name", p => p.ShiftName);
        mapper.AddMapping<EmployeeImportModel>("Shift Type", p => p.ShiftType);
        mapper.AddMapping<EmployeeImportModel>("AMIN", p => p.AMIn);
        mapper.AddMapping<EmployeeImportModel>("AM Out", p => p.AmOut);
        mapper.AddMapping<EmployeeImportModel>("PM In", p => p.PMIn);
        mapper.AddMapping<EmployeeImportModel>("PM OUT", p => p.PMOut);
        mapper.AddMapping<EmployeeImportModel>("Break Duration", p => p.BreakDuration);
        mapper.AddMapping<EmployeeImportModel>("Max Working Minutes", p => p.MaxWorkingMinutes);
        mapper.AddMapping<EmployeeImportModel>("PaidLunchBreak", p => p.PaidLunchBreak);

        mapper.AddMapping<EmployeeImportModel>("SSS", p => p.SSS);
        mapper.AddMapping<EmployeeImportModel>("PHIC", p => p.PHIC);
        mapper.AddMapping<EmployeeImportModel>("HDMF", p => p.HDMF);
        mapper.AddMapping<EmployeeImportModel>("Daily Rate", p => p.DailyRate);

        mapper.AddMapping<EmployeeImportModel>("Cut-Off 1", p => p.Cutoff1);
        mapper.AddMapping<EmployeeImportModel>("Cut-Off 2", p => p.Cutoff2);
        mapper.AddMapping<EmployeeImportModel>("Cut-Off 3", p => p.Cutoff3);
        mapper.AddMapping<EmployeeImportModel>("Cut-Off 4", p => p.Cutoff4);
        mapper.AddMapping<EmployeeImportModel>("EOM 1", p => p.EOM1);
        mapper.AddMapping<EmployeeImportModel>("EOM 2", p => p.EOM2);
        mapper.AddMapping<EmployeeImportModel>("EOM 3", p => p.EOM3);
        mapper.AddMapping<EmployeeImportModel>("EOM 4", p => p.EOM4);
    }
    private void ValidateImportData(List<EmployeeImportModel> data, List<BasicEmployeeInfo> dbEmployees)
    {
        var errors = new List<string>();
        var internalDuplicates = data
            .Where(x => x.BioId != "" || x.BioId != "0" || x.BioId != null)
            .GroupBy(x => x.BioId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (internalDuplicates.Any() && internalDuplicates[0] != null)
        {
            errors.Add($"The Excel file contains duplicate BioIds: {string.Join(", ", internalDuplicates)}");
        }

        // 1. Safe Dictionary Creation: Handle DB duplicates gracefully by taking the active/latest record
        var dbEmployeeMap = dbEmployees
            .Where(x => x.BioId.HasValue && x.BioId.Value > 0)
            .GroupBy(x => x.BioId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.Id).First()
            );

        // 2. Process Import Data
        foreach (var item in data)
        {
            // Safely parse and ensure it's a valid, non-zero biometric ID
            if (!int.TryParse(item.BioId, out var bioIdNo) || bioIdNo <= 0)
            {
                // Optional: Log an error here if a blank/zero BioId is considered invalid input
                continue;
            }

            // 3. Conflict Validation against existing database records
            if (dbEmployeeMap.TryGetValue(bioIdNo, out var existing))
            {
                if (!NamesMatch(item, existing))
                {

                    errors.Add($"BioId {bioIdNo} is already registered to '{existing.FirstName} {existing.LastName}', " +
                               $"but the import file lists it as '{item.FirstName} {item.LastName}'.");

                }
            }
        }
        if (errors.Any())
        {
            throw new ValidationException($"Import Validation Failed:\n{string.Join("\n", errors)}");
        }
    }

    //private void CutoffValidation( List<CutoffDay> cutoffDays)
    //{
    //    var cutoffDays = model.CutoffDays?.ToList() ?? new List<CutoffDay>();

    //    // GetCurrentCutoff throws the moment payroll runs for this group if it has none —
    //    // every frequency needs at least one, including MONTHLY/DAILY.
    //    if (cutoffDays.Count == 0)
    //    {
    //        return new EvaluationResult("At least one cutoff day is required.");
    //    }

    //    foreach (var cd in cutoffDays)
    //    {
    //        if (!cd.IsEndOfMonth && (cd.Day < 1 || cd.Day > 31))
    //        {
    //            var name = string.IsNullOrWhiteSpace(cd.Label) ? cd.Day.ToString() : cd.Label;
    //            return new EvaluationResult($"Cutoff day '{name}' must be between 1 and 31.");
    //        }
    //    }

    //    // Two rows pointing at the same actual day make CutoffPolicyResolver's
    //    // first/second/last-cutoff detection ambiguous.
    //    if (cutoffDays.Count(cd => cd.IsEndOfMonth) > 1)
    //    {
    //        return new EvaluationResult("Only one cutoff day can be marked as End of Month.");
    //    }
    //    var duplicateDay = cutoffDays
    //        .Where(cd => !cd.IsEndOfMonth)
    //        .GroupBy(cd => cd.Day)
    //        .FirstOrDefault(g => g.Count() > 1);
    //    if (duplicateDay != null)
    //    {
    //        return new EvaluationResult($"Cutoff day {duplicateDay.Key} is configured more than once.");
    //    }

    //    // Semi-Monthly and Weekly rely on a genuine "first" and "second" cutoff
    //    // (CutoffPolicyResolver.GetFirstCutoff/GetSecondCutoff) — a single cutoff throws
    //    // CutoffMismatchException as soon as a Fixed/PerPayroll statutory calculator runs.
    //    if (model.PayrollFrequency is PayrollFrequency.SEMI_MONTHLY or PayrollFrequency.WEEKLY
    //        && cutoffDays.Count < 2)
    //    {
    //        var frequencyName = model.PayrollFrequency == PayrollFrequency.SEMI_MONTHLY ? "Semi-Monthly" : "Weekly";
    //        return new EvaluationResult($"{frequencyName} payroll requires at least two cutoff days.");
    //    }
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
        //var models = _mapper.Map<List<TimeShift>>(shifts.Values.ToList());
        //if (models == null || !models.Any()) return;
        var existing = await _timeShiftService.GetQueryable()
            .Select(x => new { x.ShiftName, x.Id })
            .ToDictionaryAsync(
                x => x.ShiftName.Trim().ToLowerInvariant(),
                x => x.Id,
                token
            );

        var newRecords = new List<TimeShift>();
        // 2. Separate records explicitly to keep EF Core tracking clean
        foreach (var item in shifts.Values.ToList())
        {
            var standardizedName = item.ShiftName?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(standardizedName)) continue;

            if (existing.TryGetValue(standardizedName, out Guid curId))
            {
                item.Id = curId;
            }
            else
            {
                if (!newRecords.Any(x => x.ShiftName.Trim().ToLowerInvariant() == standardizedName))
                {
                    newRecords.Add(item);
                }
            }
        }
        if (newRecords.Any())
        {
            await _timeShiftService.AddRangeAsync(newRecords, token);
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
         .Include(x => x.CutoffDays)
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
            .Select(x => x.BranchCode!)
            .Select(g => g)
            .ToHashSet();

        if (!uniqueIds.Any())
            return new List<(Guid Id, string Code)>();
        return await _branchService.GetByCodes(uniqueIds, token);
    }
    private Dictionary<ShiftKey, TimeShift> ExtractShifts(List<EmployeeImportModel> data)
    {
        return data
            .GroupBy(x => new
            {
                ShiftName = x.ShiftName?.Trim(),
                AMIn = x.AMIn?.Trim(),
                PMOut = x.PMOut?.Trim(),
                AmOut = x.AmOut?.Trim(),
                PMIn = x.PMIn?.Trim()
            })
            .Select(group =>
            {
                var x = group.First();
                var startTime = GetStartTime(x);
                var endTime = GetEndTime(x);

                // These now return TimeSpan? (null if missing/empty)
                var lunchOut = GetLunchOut(x);
                var lunchIn = GetLunchIn(x);

                var shiftType = (x.ShiftType?.Contains("Fix") == true || x.ShiftType?.Contains("Fixed") == true)
                    ? TimeShiftType.FIXED
                    : TimeShiftType.SPLIT;

                // FIXED: Check for null instead of TimeSpan.Zero
                var hasBreak = lunchOut.HasValue && lunchIn.HasValue;

                var maxWorkingMinutes = x.MaxWorkingMinutes;
                var breakDuration = x.BreakDuration > 0
                        ? x.BreakDuration
                        : hasBreak
                            ? Math.Max(0, lunchIn!.Value.Subtract(lunchOut!.Value).TotalMinutes)
                            : 0;
                // Normalize name to guarantee strict matching across the application
                var normalizedShiftName = x.ShiftName?.Trim() ?? string.Empty;

                return new
                {
                    Key = new ShiftKey(normalizedShiftName, startTime, endTime, lunchOut, lunchIn),
                    Value = new TimeShift
                    {
                        Id = Guid.Empty,
                        ShiftType = string.IsNullOrEmpty(x.ShiftType) ? TimeShiftType.FIXED : shiftType,
                        ShiftName = $"{normalizedShiftName}-{x.AMIn?.Trim()}-{x.PMOut?.Trim()}" + (hasBreak ? "-WB" : ""),
                        StartTime = startTime,
                        EndTime = endTime,
                        LunchStartTime = lunchOut,
                        LunchEndTime = lunchIn,
                        BreakDurationMinutes = breakDuration,
                        WithLunchBreak = x.PaidLunchBreak ? BreakMode.PAID_BREAK : BreakMode.UNPAID_BREAK,
                        MaxWorkingMinutes = maxWorkingMinutes,
                        WithAMBreak = BreakMode.NONE,
                        AMStartTime = null,
                        AMEndTime = null,
                        WithPMBreak = BreakMode.NONE,
                        PMStartTime = null,
                        PMEndTime = null,
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
        return TimeSpan.TryParse(x.AMIn?.Trim(), out var amIn) ? amIn : new TimeSpan(8, 0, 0);
    }

    private TimeSpan GetEndTime(EmployeeImportModel x)
    {
        return TimeSpan.TryParse(x.PMOut?.Trim(), out var pmOut) ? pmOut : new TimeSpan(17, 0, 0);
    }

    private TimeSpan? GetLunchOut(EmployeeImportModel x)
    {
        if (string.IsNullOrWhiteSpace(x.AmOut)) return null;
        return TimeSpan.TryParse(x.AmOut.Trim(), out var lunchOut) ? lunchOut : null;
    }

    private TimeSpan? GetLunchIn(EmployeeImportModel x)
    {
        if (string.IsNullOrWhiteSpace(x.PMIn)) return null;
        return TimeSpan.TryParse(x.PMIn.Trim(), out var lunchIn) ? lunchIn : null;
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
            .GroupBy(x => new { x.PayrollGroup, x.Cutoff1, x.Cutoff2, x.Cutoff3, x.Cutoff4 })
            //.GroupBy(x => new { x.PayrollGroup })
            .Select(g => new PayrollGroup
            {
                PayrollFrequency = ResolveFrequency(g.First()),
                CutoffDays = ResolveCutoff(g.First()),
                Name = string.IsNullOrWhiteSpace(g.First().PayrollGroup) ? "--" : g.First().PayrollGroup!
            })
            .ToDictionary(x => x.Name, x => x);
    }

    private PayrollFrequency ResolveFrequency(EmployeeImportModel item)
    {
        if (item.Cutoff1 > 0 && item.Cutoff2 > 0 && item.Cutoff3 > 0 && item.Cutoff4 > 0)
        {
            return EnumParserConfig.SafeParseEnum("WEEKLY", PayrollFrequency.WEEKLY);
        }
        if (item.Cutoff1 > 0 && item.Cutoff2 > 0)
        {
            return EnumParserConfig.SafeParseEnum("SEMI_MONTHLY", PayrollFrequency.SEMI_MONTHLY);
        }
        if (item.Cutoff1 > 0)
        {
            return EnumParserConfig.SafeParseEnum("MONTHLY", PayrollFrequency.MONTHLY);
        }
        return PayrollFrequency.DAILY;
    }

    private List<CutoffDay> ResolveCutoff(EmployeeImportModel item)
    {
        var pf = ResolveFrequency(item);
        switch (pf)
        {
            case PayrollFrequency.DAILY:
                return new List<CutoffDay>()
                    {
                        new CutoffDay(){ Day= item.Cutoff1, IsEndOfMonth= item.EOM1}
                    };
            case PayrollFrequency.WEEKLY:
                return new List<CutoffDay>()
                    {
                        new CutoffDay(){ Day= item.Cutoff1, IsEndOfMonth= item.EOM1},
                        new CutoffDay(){ Day= item.Cutoff2, IsEndOfMonth= item.EOM2},
                        new CutoffDay(){ Day= item.Cutoff3, IsEndOfMonth= item.EOM3},
                        new CutoffDay(){ Day= item.Cutoff4, IsEndOfMonth= item.EOM4},
                    };
            case PayrollFrequency.SEMI_MONTHLY:
                return new List<CutoffDay>()
                    {
                        new CutoffDay(){ Day= item.Cutoff1, IsEndOfMonth= item.EOM1},
                        new CutoffDay(){ Day=  item.Cutoff2, IsEndOfMonth=  item.EOM2},
                    };
            case PayrollFrequency.MONTHLY:
                return new List<CutoffDay>()
                    {
                        new CutoffDay(){ Day= item.Cutoff1, IsEndOfMonth= item.EOM1},
                    };
            default:
                return new List<CutoffDay>()
                    {
                        new CutoffDay(){ Day= 10, IsEndOfMonth= false},
                        new CutoffDay(){ Day=  15, IsEndOfMonth=  false},
                    };
        }
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
            .Select(x => x.RestDay1!)
            .ToList();

        var r2 = data
            .Where(x => !string.IsNullOrWhiteSpace(x.RestDay2))
            .Select(x => x.RestDay2!)
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
    public int? BioId { get; set; }
    public string FirstName { get; set; }
    public string MiddleName { get; set; }
    public string LastName { get; set; }
    public string Suffix { get; set; }
}
public class EmployeeImportModel
{
    // Nullable throughout -- real import rows routinely omit any of these (a blank cell reads
    // back as null from Ganss.Excel), and with Nullable enabled project-wide, a non-nullable
    // `string` here makes ASP.NET Core infer an implicit [Required] on it wherever this type is
    // bound from a request body (e.g. ExportErrorRows), 400-ing on every row with a blank field
    // even though nothing here has ever actually required them -- Upload already null-coalesces
    // every one of these (e.g. `item.SSS ?? ""`).
    public string? BioId { get; set; }
    public string? BranchCode { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Suffix { get; set; }
    public string? Gender { get; set; }
    public string? RestDay1 { get; set; }
    public string? RestDay2 { get; set; }

    public string? DepartmentName { get; set; }
    public string? ClientName { get; set; }
    public string? PayrollGroup { get; set; }
    public string? ShiftName { get; set; }
    public string? ShiftType { get; set; }

    public string? AMIn { get; set; }
    public string? PMOut { get; set; }
    public string? AmOut { get; set; }
    public string? PMIn { get; set; }
    public bool PaidLunchBreak { get; set; }
    public double BreakDuration { get; set; }
    public double MaxWorkingMinutes { get; set; }
    public string? SalaryType { get; set; }

    public string? SSS { get; set; }
    public string? PHIC { get; set; }
    public string? HDMF { get; set; }
    public decimal DailyRate { get; set; }
    public DateOnly? HireDate { get; set; }

    public int Cutoff1 { get; set; }
    public int Cutoff2 { get; set; }
    public int Cutoff3 { get; set; }
    public int Cutoff4 { get; set; }

    public bool EOM1 { get; set; }
    public bool EOM2 { get; set; }
    public bool EOM3 { get; set; }
    public bool EOM4 { get; set; }

}
public class EmployeeImportPreviewRow : EmployeeImportModel
{
    public int RowNumber { get; set; }
    public List<string> Errors { get; set; } = new();
}
public readonly record struct ShiftKey(string ShiftName, TimeSpan start, TimeSpan end, TimeSpan? lunchout, TimeSpan? lunchIn);
public class TemplateDownloaderService
{
    private readonly IWebHostEnvironment _environment;
    private readonly BranchService _branchService;
    private readonly PayrollGroupService _payrollGroupService;

    public TemplateDownloaderService(IWebHostEnvironment environment,
        BranchService branchService,
        PayrollGroupService payrollGroupService
        )
    {
        _environment = environment;
        _branchService = branchService;
        _payrollGroupService = payrollGroupService;
    }

    public async Task<MemoryStream> GetEmployeeTemplate(CancellationToken token)
    {
        var (workbook, _) = await BuildTemplateWorkbookAsync(token);
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0; // Crucial: Reset the stream position to the beginning!
        return stream;
    }

    /// <summary>
    /// Builds only the rows that failed <see cref="EmployeeImportService.PreviewAsync"/>'s
    /// validation, in the exact same column layout/dropdowns as the blank template (reuses
    /// <see cref="BuildTemplateWorkbookAsync"/>), plus a trailing "Import Errors" column
    /// explaining each row's issue -- so after fixing what's flagged, the file re-uploads
    /// through the normal upload-employees endpoint unchanged.
    /// </summary>
    public async Task<MemoryStream> GetEmployeeTemplateWithErrors(List<EmployeeImportPreviewRow> rows, CancellationToken token)
    {
        var (workbook, worksheet) = await BuildTemplateWorkbookAsync(token);

        // The real column headers live on row 2 -- row 1 is a merged "Instructions" banner, and
        // MapFields' header-text mapping is what the upload endpoint actually keys off, not the
        // banner text -- so locating columns has to scan row 2, not row 1.
        const int headerRow = 2;
        const int firstDataRow = 3;

        (string Header, Action<IXLCell, EmployeeImportPreviewRow> Write)[] fieldWriters =
        {
            ("BioId", (c, r) => c.Value = r.BioId),
            ("Branch Code", (c, r) => c.Value = r.BranchCode),
            ("FirstName", (c, r) => c.Value = r.FirstName),
            ("MiddleName", (c, r) => c.Value = r.MiddleName),
            ("LastName", (c, r) => c.Value = r.LastName),
            ("Suffix", (c, r) => c.Value = r.Suffix),
            ("Gender", (c, r) => c.Value = r.Gender),
            ("Email", (c, r) => c.Value = r.Email),
            ("Rest Day 1", (c, r) => c.Value = r.RestDay1),
            ("Rest Day 2", (c, r) => c.Value = r.RestDay2),
            ("Department Name", (c, r) => c.Value = r.DepartmentName),
            ("Client Name", (c, r) => c.Value = r.ClientName),
            ("Payroll Group", (c, r) => c.Value = r.PayrollGroup),
            ("Shift Name", (c, r) => c.Value = r.ShiftName),
            ("Shift Type", (c, r) => c.Value = r.ShiftType),
            ("AMIN", (c, r) => c.Value = r.AMIn),
            ("AM Out", (c, r) => c.Value = r.AmOut),
            ("PM In", (c, r) => c.Value = r.PMIn),
            ("PM OUT", (c, r) => c.Value = r.PMOut),
            ("Break Duration", (c, r) => c.Value = r.BreakDuration),
            ("Max Working Minutes", (c, r) => c.Value = r.MaxWorkingMinutes),
            ("PaidLunchBreak", (c, r) => c.Value = r.PaidLunchBreak),
            ("SSS", (c, r) => c.Value = r.SSS),
            ("PHIC", (c, r) => c.Value = r.PHIC),
            ("HDMF", (c, r) => c.Value = r.HDMF),
            ("Daily Rate", (c, r) => c.Value = r.DailyRate),
            ("Hire Date", (c, r) => { if (r.HireDate.HasValue) c.Value = r.HireDate.Value.ToDateTime(TimeOnly.MinValue); }),
            ("Cut-Off1", (c, r) => c.Value = r.Cutoff1),
            ("Cut-Off2", (c, r) => c.Value = r.Cutoff2),
            ("Cut-Off3", (c, r) => c.Value = r.Cutoff3),
            ("Cut-Off4", (c, r) => c.Value = r.Cutoff4),
            ("EOM1", (c, r) => c.Value = r.EOM1),
            ("EOM2", (c, r) => c.Value = r.EOM2),
            ("EOM3", (c, r) => c.Value = r.EOM3),
            ("EOM4", (c, r) => c.Value = r.EOM4),
        };

        var lastHeaderCol = worksheet.Row(headerRow).LastCellUsed()?.Address.ColumnNumber ?? 1;
        var columnByHeader = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var col = 1; col <= lastHeaderCol; col++)
        {
            var text = worksheet.Cell(headerRow, col).GetString();
            if (!string.IsNullOrWhiteSpace(text) && !columnByHeader.ContainsKey(text))
            {
                columnByHeader[text] = col;
            }
        }

        var errorsColumn = lastHeaderCol + 1;
        worksheet.Cell(headerRow, errorsColumn).Value = "Import Errors";

        // Rows 3+ in the blank template are sample/reference data (a filled-in example row, plus
        // a small lookup table of cutoff-day examples for other payroll frequencies) -- not real
        // rows. Clear them so they don't get re-uploaded as bogus employees alongside the actual
        // corrections; XLClearOptions.Contents leaves the dropdown validations on those cells intact.
        var lastUsedRow = worksheet.LastRowUsed()?.RowNumber() ?? firstDataRow;
        if (lastUsedRow >= firstDataRow)
        {
            worksheet.Range(firstDataRow, 1, lastUsedRow, errorsColumn).Clear(XLClearOptions.Contents);
        }

        var errorRows = rows.Where(r => r.Errors is { Count: > 0 }).ToList();
        for (var i = 0; i < errorRows.Count; i++)
        {
            var row = errorRows[i];
            var excelRow = firstDataRow + i;
            foreach (var (header, write) in fieldWriters)
            {
                if (columnByHeader.TryGetValue(header, out var col))
                {
                    write(worksheet.Cell(excelRow, col), row);
                }
            }
            worksheet.Cell(excelRow, errorsColumn).Value = string.Join("; ", row.Errors);
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private async Task<(XLWorkbook Workbook, IXLWorksheet Worksheet)> BuildTemplateWorkbookAsync(CancellationToken token)
    {
        string templatePath = Path.Combine(_environment.ContentRootPath, "wwwroot", "Templates", "employee_template.xlsx");
        var branchList = await _branchService
            .GetQueryable()
            .Select(x => x.Code)
            .ToListAsync(token);
        ;

        var workbook = new XLWorkbook(templatePath);
        var worksheet = workbook.Worksheet(1);

        //branches
        var helperSheet = workbook.Worksheets.Add("Branches");
        CreateSheet(helperSheet, branchList);
        var range = helperSheet.Range(1, 1, branchList.Count, 1);
        worksheet.Cell("B3").CreateDataValidation().List(range);
        worksheet.Cell("B3").Value = branchList.FirstOrDefault();


        //PayrollGroups
        var payrollgroups = await _payrollGroupService
           .GetQueryable()
           .Select(x => x.Name)
           .ToListAsync(token);

        helperSheet = workbook.Worksheets.Add("payrollgroups");
        CreateSheet(helperSheet, payrollgroups);
        range = helperSheet.Range(1, 1, payrollgroups.Count, 1);
        worksheet.Cell("L3").CreateDataValidation().List(range);
        worksheet.Cell("L3").Value = payrollgroups.FirstOrDefault(x => x.Contains("Semi-Monthly"));


        var shiftTypes = new List<string>() { "Fixed", "Split" };
        helperSheet = workbook.Worksheets.Add("ShiftTypes");
        CreateSheet(helperSheet, shiftTypes);
        range = helperSheet.Range(1, 1, shiftTypes.Count, 1);
        worksheet.Cell("V3").CreateDataValidation().List(range);
        worksheet.Cell("V3").Value = shiftTypes.FirstOrDefault();

        //paid breaks
        var trueFalse = new List<string>() { "False", "True" };
        helperSheet = workbook.Worksheets.Add("trueFalse");
        CreateSheet(helperSheet, trueFalse);
        var truFalserange = helperSheet.Range(1, 1, trueFalse.Count, 1);

        worksheet.Cell("N3").CreateDataValidation().List(truFalserange);
        worksheet.Cell("N3").Value = trueFalse.FirstOrDefault();
        worksheet.Cell("P3").CreateDataValidation().List(truFalserange);
        worksheet.Cell("P3").Value = trueFalse.FirstOrDefault();
        worksheet.Cell("R3").CreateDataValidation().List(truFalserange);
        worksheet.Cell("R3").Value = trueFalse.FirstOrDefault();
        worksheet.Cell("T3").CreateDataValidation().List(truFalserange);
        worksheet.Cell("T3").Value = trueFalse.FirstOrDefault();
        worksheet.Cell("AC3").CreateDataValidation().List(truFalserange);
        worksheet.Cell("AC3").Value = trueFalse.FirstOrDefault();

        //salary Type
        var SalaryTypes = new List<string>() { "Variable", "Fixed" };
        helperSheet = workbook.Worksheets.Add("SalaryType");
        CreateSheet(helperSheet, SalaryTypes);
        range = helperSheet.Range(1, 1, SalaryTypes.Count, 1);
        worksheet.Cell("AD3").CreateDataValidation().List(range);
        worksheet.Cell("AD3").Value = SalaryTypes.LastOrDefault();

        worksheet.Cell("AE3").Value = DateTime.UtcNow.Date;

        return (workbook, worksheet);
    }
    private void CreateSheet<T>(IXLWorksheet helperSheet, List<T> data)
    {
        helperSheet.Cell(1, 1).InsertData(data);
        helperSheet.Hide();
    }
}
