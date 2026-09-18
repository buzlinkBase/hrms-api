using ClosedXML.Excel;
using Ganss.Excel;
using Ganss.Excel.Exceptions;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Mapster;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using NPOI.SS.UserModel;
using System.Globalization;

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

    // ExcelMapper's own MinRowNumber (0-based NPOI row index of the first real data row) --
    // shared with PreviewAsync so a parse error's Line can be matched back to the right data
    // row, since ExcelMapper.MinRowNumber itself is only set locally in ParseAndDefaultAsync.
    private const int ExcelMinDataRowNumber = 2;

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
        var (data, allEmployees, _) = await ParseAndDefaultAsync(fileStream, token);
        ValidateImportData(data, allEmployees);
        await PersistAsync(data, allEmployees, token);
    }

    /// <summary>
    /// Commits a previously-previewed import (<see cref="PreviewAsync"/>), letting the caller
    /// drop rows the user excluded in the preview UI instead of re-uploading the original file.
    /// Rows still carrying validation errors are silently skipped rather than imported --
    /// defense in depth, since the frontend already disables its own Confirm while any row has
    /// errors and the only way one reaches here is a stale/tampered payload.
    /// </summary>
    public async Task CommitPreviewAsync(List<EmployeeImportPreviewRow> rows, CancellationToken token)
    {
        var data = rows
            .Where(r => r.Errors is not { Count: > 0 })
            .Select(r => r.Adapt<EmployeeImportModel>())
            .ToList();
        if (data.Count == 0) return;

        var allEmployees = await GetAllEmployees(token);
        await PersistAsync(data, allEmployees, token);
    }

    private SalaryType ResolveSalaryType(EmployeeImportModel item)
    {
        if (string.Equals(item.SalaryType?.Trim(), nameof(SalaryType.FIXED), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.SalaryType?.Trim(), "Fix", StringComparison.OrdinalIgnoreCase))
        {
            return SalaryType.FIXED;
        }
        return SalaryType.VARIABLE;
    }

    private async Task PersistAsync(List<EmployeeImportModel> data, List<BasicEmployeeInfo> allEmployees, CancellationToken token)
    {
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

        // store employees
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

            clients.TryGetValue(item.ClientName!, out Client? client);

            var pgKey = GetPayrollGroupKey(item);
            pyGroups.TryGetValue(pgKey, out PayrollGroup? pg);

            departments.TryGetValue(item.DepartmentName!, out Department? department);
            var branch = branches.FirstOrDefault(x => x.Code == item.BranchCode);
            Guid? BranchId = !branches.Any() ? null : branch.Equals(default) ? branches.FirstOrDefault().Id : branch.Id;
            if (pg == null) pg = pyGroups.Values.FirstOrDefault();

            int.TryParse(item.BioId, out var bioIdNo);
            int? bioId = bioIdNo == 0 ? null : bioIdNo;

            var employee = new Employee()
            {
                BioId = bioId,
                BranchId = BranchId,
                FirstName = item.FirstName ?? "",
                MiddleName = item.MiddleName ?? "",
                LastName = item.LastName ?? "",
                Suffix = item.Suffix ?? "",
                Gender = item.Gender ?? "Male",
                TimeShiftId = timeShift?.Id,
                ClientId = client?.Id,
                PayrollGroupId = pg?.Id ?? Guid.Empty,
                DepartmentId = department?.Id,
                SSSNo = item.SSS ?? "",
                PHICNo = item.PHIC ?? "",
                HDMFNo = item.HDMF ?? "",
                TIN = item.TIN ?? "",
                Email = item.Email ?? "",
                Contact = item.ContactNo ?? "",
                JobLevel = JobLevelOption.RankandFile,
                CivilStatus = item.CivilStatus ?? "",
                DOB = item.DateOfBirth.HasValue ? item.DateOfBirth.Value : null,
                EmploymentStatus = EmploymentStatus.Regular,
                Address1 = item.Address1 ?? "",
                Address2 = item.Address2 ?? "",
                ModeOfPayment = string.IsNullOrWhiteSpace(item.BankName) && string.IsNullOrWhiteSpace(item.BankNo)
                                ? PaymentMethod.Cash
                                : PaymentMethod.ATM,
                BankName = item.BankName ?? "",
                BankNo = item.BankNo ?? "",
                SalaryType = ResolveSalaryType(item),
                Settings = new EmployeeSetting
                {
                    IsEligibleFor13thMonth = true,
                    IsEligibleForOvertime = true,
                    IsEligibleForLeaveCredits = true,
                    IsEligibleForNightDifferential = true,
                    IsEligibleForRegularHolidayPay = true,
                    IsEligibleForSpecialHolidayPay = true,
                },
                MonthlyRate = item.MonthlyRate,
                DailyRate = item.DailyRate,
                BloodType = item.BloodType ?? "",
                HireDate = item.HireDate == null ? DateOnly.FromDateTime(DateTime.UtcNow) : item.HireDate.Value,
            };

            var existing = allEmployees
                .FirstOrDefault(x => x.FirstName == employee.FirstName &&
                x.MiddleName == employee.MiddleName &&
                x.LastName == employee.LastName &&
                x.Suffix == employee.Suffix);

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

    private async Task<(List<EmployeeImportModel> Data, List<BasicEmployeeInfo> DbEmployees, List<ExcelMapperConvertException> ParseErrors)> ParseAndDefaultAsync(Stream fileStream, CancellationToken token)
    {
        var mapper = new ExcelMapper(fileStream)
        {
            HeaderRowNumber = 1,
            MinRowNumber = ExcelMinDataRowNumber,
        };
        MapFields(mapper);

        // A single malformed cell (e.g. a phone number typed into the Date Birth column) would
        // otherwise throw and abort Fetch for the ENTIRE file, with nothing to show the user --
        // cancel the exception so that cell is left at its property's default instead, and keep
        // the raw error so PreviewAsync can surface it as a normal per-row issue.
        var parseErrors = new List<ExcelMapperConvertException>();
        mapper.ErrorParsingCell += (_, e) =>
        {
            parseErrors.Add(e.Error);
            e.Cancel = true;
        };

        var data = mapper.Fetch<EmployeeImportModel>(0, ConvertCellValue).ToList();
        var allEmployees = await GetAllEmployees(token);
        SetDefaults(data);
        return (data, allEmployees, parseErrors);
    }

    // ValueConverterArgs.ColumnName is the mapped C# PROPERTY name (per AddMapping in MapFields),
    // not the Excel header text -- "PMOut", not "PM OUT".
    private static readonly HashSet<string> TimeColumnHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(EmployeeImportModel.AMIn),
        nameof(EmployeeImportModel.AmOut),
        nameof(EmployeeImportModel.PMIn),
        nameof(EmployeeImportModel.PMOut),
    };

    private static bool IsNumericCell(ICell cell) =>
        cell.CellType == CellType.Numeric
        || (cell.CellType == CellType.Formula && cell.CachedFormulaResultType == CellType.Numeric);

    /// <summary>
    /// Two independent ExcelMapper conversion gaps, both bypassed here by computing the value
    /// straight from the cell's raw numeric (Excel date/time serial) value instead of letting
    /// ExcelMapper/NPOI do it:
    /// - AMIn/AmOut/PMIn/PMOut are read as plain strings, which normally means NPOI renders the
    ///   raw numeric time value through its own number-format lookup -- for a handful of Excel's
    ///   built-in time formats NPOI has never implemented, that lookup fails and produces a
    ///   literal "reserved-0x.." placeholder instead of an actual time, corrupting the value
    ///   regardless of what the cell displays in Excel. Uses TimeSpan's own ToString() (hh:mm:ss,
    ///   or d.hh:mm:ss past 24h -- e.g. an overnight shift's PM OUT) since that's exactly what
    ///   TimeSpan.TryParse (GetStartTime/GetEndTime/GetLunchOut/GetLunchIn below) already expects.
    /// - HireDate is DateOnly (unlike DateOfBirth, which is DateTime) -- ExcelMapper has NO
    ///   built-in conversion to DateOnly at all, from either a numeric Excel date serial or a
    ///   typed date string, so every row with a real date in that column throws
    ///   ExcelMapperConvertException ("... is not a valid DateOnly"), not just the rare
    ///   genuinely-bad cell. For a numeric cell, NPOI's own DateUtil.GetJavaDate already knows how
    ///   to turn an Excel serial into the correct DateTime (including Excel's 1900-leap-year
    ///   quirk) -- this just narrows that down to the date part ExcelMapper couldn't produce
    ///   itself. For a typed string (someone entered "04/16/2025" as text instead of a real Excel
    ///   date), parse it directly; if it doesn't even parse as a date, fall through to
    ///   ExcelMapper's own conversion so that cell still gets the normal ErrorParsingCell/
    ///   per-row-error treatment instead of being silently dropped.
    /// </summary>
    private static object? ConvertCellValue(ValueConverterArgs args)
    {
        if (args.Cell == null) return args.CellValue;

        if (TimeColumnHeaders.Contains(args.ColumnName))
        {
            if (!IsNumericCell(args.Cell)) return args.CellValue;
            var totalSeconds = (long)Math.Round(args.Cell.NumericCellValue * 24 * 60 * 60);
            return TimeSpan.FromSeconds(totalSeconds).ToString();
        }

        if (string.Equals(args.ColumnName, nameof(EmployeeImportModel.HireDate), StringComparison.OrdinalIgnoreCase))
        {
            if (IsNumericCell(args.Cell))
                return DateOnly.FromDateTime(DateUtil.GetJavaDate(args.Cell.NumericCellValue));
            if (args.CellValue is string text
                && DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return parsed;
        }

        return args.CellValue;
    }

    /// <summary>
    /// Turns a cancelled ExcelMapper conversion error into a user-facing message. Doesn't name
    /// the target column (ExcelMapperConvertException doesn't carry the header text, only a raw
    /// column index) -- the Excel column letter plus the offending value is enough to find and
    /// fix the cell.
    /// </summary>
    private static string FormatParseError(ExcelMapperConvertException error)
    {
        var typeName = Nullable.GetUnderlyingType(error.TargetType) ?? error.TargetType;
        var friendly = typeName == typeof(DateTime) ? "date"
            : typeName == typeof(bool) ? "Yes/No value"
            : typeName == typeof(int) || typeName == typeof(double) || typeName == typeof(decimal) ? "number"
            : typeName.Name;
        return $"Column {ExcelMapper.IndexToLetter(error.Column)}: \"{error.CellValue}\" is not a valid {friendly} and was left blank.";
    }

    /// <summary>
    /// Read-only counterpart to <see cref="Upload"/>: parses and defaults the file exactly the
    /// same way, but never writes anything -- no reference-entity creation, no SaveChanges, no
    /// commit. Lets the caller show the user what will be imported (and any per-row conflicts)
    /// before they commit to <see cref="Upload"/>.
    /// </summary>
    public async Task<List<EmployeeImportPreviewRow>> PreviewAsync(Stream fileStream, CancellationToken token)
    {
        var (data, allEmployees, parseErrors) = await ParseAndDefaultAsync(fileStream, token);

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

        // Best-effort match back to a data row: ExcelMapper visits rows in order starting at
        // MinRowNumber (0-based), so a given row's position in `data` is Line - MinRowNumber --
        // exact as long as no fully blank row was skipped in between. Grouped (not a dictionary)
        // since one bad row can have more than one bad cell.
        var parseErrorsByRowIndex = parseErrors
            .ToLookup(e => e.Line - ExcelMinDataRowNumber);

        var rows = new List<EmployeeImportPreviewRow>();
        for (var i = 0; i < data.Count; i++)
        {
            var item = data[i];
            var row = item.Adapt<EmployeeImportPreviewRow>();
            row.RowNumber = i + 1;
            row.Errors = BuildRowErrors(item, dbEmployeeMap, internalDuplicateBioIds);
            row.Errors.AddRange(parseErrorsByRowIndex[i].Select(FormatParseError));
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
        mapper.AddMapping<EmployeeImportModel>("Hire Date", p => p.HireDate);
        mapper.AddMapping<EmployeeImportModel>("Date Birth", p => p.DateOfBirth);
        mapper.AddMapping<EmployeeImportModel>("Contact Number", p => p.ContactNo);
        mapper.AddMapping<EmployeeImportModel>("Bank Name", p => p.BankName);
        mapper.AddMapping<EmployeeImportModel>("Bank No", p => p.BankNo);

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
        mapper.AddMapping<EmployeeImportModel>("TIN", p => p.TIN);

        mapper.AddMapping<EmployeeImportModel>("SalaryType", p => p.SalaryType);
        mapper.AddMapping<EmployeeImportModel>("Monthly Rate", p => p.MonthlyRate);
        mapper.AddMapping<EmployeeImportModel>("Daily Rate", p => p.DailyRate);
        mapper.AddMapping<EmployeeImportModel>("Civil Status", p => p.CivilStatus);

        mapper.AddMapping<EmployeeImportModel>("Cut-Off 1", p => p.Cutoff1);
        mapper.AddMapping<EmployeeImportModel>("Cut-Off 2", p => p.Cutoff2);
        mapper.AddMapping<EmployeeImportModel>("Cut-Off 3", p => p.Cutoff3);
        mapper.AddMapping<EmployeeImportModel>("Cut-Off 4", p => p.Cutoff4);
        mapper.AddMapping<EmployeeImportModel>("EOM 1", p => p.EOM1);
        mapper.AddMapping<EmployeeImportModel>("EOM 2", p => p.EOM2);
        mapper.AddMapping<EmployeeImportModel>("EOM 3", p => p.EOM3);
        mapper.AddMapping<EmployeeImportModel>("EOM 4", p => p.EOM4);
        mapper.AddMapping<EmployeeImportModel>("Address 1", p => p.Address1);
        mapper.AddMapping<EmployeeImportModel>("Address 2", p => p.Address2);
        mapper.AddMapping<EmployeeImportModel>("Blood Type", p => p.BloodType);

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

        var dbEmployeeMap = dbEmployees
            .Where(x => x.BioId.HasValue && x.BioId.Value > 0)
            .GroupBy(x => x.BioId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.Id).First()
            );

        foreach (var item in data)
        {
            if (!int.TryParse(item.BioId, out var bioIdNo) || bioIdNo <= 0)
            {
                continue;
            }

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

        var existing = await _timeShiftService.GetQueryable()
            .Select(x => new { x.ShiftName, x.Id })
            .ToDictionaryAsync(
                x => x.ShiftName.Trim().ToLowerInvariant(),
                x => x.Id,
                token
            );

        var newRecords = new List<TimeShift>();
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

        var existingGroups = await _payrollGroupService.GetQueryable()
            .Include(x => x.CutoffDays)
            .ToListAsync(token);

        var newRecords = new List<PayrollGroup>();

        foreach (var kvp in pr)
        {
            var item = kvp.Value;

            var match = existingGroups.FirstOrDefault(x =>
                string.Equals(x.Name.Trim(), item.Name.Trim(), StringComparison.OrdinalIgnoreCase) &&
                x.PayrollFrequency == item.PayrollFrequency &&
                x.CutoffDays.Count == item.CutoffDays.Count &&
                !x.CutoffDays.ExceptBy(item.CutoffDays.Select(c => (c.Day, c.IsEndOfMonth)), c => (c.Day, c.IsEndOfMonth)).Any());

            if (match != null)
            {
                item.Id = match.Id;
            }
            else
            {
                newRecords.Add(item);
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

                var lunchOut = GetLunchOut(x);
                var lunchIn = GetLunchIn(x);

                var shiftType = (x.ShiftType?.Contains("Fix") == true || x.ShiftType?.Contains("Fixed") == true)
                    ? TimeShiftType.FIXED
                    : TimeShiftType.SPLIT;

                var hasBreak = lunchOut.HasValue && lunchIn.HasValue;

                var maxWorkingMinutes = x.MaxWorkingMinutes;
                var breakDuration = x.BreakDuration > 0
                        ? x.BreakDuration
                        : hasBreak
                            ? Math.Max(0, lunchIn!.Value.Subtract(lunchOut!.Value).TotalMinutes)
                            : 0;
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

    // UTILITY
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
    }

    private Dictionary<string, PayrollGroup> ExtractPayrollGroups(List<EmployeeImportModel> data)
    {
        return data
            .GroupBy(x => new
            {
                BaseName = string.IsNullOrWhiteSpace(x.PayrollGroup) ? "--" : x.PayrollGroup.Trim(),
                x.Cutoff1,
                x.Cutoff2,
                x.Cutoff3,
                x.Cutoff4,
                x.EOM1,
                x.EOM2,
                x.EOM3,
                x.EOM4
            })
            .Select(g =>
            {
                var sample = g.First();
                var formattedName = BuildPayrollGroupName(
                    sample.PayrollGroup,
                    (sample.Cutoff1, sample.EOM1),
                    (sample.Cutoff2, sample.EOM2),
                    (sample.Cutoff3, sample.EOM3),
                    (sample.Cutoff4, sample.EOM4));
                var cutoffDays = ResolveCutoff(sample);
                var frequency = ResolveFrequency(sample);

                var key = formattedName.ToLowerInvariant();

                return new
                {
                    Key = key,
                    PayrollGroup = new PayrollGroup
                    {
                        Name = formattedName,
                        PayrollFrequency = frequency,
                        CutoffDays = cutoffDays
                    }
                };
            })
            // GroupBy+ToDictionary (first-wins) rather than a plain ToDictionary -- cheap
            // insurance against ToDictionary's duplicate-key crash on any unforeseen name
            // collision, now that day + EOM are both folded into the name itself.
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.First().PayrollGroup);
    }

    private string GetPayrollGroupKey(EmployeeImportModel item)
    {
        return BuildPayrollGroupName(
            item.PayrollGroup,
            (item.Cutoff1, item.EOM1),
            (item.Cutoff2, item.EOM2),
            (item.Cutoff3, item.EOM3),
            (item.Cutoff4, item.EOM4)).ToLowerInvariant();
    }

    /// <summary>
    /// Builds the unique payroll-group name (and, lowercased, the lookup key into
    /// ExtractPayrollGroups' dictionary -- see GetPayrollGroupKey) by joining the entered base
    /// name with each active cutoff day, hyphen-separated. Cutoffs marked End-of-Month get an
    /// "EOM" suffix on that cutoff's number, so two rows sharing the same base name and cutoff
    /// day but differing only in End-of-Month don't collide into the same group/name.
    /// </summary>
    private static string BuildPayrollGroupName(string? baseName, params (int Day, bool IsEndOfMonth)[] cutoffs)
    {
        var name = string.IsNullOrWhiteSpace(baseName) || baseName.Trim() == "--" ? "-" : baseName.Trim();
        var parts = new List<string> { name };
        parts.AddRange(cutoffs
            .Where(c => c.Day > 0)
            .Select(c => c.IsEndOfMonth ? $"{c.Day}EOM" : $"{c.Day}"));
        return string.Join("-", parts);
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
                    new CutoffDay(){ Day = item.Cutoff1, IsEndOfMonth = item.EOM1 }
                };
            case PayrollFrequency.WEEKLY:
                return new List<CutoffDay>()
                {
                    new CutoffDay(){ Day = item.Cutoff1, IsEndOfMonth = item.EOM1 },
                    new CutoffDay(){ Day = item.Cutoff2, IsEndOfMonth = item.EOM2 },
                    new CutoffDay(){ Day = item.Cutoff3, IsEndOfMonth = item.EOM3 },
                    new CutoffDay(){ Day = item.Cutoff4, IsEndOfMonth = item.EOM4 },
                };
            case PayrollFrequency.SEMI_MONTHLY:
                return new List<CutoffDay>()
                {
                    new CutoffDay(){ Day = item.Cutoff1, IsEndOfMonth = item.EOM1 },
                    new CutoffDay(){ Day = item.Cutoff2, IsEndOfMonth = item.EOM2 },
                };
            case PayrollFrequency.MONTHLY:
                return new List<CutoffDay>()
                {
                    new CutoffDay(){ Day = item.Cutoff1, IsEndOfMonth = item.EOM1 },
                };
            default:
                return new List<CutoffDay>()
                {
                    new CutoffDay(){ Day = 10, IsEndOfMonth = false },
                    new CutoffDay(){ Day = 15, IsEndOfMonth = false },
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
    public string? ContactNo { get; set; }
    public string? CivilStatus { get; set; }
    public string? BloodType { get; set; }

    public string? SSS { get; set; }
    public string? PHIC { get; set; }
    public string? HDMF { get; set; }
    public string? TIN { get; set; }
    public decimal DailyRate { get; set; }
    public decimal MonthlyRate { get; set; }
    public DateOnly? HireDate { get; set; }
    public DateTime? DateOfBirth { get; set; }

    public int Cutoff1 { get; set; }
    public int Cutoff2 { get; set; }
    public int Cutoff3 { get; set; }
    public int Cutoff4 { get; set; }

    public bool EOM1 { get; set; }
    public bool EOM2 { get; set; }
    public bool EOM3 { get; set; }
    public bool EOM4 { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? BankNo { get; set; }
    public string? BankName { get; set; }
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
    private readonly DepartmentService _departmentService;
    private readonly PayrollGroupService _payrollGroupService;

    public TemplateDownloaderService(IWebHostEnvironment environment,
        BranchService branchService,
        DepartmentService departmentService,
        PayrollGroupService payrollGroupService
        )
    {
        _environment = environment;
        _branchService = branchService;
        _departmentService = departmentService;
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

        // Header text here must match the real template's row-2 headers exactly (whitespace
        // included -- the lookup below is case-insensitive but not space-insensitive) and the
        // same header text AddMapping (above, in EmployeeImportService's own mapper setup) binds
        // on upload, so a fixed-up file re-uploads through the normal endpoint unchanged. Kept in
        // the same left-to-right order as the template's actual columns for easier upkeep.
        (string Header, Action<IXLCell, EmployeeImportPreviewRow> Write)[] fieldWriters =
        {
            ("BioId", (c, r) => c.Value = r.BioId),
            ("Branch Code", (c, r) => c.Value = r.BranchCode),
            ("FirstName", (c, r) => c.Value = r.FirstName),
            ("MiddleName", (c, r) => c.Value = r.MiddleName),
            ("LastName", (c, r) => c.Value = r.LastName),
            ("Suffix", (c, r) => c.Value = r.Suffix),
            ("Gender", (c, r) => c.Value = r.Gender),
            ("Rest Day 1", (c, r) => c.Value = r.RestDay1),
            ("Rest Day 2", (c, r) => c.Value = r.RestDay2),
            ("Department Name", (c, r) => c.Value = r.DepartmentName),
            ("Client Name", (c, r) => c.Value = r.ClientName),
            ("Payroll Group", (c, r) => c.Value = r.PayrollGroup),
            ("Cut-Off 1", (c, r) => c.Value = r.Cutoff1),
            ("EOM 1", (c, r) => c.Value = r.EOM1),
            ("Cut-Off 2", (c, r) => c.Value = r.Cutoff2),
            ("EOM 2", (c, r) => c.Value = r.EOM2),
            ("Cut-Off 3", (c, r) => c.Value = r.Cutoff3),
            ("EOM 3", (c, r) => c.Value = r.EOM3),
            ("Cut-Off 4", (c, r) => c.Value = r.Cutoff4),
            ("EOM 4", (c, r) => c.Value = r.EOM4),
            ("Shift Name", (c, r) => c.Value = r.ShiftName),
            ("Shift Type", (c, r) => c.Value = r.ShiftType),
            ("AMIN", (c, r) => c.Value = r.AMIn),
            ("AM Out", (c, r) => c.Value = r.AmOut),
            ("PM In", (c, r) => c.Value = r.PMIn),
            ("PM OUT", (c, r) => c.Value = r.PMOut),
            ("Break Duration", (c, r) => c.Value = r.BreakDuration),
            ("Max Working Minutes", (c, r) => c.Value = r.MaxWorkingMinutes),
            ("PaidLunchBreak", (c, r) => c.Value = r.PaidLunchBreak),
            ("SalaryType", (c, r) => c.Value = r.SalaryType),
            ("Monthly Rate", (c, r) => c.Value = r.MonthlyRate),
            ("Daily Rate", (c, r) => c.Value = r.DailyRate),
            ("Hire Date", (c, r) => { if (r.HireDate.HasValue) c.Value = r.HireDate.Value.ToDateTime(TimeOnly.MinValue); }),
            ("SSS", (c, r) => c.Value = r.SSS),
            ("PHIC", (c, r) => c.Value = r.PHIC),
            ("HDMF", (c, r) => c.Value = r.HDMF),
            ("TIN", (c, r) => c.Value = r.TIN),
            ("Email", (c, r) => c.Value = r.Email),
            ("Date Birth", (c, r) => { if (r.DateOfBirth.HasValue) c.Value = r.DateOfBirth.Value; }),
            ("Contact Number", (c, r) => c.Value = r.ContactNo),
            ("Civil Status", (c, r) => c.Value = r.CivilStatus),
            ("Blood Type", (c, r) => c.Value = r.BloodType),
            ("Address 1", (c, r) => c.Value = r.Address1),
            ("Address 2", (c, r) => c.Value = r.Address2),
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

        var departments = await _departmentService
            .GetQueryable()
            .Select(x => x.Name)
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


        // Rest Days -- day-name dropdown plus a blank entry, so employees without a configured
        // rest day can leave Rest Day 1 / Rest Day 2 empty instead of being forced to pick a
        // day (a blank cell is already treated as "no rest day", not an error, by
        // ExtractRestDay/UploadAsync above).
        var dayNames = new List<string> { "" };
        dayNames.AddRange(Enum.GetNames(typeof(DayName)));
        helperSheet = workbook.Worksheets.Add("RestDays");
        CreateSheet(helperSheet, dayNames);
        range = helperSheet.Range(1, 1, dayNames.Count, 1);
        worksheet.Cell("H3").CreateDataValidation().List(range);
        worksheet.Cell("H3").Value = "Saturday";
        worksheet.Cell("I3").CreateDataValidation().List(range);
        worksheet.Cell("I3").Value = "Sunday";

        //departments
        helperSheet = workbook.Worksheets.Add("Departments");
        CreateSheet(helperSheet, departments);
        range = helperSheet.Range(1, 1, departments.Count, 1);
        worksheet.Cell("J3").CreateDataValidation().List(range);
        worksheet.Cell("J3").Value = departments.FirstOrDefault();


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