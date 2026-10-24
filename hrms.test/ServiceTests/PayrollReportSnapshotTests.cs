using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// Characterization ("golden file") tests for PayrollReportService. The report queries were
/// rewritten to aggregate in SQL and read only the columns they need instead of loading every
/// full Payroll row; these snapshots were recorded from the ORIGINAL in-memory implementation,
/// so any difference in the rewritten reports' output fails here.
///
/// Fixture covers: several employees (one soft-deleted, one opening-balance-only), Regular /
/// 13th month / year-end adjustment runs, posted vs unposted, rows either side of year
/// boundaries (PayPeriodStart vs PostingPeriod years differ), a minimum-wage earner, hazard pay,
/// clients, branches, departments and deduction schedules.
///
/// To re-record after an intentional change: set UPDATE_SNAPSHOTS=1 and run these tests.
/// </summary>
public class PayrollReportSnapshotTests
{
    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:D12}");

    private static readonly Guid PayrollGroup = Id(1);
    private static readonly Guid DeptA = Id(2), DeptB = Id(3);
    private static readonly Guid BranchNcr = Id(4);
    private static readonly Guid ClientX = Id(5);
    private static readonly Guid Batch = Id(6);
    private static readonly Guid Ana = Id(10), Ben = Id(11), Cy = Id(12), Dee = Id(13);
    private static readonly Guid HazardIncome = Id(20), HazardApp = Id(21);
    private static readonly Guid LoanType = Id(30), Loan = Id(31), LoanApp = Id(32);

    private static async Task<SqliteHrmsContext> SeedAsync()
    {
        var db = new SqliteHrmsContext();
        await using var seed = db.NewContext();

        seed.PayrollGroups.Add(new PayrollGroup { Id = PayrollGroup, Name = "Semi-Monthly" });
        seed.Departments.AddRange(
            new Department { Id = DeptA, Name = "Accounting" },
            new Department { Id = DeptB, Name = "Operations" });
        seed.Branches.Add(new Branch { Id = BranchNcr, Code = "MNL", Name = "Manila", RegionCode = "NCR" });
        seed.Clients.Add(new Client { Id = ClientX, Name = "Client X" });
        seed.MinimumWageRates.Add(new MinimumWageRate
        {
            Id = Id(7), RegionCode = "NCR", RegionName = "NCR", DailyRate = 645m, EffectiveDate = new DateOnly(2025, 1, 1),
        });
        seed.PayrollBatches.Add(new PayrollBatch { Id = Batch, PayPeriodStart = new DateOnly(2026, 1, 1), PayPeriodEnd = new DateOnly(2026, 12, 31) });

        seed.Employees.AddRange(
            Employee(Ana, "E-001", "Ana", "Alpha", DeptA, BranchNcr, tin: "111-111", bank: "BDO", bankNo: "123", cashBond: 100m, middle: "Maria", suffix: "Jr"),
            Employee(Ben, "E-002", "Ben", "Bravo", DeptB, null, middle: "Bautista"),
            Employee(Cy, "E-003", "Cy", "Charlie", DeptA, BranchNcr),
            Employee(Dee, "E-004", "Dee", "Delta", DeptB, null, deleted: true));

        seed.Payrolls.AddRange(
            // Ana: two posted January cutoffs (Jan 1-15 a minimum wage earner, Jan 16-31 not).
            Payroll(100, Ana, PayrollType.Regular, new(2026, 1, 1), new(2026, 1, 15), new(2026, 1, 15), posted: true, k: 1, dailyRate: 600m, client: ClientX),
            Payroll(101, Ana, PayrollType.Regular, new(2026, 1, 16), new(2026, 1, 31), new(2026, 1, 31), posted: true, k: 2, dailyRate: 700m),
            // Ana: posted February cutoff as a minimum wage earner, and an UNPOSTED one.
            Payroll(102, Ana, PayrollType.Regular, new(2026, 2, 1), new(2026, 2, 15), new(2026, 2, 15), posted: true, k: 3, dailyRate: 600m),
            Payroll(103, Ana, PayrollType.Regular, new(2026, 2, 16), new(2026, 2, 28), new(2026, 2, 28), posted: false, k: 4, dailyRate: 600m),
            // Ana: 13th month and a year-end tax refund, both posted in December.
            Payroll(104, Ana, PayrollType.ThirteenthMonth, new(2026, 12, 1), new(2026, 12, 31), new(2026, 12, 20), posted: true, k: 5),
            Payroll(105, Ana, PayrollType.YearEndAdjustment, new(2026, 12, 1), new(2026, 12, 31), new(2026, 12, 31), posted: true, k: 0, withholdingTax: -100m),
            // Ana: a December 2025 cutoff credited in January 2026 (year boundary).
            Payroll(106, Ana, PayrollType.Regular, new(2025, 12, 16), new(2025, 12, 31), new(2026, 1, 5), posted: true, k: 6, dailyRate: 700m),
            // Ben: posted January cutoff, and a December 2026 cutoff credited in 2027.
            Payroll(107, Ben, PayrollType.Regular, new(2026, 1, 1), new(2026, 1, 15), new(2026, 1, 15), posted: true, k: 7, dailyRate: 900m),
            Payroll(108, Ben, PayrollType.Regular, new(2026, 12, 16), new(2026, 12, 31), new(2027, 1, 5), posted: true, k: 8, dailyRate: 900m),
            // Dee (deleted employee): posted January cutoff.
            Payroll(109, Dee, PayrollType.Regular, new(2026, 1, 1), new(2026, 1, 15), new(2026, 1, 15), posted: true, k: 9, dailyRate: 800m),
            // A 2025-only row that no 2026 report may pick up.
            Payroll(110, Ben, PayrollType.Regular, new(2025, 6, 1), new(2025, 6, 15), new(2025, 6, 15), posted: true, k: 10, dailyRate: 900m));

        seed.PayrollOpeningBalances.AddRange(
            new PayrollOpeningBalance
            {
                Id = Id(200), EmployeeId = Ana, Year = 2026, BasicPay = 5000m, OvertimePay = 50m, HolidayPay = 20m, Allowances = 30m,
                OtherIncome = 10m, Bonuses = 40m, GrossIncome = 5150m, NonTaxableIncome = 60m, SSSContribution = 70m,
                PhilHealthContribution = 35m, PagIbigContribution = 15m, WithholdingTax = 120m, OtherDeductions = 5m,
                TotalDeductions = 245m, NetPay = 4905m,
            },
            new PayrollOpeningBalance
            {
                Id = Id(201), EmployeeId = Cy, Year = 2026, BasicPay = 8000m, GrossIncome = 8000m, SSSContribution = 100m,
                PhilHealthContribution = 50m, PagIbigContribution = 20m, WithholdingTax = 300m, TotalDeductions = 470m, NetPay = 7530m,
            });

        seed.Allowances.Add(new OtherIncome { Id = HazardIncome, Code = "HAZ", Name = "Hazard Pay", IsHazardPay = true });
        seed.Set<OtherIncomeApplication>().Add(new OtherIncomeApplication
        {
            Id = HazardApp, EmployeeId = Ana, IncomeId = HazardIncome, ProcessBy = "", Remarks = "",
            StartDate = new DateOnly(2026, 2, 1), EndDate = new DateOnly(2026, 2, 28),
        });
        seed.OtherIncomeApplicationDetails.AddRange(
            new OtherIncomeSchedules { Id = Id(210), EmployeeId = Ana, OtherIncomeApplicationId = HazardApp, IncomeId = HazardIncome, Date = new DateOnly(2026, 2, 10), Amount = 50m },
            new OtherIncomeSchedules { Id = Id(211), EmployeeId = Ana, OtherIncomeApplicationId = HazardApp, IncomeId = HazardIncome, Date = new DateOnly(2026, 3, 10), Amount = 70m });

        seed.DeductionTypes.Add(new DeductionType { Id = LoanType, Code = "LN", Name = "Loans" });
        seed.Deductions.Add(new Deduction { Id = Loan, CategoryId = LoanType, Code = "SAL", Name = "Salary Loan" });
        seed.DeductionApplications.Add(new DeductionApplication
        {
            Id = LoanApp, EmployeeId = Ana, DeductionId = Loan, TotalPrincipal = 3000m, InterestRate = 0.02m,
            StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 3, 31), ProcessBy = "", Note = "", Remarks = "",
        });
        seed.DeductionApplicationDetails.AddRange(
            new DeductionApplicationDetail { Id = Id(220), EmployeeId = Ana, DeductionId = Loan, DeductionApplicationId = LoanApp, Date = new DateOnly(2026, 1, 15), RecordOrder = 1, Balance = 2000m },
            new DeductionApplicationDetail { Id = Id(221), EmployeeId = Ana, DeductionId = Loan, DeductionApplicationId = LoanApp, Date = new DateOnly(2026, 1, 31), RecordOrder = 2, Balance = 1000m },
            new DeductionApplicationDetail { Id = Id(222), EmployeeId = Ana, DeductionId = Loan, DeductionApplicationId = LoanApp, Date = new DateOnly(2026, 2, 15), RecordOrder = 3, Balance = 0m });

        await seed.SaveChangesAsync();
        return db;
    }

    private static Employee Employee(Guid id, string no, string first, string last, Guid dept, Guid? branch,
        string tin = "", string bank = "", string bankNo = "", decimal cashBond = 0m, bool deleted = false,
        string middle = "", string suffix = "") => new()
    {
        Id = id, EmployeeNo = no, FirstName = first, LastName = last, MiddleName = middle, Suffix = suffix, PayrollGroupId = PayrollGroup,
        DepartmentId = dept, BranchId = branch, TIN = tin, BankName = bank, BankNo = bankNo, CashBond = cashBond,
        ModeOfPayment = string.IsNullOrEmpty(bank) ? PaymentMethod.Cash : PaymentMethod.ATM,
        DeletedAt = deleted ? new DateTime(2026, 3, 1) : null,
    };

    // Every summed field gets a distinct value derived from k, so a mixed-up column shows up.
    private static Payroll Payroll(int n, Guid employee, PayrollType type, DateOnly start, DateOnly end, DateOnly posting,
        bool posted, int k, decimal dailyRate = 0m, Guid? client = null, decimal? withholdingTax = null) => new()
    {
        Id = Id(n), PayrollBatchId = Batch, EmployeeId = employee, PayrollType = type,
        PayPeriodStart = start, PayPeriodEnd = end, PostingPeriod = posting, IsPosted = posted,
        PayrollGroupId = PayrollGroup, ClientId = client, SalaryType = SalaryType.VARIABLE, DailyRate = dailyRate,
        BasicPay = 1000m * k + 0.25m, OvertimePay = 101m * k, HolidayPay = 102m * k, NightDifferentialPay = 11m * k,
        NightDifferentialOTPay = 12m * k, TotalRegularAllowances = 103m * k, TotalOtherIncome = 104m * k,
        TotalBonuses = 105m * k, TotalDeminimises = 13m * k, GrossIncome = 2000m * k + 0.5m,
        NonTaxableIncome = 106m * k, TaxableIncome = 1500m * k, NonTaxableBenefits = 107m * k, TaxableBenefits = 108m * k,
        SSSContribution = 109m * k, PhilHealthContribution = 110m * k, PagIbigContribution = 111m * k,
        WithholdingTax = withholdingTax ?? 112m * k, OtherDeductions = 113m * k, TotalDeductions = 500m * k,
        NetPay = 1500m * k + 0.5m, CashBondDeduction = 14m * k,
        EmployerSSSContribution = 115m * k, EmployerPhilHealthContribution = 116m * k,
        EmployerPagIbigContribution = 117m * k, EmployerECContribution = 10m * k,
    };

    private static PayrollReportService BuildService(HrmsContext context)
    {
        // A private Mapster config: the reports don't map through it, and scanning the shared
        // GlobalSettings from parallel test classes races ("Adapt was already called").
        var uow = new UnitOfWorkService(context);
        var employeeService = new EmployeeService(
            uow, Substitute.For<IMapper>(), new TypeAdapterConfig(),
            new DepartmentService(uow), new PayrollGroupService(uow), new BranchService(uow),
            new CostCenterService(uow), new PositionService(uow), new SectionService(uow));
        return new PayrollReportService(uow, employeeService, new PayrollOpeningBalanceService(uow));
    }

    private static async Task SnapshotAsync(string name, Func<PayrollReportService, Task<object?>> report)
    {
        using var db = await SeedAsync();
        await using var context = db.NewContext();
        var result = await report(BuildService(context));
        AssertMatchesSnapshot(name, result);
    }

    public static IEnumerable<object[]> Reports() =>
    [
        ["bank-disbursement-jan-feb"],
        ["cost-summary-department"], ["cost-summary-branch"], ["cost-summary-client"],
        ["ytd-2026"], ["ytd-2026-ana"],
        ["13th-2026"], ["13th-2026-asof-jan20"], ["13th-2026-ben"],
        ["tax-annualization-2026"],
        ["1601c-jan"], ["1601c-feb"], ["1601c-dec"],
        ["alphalist-2026"],
        ["2316-ana-2026"], ["2316-cy-2026"], ["2316-nobody-2026"],
        ["cash-bond"], ["deduction-ledger"],
    ];

    // Balance is per installment (Amount until paid, then 0), so the ledger's outstanding figure is
    // the sum of the open ones -- not the Balance of a single row -- and a loan that has not begun or
    // is fully paid is not listed.
    [Fact]
    public async Task DeductionLedger_SumsOpenInstallments_AndOmitsPaidOrNotYetStartedLoans()
    {
        using var db = await SeedAsync();
        await using (var seed = db.NewContext())
        {
            var paidApp = Id(40);
            var futureApp = Id(41);
            seed.DeductionApplications.AddRange(
                new DeductionApplication { Id = paidApp, EmployeeId = Ben, DeductionId = Loan, TotalPrincipal = 100m, StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 31), ProcessBy = "", Note = "", Remarks = "" },
                new DeductionApplication { Id = futureApp, EmployeeId = Ben, DeductionId = Loan, TotalPrincipal = 100m, StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2026, 6, 30), ProcessBy = "", Note = "", Remarks = "" });
            seed.DeductionApplicationDetails.AddRange(
                new DeductionApplicationDetail { Id = Id(230), EmployeeId = Ben, DeductionId = Loan, DeductionApplicationId = paidApp, Date = new DateOnly(2026, 1, 15), RecordOrder = 1, Amount = 100m, Balance = 0m },
                new DeductionApplicationDetail { Id = Id(231), EmployeeId = Ben, DeductionId = Loan, DeductionApplicationId = futureApp, Date = new DateOnly(2026, 6, 15), RecordOrder = 1, Amount = 100m, Balance = 100m });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var result = await BuildService(context).GetDeductionLedgerAsync(new(2026, 2, 28), default);

        result.Should().ContainSingle();
        result[0].EmployeeId.Should().Be(Ana);
        result[0].CurrentBalance.Should().Be(3000m);
    }

    [Theory]
    [MemberData(nameof(Reports))]
    public Task Report_MatchesRecordedOutput(string name) => SnapshotAsync(name, name switch
    {
        "bank-disbursement-jan-feb" => async s => await s.GetBankDisbursementAsync(new(2026, 1, 1), new(2026, 2, 28), default),
        "cost-summary-department" => async s => await s.GetCostSummaryAsync(new(2026, 1, 1), new(2026, 12, 31), "department", default),
        "cost-summary-branch" => async s => await s.GetCostSummaryAsync(new(2026, 1, 1), new(2026, 12, 31), "branch", default),
        "cost-summary-client" => async s => await s.GetCostSummaryAsync(new(2026, 1, 1), new(2026, 12, 31), "client", default),
        "ytd-2026" => async s => await s.GetYtdSummaryAsync(2026, null, default),
        "ytd-2026-ana" => async s => await s.GetYtdSummaryAsync(2026, Ana, default),
        "13th-2026" => async s => await s.GetThirteenthMonthAsync(2026, default),
        "13th-2026-asof-jan20" => async s => await s.GetThirteenthMonthAsync(2026, default, asOfDate: new DateOnly(2026, 1, 20)),
        "13th-2026-ben" => async s => await s.GetThirteenthMonthAsync(2026, default, employeeId: Ben),
        "tax-annualization-2026" => async s => await s.GetAnnualTaxAnnualizationInputsAsync(2026, default),
        "1601c-jan" => async s => await s.GetMonthlyRemittanceReturnAsync(new(2026, 1, 1), new(2026, 1, 31), false, default),
        "1601c-feb" => async s => await s.GetMonthlyRemittanceReturnAsync(new(2026, 2, 1), new(2026, 2, 28), false, default),
        "1601c-dec" => async s => await s.GetMonthlyRemittanceReturnAsync(new(2026, 12, 1), new(2026, 12, 31), true, default),
        "alphalist-2026" => async s => await s.GetAlphalistAsync(2026, default),
        "2316-ana-2026" => async s => await s.Get2316DataAsync(Ana, 2026, default),
        "2316-cy-2026" => async s => await s.Get2316DataAsync(Cy, 2026, default),
        "2316-nobody-2026" => async s => await s.Get2316DataAsync(Id(999), 2026, default),
        "cash-bond" => async s => await s.GetCashBondReportAsync(new(2026, 12, 31), default),
        "deduction-ledger" => async s => await s.GetDeductionLedgerAsync(new(2026, 2, 28), default),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    });

    // ---- snapshot plumbing ----

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true, // (Summary, Employees) tuples
        Converters = { new JsonStringEnumConverter(), new NormalizedDecimalConverter() },
    };

    private static void AssertMatchesSnapshot(string name, object? result, [CallerFilePath] string testFile = "")
    {
        var actual = JsonSerializer.Serialize(result, JsonOptions).Replace("\r\n", "\n");
        var path = Path.Combine(Path.GetDirectoryName(testFile)!, "Snapshots", "PayrollReports", $"{name}.json");

        if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
            return;
        }

        File.Exists(path).Should().BeTrue($"snapshot {path} must exist (record with UPDATE_SNAPSHOTS=1)");
        actual.Should().Be(File.ReadAllText(path).Replace("\r\n", "\n"));
    }

    // SQL aggregation and C# summing can return the same amount with a different decimal scale
    // (300.75 vs 300.7500); compare values, not representation.
    private sealed class NormalizedDecimalConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDecimal();

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteRawValue(Math.Round(value, 6).ToString("0.######", CultureInfo.InvariantCulture));
    }
}
