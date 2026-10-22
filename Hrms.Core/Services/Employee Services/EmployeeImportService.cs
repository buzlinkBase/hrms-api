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
using System.Reflection;

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
    private readonly CostCenterService _costCenterService;

    public event Action<string> OnMessage;

    // 0-based NPOI row index of the header row in the official template (row 1 is a merged
    // "Instructions" banner, headers on row 2). Only the fallback -- FindHeaderRowIndex detects
    // the real header row, since hand-made files routinely drop the banner and put headers on
    // row 1, which used to make the first EMPLOYEE row get read as the header row (so no
    // FirstName/LastName/... header was ever found and every mapped field imported blank).
    private const int TemplateHeaderRowIndex = 1;
    private const int HeaderScanRowLimit = 10;

    public EmployeeImportService(
        IUnitOfWorkService uow,
        EmployeeService employeeService,
        TimeShiftService timeShiftService,
        DepartmentService departmentService,
        PayrollGroupService payrollGroupService,
        ClientService clientService,
        BranchService branchService,
        CostCenterService costCenterService)
    {
        _uow = uow;
        _employeeService = employeeService;
        _timeShiftService = timeShiftService;
        _departmentService = departmentService;
        _payrollGroupService = payrollGroupService;
        _clientService = clientService;
        _branchService = branchService;
        _costCenterService = costCenterService;
    }

    public async Task Upload(Stream fileStream, CancellationToken token)
    {
        var (data, allEmployees, _, _) = await ParseAndDefaultAsync(fileStream, token);
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
        // Checked here too, not trusted to the client's Errors list: nothing is imported if any
        // row is missing a name. Numbered by the preview's own row numbers.
        var missingNames = rows
            .SelectMany(r => MissingNameErrors(r).Select(e => $"Row {r.RowNumber}: {e}"))
            .ToList();
        if (missingNames.Count > 0)
        {
            throw new ValidationException($"Import Validation Failed:\n{string.Join("\n", missingNames)}");
        }

        var data = rows
            .Where(r => r.Errors is not { Count: > 0 })
            .Select(r => r.Adapt<EmployeeImportModel>())
            .ToList();
        if (data.Count == 0) return;
        TrimStrings(data);

        var allEmployees = await GetAllEmployees(token);
        // Defense in depth, like the error-row skip above: never touch a record the caller can't see.
        data = data.Where(item => FindRestrictedMatch(item, allEmployees) == null).ToList();
        if (data.Count == 0) return;
        await PersistAsync(data, allEmployees, token);
    }

    /// <summary>
    /// Shown instead of the usual conflict details, so the caller learns nothing about the
    /// confidential employee a row collides with.
    /// </summary>
    public const string RestrictedMatchError =
        "This row matches an employee record you don't have access to, so it can't be imported. " +
        "Ask someone with access to confidential records to import it.";

    // A row that would update (same name) or clash with (same Bio ID) a confidential employee the
    // caller can't see. Without this check the row would silently create a duplicate.
    private BasicEmployeeInfo? FindRestrictedMatch(EmployeeImportModel item, List<BasicEmployeeInfo> dbEmployees)
    {
        int.TryParse(item.BioId, out var bioIdNo);
        return dbEmployees.FirstOrDefault(x => x.IsRestricted &&
            (NamesMatch(item, x) || (bioIdNo > 0 && x.BioId == bioIdNo)));
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

    /// <summary>
    /// A NEW employee whose row leaves both rest day columns blank gets the company's default rest
    /// day(s) (Company Info > Regional Defaults). Rows that name a rest day, and employees that
    /// already exist (a re-import keeps replacing theirs with what the file says), are untouched.
    /// </summary>
    private async Task ApplyCompanyDefaultRestDaysAsync(List<EmployeeImportModel> data, List<BasicEmployeeInfo> allEmployees, CancellationToken token)
    {
        var configured = await _employeeService.Context.Companies
            .Select(c => c.WorkWeekRestDays)
            .FirstOrDefaultAsync(token);
        var days = CompanyRestDays.Parse(configured);
        if (days.Count == 0) return;

        foreach (var item in data)
        {
            if (item == null) continue;
            if (!string.IsNullOrWhiteSpace(item.RestDay1) || !string.IsNullOrWhiteSpace(item.RestDay2)) continue;
            if (allEmployees.Any(x => NamesMatch(item, x))) continue;
            item.RestDay1 = days[0].ToString();
            if (days.Count > 1) item.RestDay2 = days[1].ToString();
        }
    }

    private async Task PersistAsync(List<EmployeeImportModel> data, List<BasicEmployeeInfo> allEmployees, CancellationToken token)
    {
        await ApplyCompanyDefaultRestDaysAsync(data, allEmployees, token);
        var shifts = ExtractShifts(data);
        var branches = await ExtractBranchesAsync(data, token);
        var clients = ExtractClients(data);
        var pyGroups = ExtractPayrollGroups(data);
        var departments = ExtractDepartments(data);
        var projectSites = ExtractProjectSites(data, branches);
        var restDays = ExtractRestDay(data);

        await StoreShiftAsync(shifts, token);
        await StoreClientsAsync(clients, token);
        await StorePayrollGroupsAsync(pyGroups, token);
        await StoreDepartmentsAsync(departments, token);
        await StoreProjectSitesAsync(projectSites, token);
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
            // TimeShiftId is nullable: a row with no (or an unparseable) AMIN / PM OUT gets no
            // shift at all rather than a made-up 8:00-17:00 one.
            TimeShift? timeShift = null;
            if (TryGetShiftTimes(item, out var startTime, out var endTime))
            {
                var sanitizedShiftName = item.ShiftName?.Trim() ?? string.Empty;
                var shiftKey = new ShiftKey(sanitizedShiftName, startTime, endTime, GetLunchOut(item), GetLunchIn(item));
                shifts.TryGetValue(shiftKey, out timeShift);
            }

            Client? client = null;
            if (!string.IsNullOrWhiteSpace(item.ClientName))
            {
                clients.TryGetValue(item.ClientName.Trim(), out client);
            }

            var pgKey = GetPayrollGroupKey(item);
            pyGroups.TryGetValue(pgKey, out PayrollGroup? pg);

            departments.TryGetValue(item.DepartmentName!, out Department? department);
            // Unlike DepartmentName, ProjectSiteName (like ClientName) has no SetDefaults fallback --
            // it legitimately stays null for any row that leaves the (optional) column blank,
            // and Dictionary.TryGetValue throws ArgumentNullException on a null key, so this
            // must be guarded rather than looked up unconditionally like the others above.
            CostCenters? area = null;
            if (!string.IsNullOrWhiteSpace(item.ProjectSiteName))
            {
                projectSites.TryGetValue(item.ProjectSiteName.Trim(), out area);
            }
            var BranchId = ResolveBranchId(item, branches);
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
                AreaId = area?.Id,
                SSSNo = item.SSS ?? "",
                PHICNo = item.PHIC ?? "",
                HDMFNo = item.HDMF ?? "",
                TIN = item.TIN ?? "",
                Email = item.Email ?? "",
                Contact = item.ContactNo ?? "",
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
                CashBond = item.CashBond,
                BloodType = item.BloodType ?? "",
                HireDate = item.HireDate == null ? DateOnly.FromDateTime(DateTime.UtcNow) : item.HireDate.Value,
            };

            // Same trimmed, case-insensitive comparison as NamesMatch -- the file is trimmed now,
            // but employees saved by earlier imports can still carry stray spaces ("Alba "), and
            // an exact match would duplicate them instead of updating them.
            var existing = allEmployees.FirstOrDefault(x => NamesMatch(item, x));

            if (existing != null)
            {
                employee.Id = existing.Id;
                // Import never sets the confidential tag -- a re-import keeps it as stored.
                employee.IsConfidential = existing.IsConfidential;
                // The file has no employee number column, so keep the one already on the record.
                employee.EmployeeNo = existing.EmployeeNo;
                // Re-key onto the row that already exists (one-to-one, unique-indexed on
                // EmployeeId) -- otherwise EF's UpdateRange graph-walk sees this fresh
                // `new EmployeeSetting` with a default Id, treats it as a NEW entity to insert,
                // and collides with the EmployeeSetting row this employee already has.
                if (employee.Settings != null)
                {
                    employee.Settings.Id = existing.SettingsId ?? Guid.Empty;
                }
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

    private async Task<(List<EmployeeImportModel> Data, List<BasicEmployeeInfo> DbEmployees, List<ExcelMapperConvertException> ParseErrors, int FirstDataRowIndex)> ParseAndDefaultAsync(Stream fileStream, CancellationToken token)
    {
        var workbook = WorkbookFactory.Create(fileStream);
        var headerRowIndex = FindHeaderRowIndex(workbook.GetSheetAt(0));
        var firstDataRowIndex = headerRowIndex + 1;
        var mapper = new ExcelMapper(workbook)
        {
            HeaderRowNumber = headerRowIndex,
            MinRowNumber = firstDataRowIndex,
            // Own factory per import: ExcelMapper's default is one process-wide static cache, and
            // MapFields' AddMapping calls mutate it -- two imports at once raced on it ("An item
            // with the same key has already been added").
            TypeMapperFactory = new TypeMapperFactory(),
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
        TrimStrings(data);
        SetDefaults(data);
        return (data, allEmployees, parseErrors, firstDataRowIndex);
    }

    /// <summary>
    /// 0-based index of the first row (within the first few) that carries the FirstName or
    /// LastName header, so a file with headers on row 1 (no "Instructions" banner) imports the
    /// same as the official template. Falls back to the template's row when neither is found.
    /// </summary>
    private static int FindHeaderRowIndex(ISheet sheet)
    {
        var lastRowToScan = Math.Min(sheet.LastRowNum, HeaderScanRowLimit - 1);
        for (var r = sheet.FirstRowNum; r <= lastRowToScan; r++)
        {
            var row = sheet.GetRow(r);
            if (row == null) continue;
            foreach (var cell in row.Cells)
            {
                if (cell.CellType != CellType.String) continue;
                var text = cell.StringCellValue.Trim();
                if (string.Equals(text, EmployeeTemplateHeaders.FirstName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, EmployeeTemplateHeaders.LastName, StringComparison.OrdinalIgnoreCase))
                {
                    return r;
                }
            }
        }
        return TemplateHeaderRowIndex;
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
    ///   TimeSpan.TryParse (ParseTime below) already expects.
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
        var (data, allEmployees, parseErrors, firstDataRowIndex) = await ParseAndDefaultAsync(fileStream, token);

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
            .ToLookup(e => e.Line - firstDataRowIndex);

        var rows = new List<EmployeeImportPreviewRow>();
        for (var i = 0; i < data.Count; i++)
        {
            var item = data[i];
            var row = item.Adapt<EmployeeImportPreviewRow>();
            row.RowNumber = i + 1;
            row.Errors = BuildRowErrors(item, dbEmployeeMap, internalDuplicateBioIds, allEmployees);
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
    private List<string> BuildRowErrors(EmployeeImportModel item, Dictionary<int, BasicEmployeeInfo> dbEmployeeMap, HashSet<string?> internalDuplicateBioIds, List<BasicEmployeeInfo> dbEmployees)
    {
        var errors = MissingNameErrors(item);
        if (errors.Count > 0) return errors;
        if (FindRestrictedMatch(item, dbEmployees) != null)
        {
            errors.Add(RestrictedMatchError);
            return errors;
        }
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

    // Every employee, confidential ones included, so a row is never matched against a partial
    // list (which would duplicate a hidden employee). Hidden ones are marked IsRestricted.
    private async Task<List<BasicEmployeeInfo>> GetAllEmployees(CancellationToken token)
    {
        var employees = await _employeeService.EmployeesForIntegrityChecks()
           .Select(x => new BasicEmployeeInfo
           {
               Id = x.Id,
               BioId = x.BioId,
               FirstName = x.FirstName,
               MiddleName = x.MiddleName,
               LastName = x.LastName,
               Suffix = x.Suffix,
               SettingsId = x.Settings != null ? x.Settings.Id : (Guid?)null,
               EmployeeNo = x.EmployeeNo,
               IsConfidential = x.IsConfidential,
           }).ToListAsync(token);
        foreach (var employee in employees)
        {
            employee.IsRestricted = _employeeService.IsHiddenFromEmployeeScreens(employee.Id);
        }
        return employees;
    }

    private static readonly PropertyInfo[] ImportStringProperties = typeof(EmployeeImportModel)
        .GetProperties()
        .Where(p => p.PropertyType == typeof(string) && p.CanRead && p.CanWrite)
        .ToArray();

    // Every text cell is trimmed at both ends before anything is matched or saved -- stray
    // spaces (" Royal Gem", "Alba ") otherwise end up in names and break lookups. A cell that
    // is only whitespace becomes null, i.e. blank.
    private static void TrimStrings(IEnumerable<EmployeeImportModel> data)
    {
        foreach (var item in data)
        {
            foreach (var property in ImportStringProperties)
            {
                if (property.GetValue(item) is string value)
                {
                    var trimmed = value.Trim();
                    property.SetValue(item, trimmed.Length == 0 ? null : trimmed);
                }
            }
        }
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
            if (string.IsNullOrWhiteSpace(item.DepartmentName))
            {
                item.DepartmentName = "--";
            }
            if (string.IsNullOrWhiteSpace(item.PayrollGroup))
            {
                item.PayrollGroup = "--";
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
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.BioId, p => p.BioId);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.BranchCode, p => p.BranchCode);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.FirstName, p => p.FirstName);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.MiddleName, p => p.MiddleName);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.LastName, p => p.LastName);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.Suffix, p => p.Suffix);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.Gender, p => p.Gender);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.Email, p => p.Email);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.HireDate, p => p.HireDate);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.DateBirth, p => p.DateOfBirth);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.ContactNumber, p => p.ContactNo);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.BankName, p => p.BankName);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.BankNo, p => p.BankNo);

        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.RestDay1, p => p.RestDay1);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.RestDay2, p => p.RestDay2);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.DepartmentName, p => p.DepartmentName);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.ProjectSite, p => p.ProjectSiteName);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.ClientName, p => p.ClientName);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.PayrollGroup, p => p.PayrollGroup);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.ShiftName, p => p.ShiftName);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.ShiftType, p => p.ShiftType);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.AmIn, p => p.AMIn);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.AmOut, p => p.AmOut);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.PmIn, p => p.PMIn);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.PmOut, p => p.PMOut);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.BreakDuration, p => p.BreakDuration);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.MaxWorkingMinutes, p => p.MaxWorkingMinutes);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.PaidLunchBreak, p => p.PaidLunchBreak);

        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.Sss, p => p.SSS);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.Phic, p => p.PHIC);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.Hdmf, p => p.HDMF);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.Tin, p => p.TIN);

        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.SalaryType, p => p.SalaryType);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.MonthlyRate, p => p.MonthlyRate);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.DailyRate, p => p.DailyRate);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.CashBond, p => p.CashBond);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.CivilStatus, p => p.CivilStatus);

        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.Cutoff1, p => p.Cutoff1);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.Cutoff2, p => p.Cutoff2);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.Cutoff3, p => p.Cutoff3);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.Cutoff4, p => p.Cutoff4);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.Eom1, p => p.Eom1);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.Eom2, p => p.Eom2);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.Eom3, p => p.Eom3);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.Eom4, p => p.Eom4);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.Address1, p => p.Address1);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.Address2, p => p.Address2);
        mapper.AddMapping<EmployeeImportModel>(EmployeeTemplateHeaders.BloodType, p => p.BloodType);

    }

    private void ValidateImportData(List<EmployeeImportModel> data, List<BasicEmployeeInfo> dbEmployees)
    {
        var errors = new List<string>();
        var internalDuplicates = data
            .Where(x => !string.IsNullOrWhiteSpace(x.BioId) && x.BioId != "0")
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

        errors.AddRange(MissingNameErrorsByRow(data));

        foreach (var item in data)
        {
            if (MissingNameErrors(item).Count > 0) continue; // already reported above
            if (FindRestrictedMatch(item, dbEmployees) != null)
            {
                errors.Add($"'{item.FirstName} {item.LastName}': {RestrictedMatchError}");
                continue;
            }

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

    // Every imported employee needs a first and a last name -- a row missing either is an error on
    // every import path (preview, upload, commit), never saved as a nameless employee.
    private static List<string> MissingNameErrors(EmployeeImportModel item)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(item.FirstName)) errors.Add("First name is required.");
        if (string.IsNullOrWhiteSpace(item.LastName)) errors.Add("Last name is required.");
        return errors;
    }

    // "Row 3: Last name is required." -- numbered like the preview (1 = first data row).
    private static IEnumerable<string> MissingNameErrorsByRow(List<EmployeeImportModel> data) =>
        data.SelectMany((item, i) => MissingNameErrors(item).Select(e => $"Row {i + 1}: {e}"));

    // Normalizes null to "" before comparing -- a DB employee's MiddleName/Suffix default to ""
    // (see Employee.cs), but a blank Excel cell parses to null, so a plain string.Equals(null,
    // "") would otherwise false-flag re-importing the SAME employee (with, say, a blank Suffix
    // cell) as "BioId already registered to a different name" and abort the whole import.
    private static string Norm(string? s) => (s ?? "").Trim();

    private bool NamesMatch(EmployeeImportModel import, BasicEmployeeInfo db)
    {
        return string.Equals(Norm(import.FirstName), Norm(db.FirstName), StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Norm(import.LastName), Norm(db.LastName), StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Norm(import.MiddleName), Norm(db.MiddleName), StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Norm(import.Suffix), Norm(db.Suffix), StringComparison.OrdinalIgnoreCase);
    }

    private async Task StoreShiftAsync(Dictionary<ShiftKey, TimeShift> shifts, CancellationToken token)
    {
        if (shifts == null || !shifts.Any()) return;

        var existing = ByName(await _timeShiftService.GetQueryable()
            .Select(x => new { x.Id, x.ShiftName })
            .ToListAsync(token), x => x.ShiftName, x => x.Id);

        // The shift name is its identity: every row naming the same shift -- even with other
        // times -- gets the existing shift, or the one new shift created for that name (other
        // rows used to keep ids of shifts that were never saved).
        var newRecords = new Dictionary<string, TimeShift>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in shifts.Values.ToList())
        {
            var name = item.ShiftName?.Trim();
            if (string.IsNullOrEmpty(name)) continue;

            if (existing.TryGetValue(name, out Guid curId))
            {
                item.Id = curId;
            }
            else if (newRecords.TryGetValue(name, out var created))
            {
                item.Id = created.Id;
            }
            else
            {
                if (item.Id == Guid.Empty) item.Id = Guid.CreateVersion7();
                newRecords[name] = item;
            }
        }
        if (newRecords.Count > 0)
        {
            await _timeShiftService.AddRangeAsync(newRecords.Values.ToList(), token);
        }
    }

    // Existing master records keyed by name -- their identity -- trimmed and case-insensitive. If
    // the company already holds records whose names differ only by case or spacing, the first
    // one is used instead of failing on a duplicate key.
    private static Dictionary<string, TValue> ByName<TRow, TValue>(
        IEnumerable<TRow> rows, Func<TRow, string> name, Func<TRow, TValue> value) =>
        rows.Where(x => !string.IsNullOrWhiteSpace(name(x)))
            .GroupBy(x => name(x).Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => value(g.First()), StringComparer.OrdinalIgnoreCase);

    // Codes for records this import creates continue after the company's highest numeric code,
    // instead of restarting at 000001 and repeating codes that already exist.
    private static Func<string> CodeSequence(IEnumerable<string?> existingCodes)
    {
        var next = existingCodes
            .Select(c => int.TryParse(c, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        return () => (next++).FormatCode();
    }

    private async Task StoreClientsAsync(Dictionary<string, Client> clients, CancellationToken token)
    {
        var models = clients.Values.ToList();
        if (models.Count == 0) return;

        var stored = await _clientService.GetQueryable()
            .Select(x => new { x.Id, x.Name, x.Code })
            .ToListAsync(token);
        var existing = ByName(stored, x => x.Name, x => x.Id);
        var nextCode = CodeSequence(stored.Select(x => x.Code));

        var newRecords = new List<Client>();
        foreach (var item in models)
        {
            if (existing.TryGetValue(item.Name.Trim(), out Guid curId))
            {
                item.Id = curId;
            }
            else
            {
                item.Code = nextCode();
                newRecords.Add(item);
            }
        }

        if (newRecords.Count > 0)
        {
            await _clientService.AddRangeAsync(newRecords, token);
        }
    }

    private async Task StorePayrollGroupsAsync(Dictionary<string, PayrollGroup> pr, CancellationToken token)
    {
        var models = pr.Values.ToList();
        if (models.Count == 0)
        {
            throw new Exception("Payroll group is required");
        }

        var stored = await _payrollGroupService.GetQueryable()
            .Select(x => new { x.Id, x.Name, x.Code })
            .ToListAsync(token);
        var existing = ByName(stored, x => x.Name, x => x.Id);
        var nextCode = CodeSequence(stored.Select(x => x.Code));

        // Matched by name alone (the imported name already carries its cutoff days, e.g.
        // "Weekly-7-14-21-28EOM"). Also requiring the stored cutoff rows to match exactly made any
        // group edited after its first import come back as a second group with the same name.
        var newRecords = new List<PayrollGroup>();
        foreach (var item in models)
        {
            if (existing.TryGetValue(item.Name.Trim(), out Guid curId))
            {
                item.Id = curId;
            }
            else
            {
                item.Code = nextCode();
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
        if (models.Count == 0) return;

        var stored = await _departmentService.GetQueryable()
            .Select(x => new { x.Id, x.Name, x.Code })
            .ToListAsync(token);
        var existing = ByName(stored, x => x.Name, x => x.Id);
        var nextCode = CodeSequence(stored.Select(x => x.Code));

        var newRecords = new List<Department>();
        foreach (var item in models)
        {
            if (existing.TryGetValue(item.Name.Trim(), out Guid curId))
            {
                item.Id = curId;
            }
            else
            {
                item.Code = nextCode();
                newRecords.Add(item);
            }
        }
        if (newRecords.Count > 0)
        {
            await _departmentService.Repository.AddRangeAsync(newRecords, token);
        }
    }

    private async Task StoreProjectSitesAsync(Dictionary<string, CostCenters> sites, CancellationToken token)
    {
        var models = sites.Values.ToList();
        if (models.Count == 0) return;

        var stored = await _costCenterService.GetQueryable()
            .Select(x => new { x.Id, x.Name, x.BranchId, x.Code })
            .ToListAsync(token);
        var existing = ByName(stored, x => x.Name, x => x);
        var nextCode = CodeSequence(stored.Select(x => x.Code));

        var newRecords = new List<CostCenters>();
        var branchBackfill = new Dictionary<Guid, Guid>();
        foreach (var item in models)
        {
            if (existing.TryGetValue(item.Name.Trim(), out var match))
            {
                item.Id = match.Id;
                // A site saved by an earlier import has no Branch -- give it this file's, so it
                // shows up under that Branch. A site that already has one keeps it.
                if (match.BranchId == null && item.BranchId != null)
                {
                    branchBackfill[match.Id] = item.BranchId.Value;
                }
            }
            else
            {
                item.Code = nextCode();
                newRecords.Add(item);
            }
        }

        if (branchBackfill.Count > 0)
        {
            var ids = branchBackfill.Keys.ToList();
            var sitesToFix = await _costCenterService.GetQueryable(x => ids.Contains(x.Id), noTracking: false)
                .ToListAsync(token);
            foreach (var site in sitesToFix)
            {
                site.BranchId = branchBackfill[site.Id];
            }
        }
        if (newRecords.Count > 0)
        {
            await _costCenterService.Repository.AddRangeAsync(newRecords, token);
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
            .Where(x => TryGetShiftTimes(x, out _, out _))
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
                TryGetShiftTimes(x, out var startTime, out var endTime);

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
    // A row only gets a shift when both AMIN and PM OUT hold a real time -- blank or
    // unparseable means "no shift" (TimeShiftId stays null), never a default 8:00-17:00.
    private static bool TryGetShiftTimes(EmployeeImportModel x, out TimeSpan start, out TimeSpan end)
    {
        var amIn = ParseTime(x.AMIn);
        var pmOut = ParseTime(x.PMOut);
        start = amIn ?? default;
        end = pmOut ?? default;
        return amIn.HasValue && pmOut.HasValue;
    }

    private static TimeSpan? GetLunchOut(EmployeeImportModel x) => ParseTime(x.AmOut);

    private static TimeSpan? GetLunchIn(EmployeeImportModel x) => ParseTime(x.PMIn);

    // "08:00:00" (what ConvertCellValue produces from a real Excel time) or, for a time typed as
    // text, a clock time such as "8:00 AM".
    private static TimeSpan? ParseTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var span)) return span;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.NoCurrentDateDefault, out var clock))
            return clock.TimeOfDay;
        return null;
    }

    // Client is optional: a blank Client Name means "no client", never a placeholder client.
    // Keyed by trimmed name, case-insensitively -- the same way StoreClientsAsync matches
    // existing clients -- so "Acme" and " acme " resolve to one client.
    private Dictionary<string, Client> ExtractClients(List<EmployeeImportModel> data)
    {
        return data
            .Where(x => !string.IsNullOrWhiteSpace(x.ClientName))
            .GroupBy(x => x.ClientName!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => new Client { Name = g.Key }, StringComparer.OrdinalIgnoreCase);
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
                x.Eom1,
                x.Eom2,
                x.Eom3,
                x.Eom4
            })
            .Select(g =>
            {
                var sample = g.First();
                var formattedName = BuildPayrollGroupName(
                    sample.PayrollGroup,
                    (sample.Cutoff1, sample.Eom1),
                    (sample.Cutoff2, sample.Eom2),
                    (sample.Cutoff3, sample.Eom3),
                    (sample.Cutoff4, sample.Eom4));
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
            (item.Cutoff1, item.Eom1),
            (item.Cutoff2, item.Eom2),
            (item.Cutoff3, item.Eom3),
            (item.Cutoff4, item.Eom4)).ToLowerInvariant();
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
                    new CutoffDay(){ Day = item.Cutoff1, IsEndOfMonth = item.Eom1 }
                };
            case PayrollFrequency.WEEKLY:
                return new List<CutoffDay>()
                {
                    new CutoffDay(){ Day = item.Cutoff1, IsEndOfMonth = item.Eom1 },
                    new CutoffDay(){ Day = item.Cutoff2, IsEndOfMonth = item.Eom2 },
                    new CutoffDay(){ Day = item.Cutoff3, IsEndOfMonth = item.Eom3 },
                    new CutoffDay(){ Day = item.Cutoff4, IsEndOfMonth = item.Eom4 },
                };
            case PayrollFrequency.SEMI_MONTHLY:
                return new List<CutoffDay>()
                {
                    new CutoffDay(){ Day = item.Cutoff1, IsEndOfMonth = item.Eom1 },
                    new CutoffDay(){ Day = item.Cutoff2, IsEndOfMonth = item.Eom2 },
                };
            case PayrollFrequency.MONTHLY:
                return new List<CutoffDay>()
                {
                    new CutoffDay(){ Day = item.Cutoff1, IsEndOfMonth = item.Eom1 },
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
        // Case-insensitive like every other master record: "HR" and "hr" in one file are one
        // department, not two.
        return data
            .GroupBy(x => string.IsNullOrWhiteSpace(x.DepartmentName) ? "--" : x.DepartmentName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new Department
            {
                Name = g.Key,
            })
            .ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
    }

    // Unlike Department, a blank Project Site cell means "no Project Site" -- AreaId is already
    // nullable and there's no reason to conjure a placeholder CostCenters row for it.
    // Each site takes the Branch of the first row that uses it: every Branch -> Project Site
    // cascade in the app (employee form, DTR, attendance, devices) only lists sites whose
    // BranchId matches, so a site saved without one looked untagged on every employee.
    private Dictionary<string, CostCenters> ExtractProjectSites(List<EmployeeImportModel> data, List<(Guid Id, string Code)> branches)
    {
        return data
            .Where(x => !string.IsNullOrWhiteSpace(x.ProjectSiteName))
            .GroupBy(x => x.ProjectSiteName!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new CostCenters
            {
                Name = g.Key,
                BranchId = ResolveBranchId(g.First(), branches),
            })
            .ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
    }

    // The row's Branch Code when it matches a branch, else the first branch found in the file,
    // else none.
    private static Guid? ResolveBranchId(EmployeeImportModel item, List<(Guid Id, string Code)> branches)
    {
        if (branches.Count == 0) return null;
        var match = branches.FirstOrDefault(x => x.Code == item.BranchCode);
        return match.Equals(default) ? branches[0].Id : match.Id;
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
    // The existing EmployeeSetting row's own Id (one-to-one, unique-indexed on EmployeeId) --
    // needed so PersistAsync can re-key a re-imported employee's fresh `new EmployeeSetting`
    // onto the row that already exists instead of EF trying to INSERT a second one for the
    // same EmployeeId. See PersistAsync's existing-employee branch.
    public Guid? SettingsId { get; set; }
    /// <summary>The number the employee already has -- a re-import must not blank it.</summary>
    public string EmployeeNo { get; set; } = string.Empty;
    public bool IsConfidential { get; set; }
    // The caller can't see this (confidential) employee -- rows matching it are rejected.
    public bool IsRestricted { get; set; }
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
    public string? ProjectSiteName { get; set; }
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
    public decimal CashBond { get; set; }
    public DateOnly? HireDate { get; set; }
    public DateTime? DateOfBirth { get; set; }

    public int Cutoff1 { get; set; }
    public int Cutoff2 { get; set; }
    public int Cutoff3 { get; set; }
    public int Cutoff4 { get; set; }

    public bool Eom1 { get; set; }
    public bool Eom2 { get; set; }
    public bool Eom3 { get; set; }
    public bool Eom4 { get; set; }
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
        using var ownedWorkbook = workbook;
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
        using var ownedWorkbook = workbook;

        const int headerRow = HeaderRow;
        const int firstDataRow = SampleRow;

        // Header text here must match the real template's row-2 headers exactly (whitespace
        // included -- the lookup below is case-insensitive but not space-insensitive) and the
        // same header text AddMapping (above, in EmployeeImportService's own mapper setup) binds
        // on upload, so a fixed-up file re-uploads through the normal endpoint unchanged. Kept in
        // the same left-to-right order as the template's actual columns for easier upkeep.
        (string Header, Action<IXLCell, EmployeeImportPreviewRow> Write)[] fieldWriters =
        {
            (EmployeeTemplateHeaders.BioId, (c, r) => c.Value = r.BioId),
            (EmployeeTemplateHeaders.BranchCode, (c, r) => c.Value = r.BranchCode),
            (EmployeeTemplateHeaders.FirstName, (c, r) => c.Value = r.FirstName),
            (EmployeeTemplateHeaders.MiddleName, (c, r) => c.Value = r.MiddleName),
            (EmployeeTemplateHeaders.LastName, (c, r) => c.Value = r.LastName),
            (EmployeeTemplateHeaders.Suffix, (c, r) => c.Value = r.Suffix),
            (EmployeeTemplateHeaders.Gender, (c, r) => c.Value = r.Gender),
            (EmployeeTemplateHeaders.RestDay1, (c, r) => c.Value = r.RestDay1),
            (EmployeeTemplateHeaders.RestDay2, (c, r) => c.Value = r.RestDay2),
            (EmployeeTemplateHeaders.DepartmentName, (c, r) => c.Value = r.DepartmentName),
            (EmployeeTemplateHeaders.ProjectSite, (c, r) => c.Value = r.ProjectSiteName),
            (EmployeeTemplateHeaders.ClientName, (c, r) => c.Value = r.ClientName),
            (EmployeeTemplateHeaders.PayrollGroup, (c, r) => c.Value = r.PayrollGroup),
            (EmployeeTemplateHeaders.Cutoff1, (c, r) => c.Value = r.Cutoff1),
            (EmployeeTemplateHeaders.Eom1, (c, r) => c.Value = r.Eom1),
            (EmployeeTemplateHeaders.Cutoff2, (c, r) => c.Value = r.Cutoff2),
            (EmployeeTemplateHeaders.Eom2, (c, r) => c.Value = r.Eom2),
            (EmployeeTemplateHeaders.Cutoff3, (c, r) => c.Value = r.Cutoff3),
            (EmployeeTemplateHeaders.Eom3, (c, r) => c.Value = r.Eom3),
            (EmployeeTemplateHeaders.Cutoff4, (c, r) => c.Value = r.Cutoff4),
            (EmployeeTemplateHeaders.Eom4, (c, r) => c.Value = r.Eom4),
            (EmployeeTemplateHeaders.ShiftName, (c, r) => c.Value = r.ShiftName),
            (EmployeeTemplateHeaders.ShiftType, (c, r) => c.Value = r.ShiftType),
            (EmployeeTemplateHeaders.AmIn, (c, r) => c.Value = r.AMIn),
            (EmployeeTemplateHeaders.AmOut, (c, r) => c.Value = r.AmOut),
            (EmployeeTemplateHeaders.PmIn, (c, r) => c.Value = r.PMIn),
            (EmployeeTemplateHeaders.PmOut, (c, r) => c.Value = r.PMOut),
            (EmployeeTemplateHeaders.BreakDuration, (c, r) => c.Value = r.BreakDuration),
            (EmployeeTemplateHeaders.MaxWorkingMinutes, (c, r) => c.Value = r.MaxWorkingMinutes),
            (EmployeeTemplateHeaders.PaidLunchBreak, (c, r) => c.Value = r.PaidLunchBreak),
            (EmployeeTemplateHeaders.SalaryType, (c, r) => c.Value = r.SalaryType),
            (EmployeeTemplateHeaders.MonthlyRate, (c, r) => c.Value = r.MonthlyRate),
            (EmployeeTemplateHeaders.DailyRate, (c, r) => c.Value = r.DailyRate),
            (EmployeeTemplateHeaders.CashBond, (c, r) => c.Value = r.CashBond),
            (EmployeeTemplateHeaders.HireDate, (c, r) => { if (r.HireDate.HasValue) c.Value = r.HireDate.Value.ToDateTime(TimeOnly.MinValue); }),
            (EmployeeTemplateHeaders.Sss, (c, r) => c.Value = r.SSS),
            (EmployeeTemplateHeaders.Phic, (c, r) => c.Value = r.PHIC),
            (EmployeeTemplateHeaders.Hdmf, (c, r) => c.Value = r.HDMF),
            (EmployeeTemplateHeaders.Tin, (c, r) => c.Value = r.TIN),
            (EmployeeTemplateHeaders.Email, (c, r) => c.Value = r.Email),
            (EmployeeTemplateHeaders.DateBirth, (c, r) => { if (r.DateOfBirth.HasValue) c.Value = r.DateOfBirth.Value; }),
            (EmployeeTemplateHeaders.ContactNumber, (c, r) => c.Value = r.ContactNo),
            (EmployeeTemplateHeaders.CivilStatus, (c, r) => c.Value = r.CivilStatus),
            (EmployeeTemplateHeaders.BloodType, (c, r) => c.Value = r.BloodType),
            (EmployeeTemplateHeaders.Address1, (c, r) => c.Value = r.Address1),
            (EmployeeTemplateHeaders.Address2, (c, r) => c.Value = r.Address2),
        };

        var lastHeaderCol = worksheet.Row(headerRow).LastCellUsed()?.Address.ColumnNumber ?? 1;
        var columnByHeader = MapHeaderColumns(worksheet);

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

    // Row 1 of the template is a merged "Instructions" banner; the real column headers are on
    // row 2 (what MapFields' header-text mapping keys off on upload), and row 3 is the sample row.
    private const int HeaderRow = 2;
    private const int SampleRow = 3;

    // Header text -> column number, from the template's header row (case-insensitive).
    private static Dictionary<string, int> MapHeaderColumns(IXLWorksheet worksheet)
    {
        var lastHeaderCol = worksheet.Row(HeaderRow).LastCellUsed()?.Address.ColumnNumber ?? 1;
        var columnByHeader = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var col = 1; col <= lastHeaderCol; col++)
        {
            var text = worksheet.Cell(HeaderRow, col).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(text) && !columnByHeader.ContainsKey(text))
            {
                columnByHeader[text] = col;
            }
        }
        return columnByHeader;
    }

    // Prefills the sample row by HEADER, never by cell address -- the template has gained
    // columns over time, and hard-coded addresses ("AE3") drifted onto the wrong columns (today's
    // date landed in Monthly Rate). A header missing from the template is skipped, not thrown on.
    private async Task<(XLWorkbook Workbook, IXLWorksheet Worksheet)> BuildTemplateWorkbookAsync(CancellationToken token)
    {
        string templatePath = Path.Combine(_environment.ContentRootPath, "wwwroot", "Templates", "employee_template.xlsx");
        var branchList = await _branchService
            .GetQueryable()
            .Select(x => x.Code)
            .ToListAsync(token);

        var departments = await _departmentService
            .GetQueryable()
            .Select(x => x.Name)
            .ToListAsync(token);

        var payrollGroups = await _payrollGroupService
            .GetQueryable()
            .Select(x => x.Name)
            .ToListAsync(token);

        var workbook = new XLWorkbook(templatePath);
        var worksheet = workbook.Worksheet(1);
        var columns = MapHeaderColumns(worksheet);

        AddListDropdown(workbook, worksheet, columns, EmployeeTemplateHeaders.BranchCode, "Branches",
            branchList, branchList.FirstOrDefault());

        // Rest Days -- day-name dropdown plus a blank entry, so employees without a configured
        // rest day can leave Rest Day 1 / Rest Day 2 empty instead of being forced to pick a
        // day (a blank cell is already treated as "no rest day", not an error, by
        // ExtractRestDay/UploadAsync above).
        var dayNames = new List<string> { "" };
        dayNames.AddRange(Enum.GetNames(typeof(DayName)));
        var restDays = AddHelperSheet(workbook, "RestDays", dayNames);
        SetSampleDropdown(worksheet, columns, EmployeeTemplateHeaders.RestDay1, restDays, "Saturday");
        SetSampleDropdown(worksheet, columns, EmployeeTemplateHeaders.RestDay2, restDays, "Sunday");

        AddListDropdown(workbook, worksheet, columns, EmployeeTemplateHeaders.DepartmentName, "Departments",
            departments, departments.FirstOrDefault());

        AddListDropdown(workbook, worksheet, columns, EmployeeTemplateHeaders.PayrollGroup, "payrollgroups",
            payrollGroups,
            payrollGroups.FirstOrDefault(x => x.Contains("Semi-Monthly", StringComparison.OrdinalIgnoreCase))
                ?? payrollGroups.FirstOrDefault());

        AddListDropdown(workbook, worksheet, columns, EmployeeTemplateHeaders.ShiftType, "ShiftTypes",
            ["Fixed", "Split"], "Fixed");

        var trueFalse = AddHelperSheet(workbook, "trueFalse", ["False", "True"]);
        foreach (var header in new[]
                 {
                     EmployeeTemplateHeaders.Eom1, EmployeeTemplateHeaders.Eom2,
                     EmployeeTemplateHeaders.Eom3, EmployeeTemplateHeaders.Eom4,
                     EmployeeTemplateHeaders.PaidLunchBreak,
                 })
        {
            SetSampleDropdown(worksheet, columns, header, trueFalse, "False");
        }

        AddListDropdown(workbook, worksheet, columns, EmployeeTemplateHeaders.SalaryType, "SalaryType",
            ["Variable", "Fixed"], "Fixed");

        // Company-local "today" -- UTC's date is still yesterday before 8 AM in Manila.
        if (columns.TryGetValue(EmployeeTemplateHeaders.HireDate, out var hireDateColumn))
        {
            var manila = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
            worksheet.Cell(SampleRow, hireDateColumn).Value = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, manila).Date;
        }

        return (workbook, worksheet);
    }

    // A dropdown whose source is its own hidden helper sheet. Nothing is added when there are no
    // values (e.g. a new company with no branches yet) -- a list over an empty range is broken.
    private static void AddListDropdown(XLWorkbook workbook, IXLWorksheet worksheet, Dictionary<string, int> columns,
        string header, string helperSheetName, List<string> values, string? defaultValue)
    {
        if (values.Count == 0) return;
        var source = AddHelperSheet(workbook, helperSheetName, values);
        SetSampleDropdown(worksheet, columns, header, source, defaultValue);
    }

    private static IXLRange AddHelperSheet(XLWorkbook workbook, string name, List<string> values)
    {
        var helperSheet = workbook.Worksheets.Add(name);
        helperSheet.Cell(1, 1).InsertData(values);
        helperSheet.Hide();
        return helperSheet.Range(1, 1, values.Count, 1);
    }

    private static void SetSampleDropdown(IXLWorksheet worksheet, Dictionary<string, int> columns,
        string header, IXLRange source, string? defaultValue)
    {
        if (!columns.TryGetValue(header, out var column)) return;
        var cell = worksheet.Cell(SampleRow, column);
        cell.CreateDataValidation().List(source);
        if (defaultValue != null) cell.Value = defaultValue;
    }}

/// <summary>
/// Column headers of employee_template.xlsx (row 2), exactly as written there. The upload
/// mapping (MapFields), the error template (GetEmployeeTemplateWithErrors) and the sample-row
/// prefill (BuildTemplateWorkbookAsync) all locate columns by these, never by cell address --
/// so a column inserted into the template can't shift one value under another header.
/// </summary>
public static class EmployeeTemplateHeaders
{
    public const string BioId = "BioId";
    public const string BranchCode = "Branch Code";
    public const string FirstName = "FirstName";
    public const string MiddleName = "MiddleName";
    public const string LastName = "LastName";
    public const string Suffix = "Suffix";
    public const string Gender = "Gender";
    public const string RestDay1 = "Rest Day 1";
    public const string RestDay2 = "Rest Day 2";
    public const string DepartmentName = "Department Name";
    public const string ProjectSite = "Project Site";
    public const string ClientName = "Client Name";
    public const string PayrollGroup = "Payroll Group";
    public const string Cutoff1 = "Cut-Off 1";
    public const string Cutoff2 = "Cut-Off 2";
    public const string Cutoff3 = "Cut-Off 3";
    public const string Cutoff4 = "Cut-Off 4";
    public const string Eom1 = "EOM 1";
    public const string Eom2 = "EOM 2";
    public const string Eom3 = "EOM 3";
    public const string Eom4 = "EOM 4";
    public const string ShiftName = "Shift Name";
    public const string ShiftType = "Shift Type";
    public const string AmIn = "AMIN";
    public const string AmOut = "AM Out";
    public const string PmIn = "PM In";
    public const string PmOut = "PM OUT";
    public const string BreakDuration = "Break Duration";
    public const string MaxWorkingMinutes = "Max Working Minutes";
    public const string PaidLunchBreak = "Paid Lunch Break";
    public const string SalaryType = "SalaryType";
    public const string MonthlyRate = "Monthly Rate";
    public const string DailyRate = "Daily Rate";
    public const string CashBond = "Cash Bond";
    public const string HireDate = "Hire Date";
    public const string Sss = "SSS";
    public const string Phic = "PHIC";
    public const string Hdmf = "HDMF";
    public const string Tin = "TIN";
    public const string Email = "Email";
    public const string DateBirth = "Date Birth";
    public const string ContactNumber = "Contact Number";
    public const string CivilStatus = "Civil Status";
    public const string BloodType = "Blood Type";
    public const string Address1 = "Address 1";
    public const string Address2 = "Address 2";
    public const string BankName = "Bank Name";
    public const string BankNo = "Bank No";
}
