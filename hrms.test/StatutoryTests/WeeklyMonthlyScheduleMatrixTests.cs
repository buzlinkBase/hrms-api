using hrms.test.TestSupport;

namespace hrms.test.StatutoryTests;

/// <summary>
/// The weekly and monthly counterpart of <see cref="StatutoryScheduleMatrixTests"/>: every
/// Statutory Deduction Release Schedule on a weekly group whose weeks stay within the month
/// (7 / 14 / 21 / end of month) and one whose first week crosses from the previous month
/// (5 / 12 / 19 / 26), and on a monthly group paid on the month's end and one paid on the 25th
/// (Feb 26–Mar 25), for a Fixed employee with No DTR, a Fixed employee with DTR absences and a
/// Variable employee. Each case runs the month's payrolls in order, every earlier one's payroll
/// and ledger rows fed into the next, and checks what each withholds for SSS, PhilHealth and
/// Pag-IBIG and that together they come to exactly the month's table amount.
/// </summary>
public class WeeklyMonthlyScheduleMatrixTests : TestContextBase
{
    private static readonly SSSModel[] SssBrackets =
    [
        SSSBracket(0, 19_999, 900, 1_890, 10),
        SSSBracket(20_000, 34_999, 1_350, 2_840, 10),
        SSSBracket(35_000, 999_999, 1_800, 3_790, 10),
    ];

    // PhilHealth / Pag-IBIG brackets are exactly half of SSS's EE, so one expectation covers all three.
    private static readonly PHICModel[] PhicBrackets =
    [
        PHICBracket(0, 19_999, 450, 450),
        PHICBracket(20_000, 34_999, 675, 675),
        PHICBracket(35_000, 999_999, 900, 900),
    ];

    private static readonly HDMFModel[] HdmfBrackets =
    [
        HDMFBracket(0, 19_999, 450, 450),
        HDMFBracket(20_000, 34_999, 675, 675),
        HDMFBracket(35_000, 999_999, 900, 900),
    ];

    /// <summary>A payroll group: its frequency, cutoffs and the month's periods.</summary>
    public sealed record Group(string Name, PayrollFrequency Frequency, (int Day, bool Eom)[] Cutoffs, (DateOnly From, DateOnly To)[] Periods)
    {
        public override string ToString() => Name;
    }

    private static readonly Group WeeklySameMonth = new("Weekly 7/14/21/end of month", PayrollFrequency.WEEKLY,
        [(7, false), (14, false), (21, false), (31, true)],
        [(new(2025, 3, 1), new(2025, 3, 7)), (new(2025, 3, 8), new(2025, 3, 14)),
         (new(2025, 3, 15), new(2025, 3, 21)), (new(2025, 3, 22), new(2025, 3, 31))]);

    private static readonly Group WeeklyCrossMonth = new("Weekly 5/12/19/26 (cross month)", PayrollFrequency.WEEKLY,
        [(5, false), (12, false), (19, false), (26, false)],
        [(new(2025, 2, 27), new(2025, 3, 5)), (new(2025, 3, 6), new(2025, 3, 12)),
         (new(2025, 3, 13), new(2025, 3, 19)), (new(2025, 3, 20), new(2025, 3, 26))]);

    private static readonly Group MonthlyMonthEnd = new("Monthly, end of month", PayrollFrequency.MONTHLY,
        [(31, true)],
        [(new(2025, 3, 1), new(2025, 3, 31))]);

    private static readonly Group MonthlyCrossMonth = new("Monthly 25 (cross month)", PayrollFrequency.MONTHLY,
        [(25, false)],
        [(new(2025, 2, 26), new(2025, 3, 25))]);

    /// <summary>An employee: salary type and each payroll's gross and absences.</summary>
    public sealed record Pay(string Name, SalaryType SalaryType, decimal[] Gross, decimal[] Absences)
    {
        public override string ToString() => Name;
    }

    private const decimal MonthlyRate = 30_000;

    private static Pay FixedNoDtr(int runs) => new("Fixed, No DTR", SalaryType.FIXED, Even(MonthlyRate, runs), new decimal[runs]);

    // 2,000 absent on the first payroll: the month's gross is 28,000, still bracket B.
    private static Pay FixedWithDtr(int runs)
    {
        var gross = Even(MonthlyRate, runs);
        gross[0] -= 2_000;
        var absences = new decimal[runs];
        absences[0] = 2_000;
        return new("Fixed, with DTR absences", SalaryType.FIXED, gross, absences);
    }

    private static Pay Variable(int runs) => new("Variable", SalaryType.VARIABLE, Even(MonthlyRate, runs), new decimal[runs]);

    private static decimal[] Even(decimal total, int runs) => Enumerable.Repeat(total / runs, runs).ToArray();

    /// <summary>Group, employee, schedule, and the SSS EE each payroll should withhold (month total 1,350).</summary>
    public static TheoryData<Group, Pay, StatutoryDeductionSchedule, decimal[]> Cases()
    {
        var data = new TheoryData<Group, Pay, StatutoryDeductionSchedule, decimal[]>();
        foreach (var group in new[] { WeeklySameMonth, WeeklyCrossMonth })
        {
            // Fixed: every week brackets off the month projected from the monthly rate; the
            // 50/50-style split spreads it evenly over the group's four weeks.
            foreach (var pay in new[] { FixedNoDtr(4), FixedWithDtr(4) })
            {
                data.Add(group, pay, StatutoryDeductionSchedule.PerPayroll, [337.5m, 337.5m, 337.5m, 337.5m]);
                data.Add(group, pay, StatutoryDeductionSchedule.FirstHalfMonth, [1_350, 0, 0, 0]);
                data.Add(group, pay, StatutoryDeductionSchedule.SecondHalfMonth, [0, 0, 0, 1_350]);
            }
            // Variable: each week brackets off what was earned so far (7,500 and 15,000 -> 900;
            // 22,500 and 30,000 -> 1,350), withholding only what that bracket still lacks.
            data.Add(group, Variable(4), StatutoryDeductionSchedule.PerPayroll, [900, 0, 450, 0]);
            data.Add(group, Variable(4), StatutoryDeductionSchedule.FirstHalfMonth, [900, 0, 0, 450]);
            data.Add(group, Variable(4), StatutoryDeductionSchedule.SecondHalfMonth, [0, 0, 0, 1_350]);
        }
        // Monthly: one payroll, so every schedule withholds the whole month on it.
        foreach (var group in new[] { MonthlyMonthEnd, MonthlyCrossMonth })
        {
            foreach (var schedule in new[] { StatutoryDeductionSchedule.PerPayroll, StatutoryDeductionSchedule.FirstHalfMonth, StatutoryDeductionSchedule.SecondHalfMonth })
            {
                data.Add(group, FixedNoDtr(1), schedule, [1_350]);
                data.Add(group, FixedWithDtr(1), schedule, [1_350]);
                data.Add(group, Variable(1), schedule, [1_350]);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EachScheduleWithholdsTheMonthOnce(Group group, Pay pay, StatutoryDeductionSchedule schedule, decimal[] expected)
    {
        var employee = Guid.NewGuid();
        var earlier = new List<(DeductionPayloadContext Context, DeductionPipeData Result)>();
        var withheld = new List<DeductionPipeData>();

        for (var i = 0; i < group.Periods.Length; i++)
        {
            var (from, to) = group.Periods[i];
            var ctx = Build(group, pay.SalaryType, schedule, employee, from, to, pay.Gross[i], pay.Absences[i]);
            foreach (var (before, result) in earlier) Feed(ctx, before, result);
            var run = new DeductionPipeline().Run(ctx);
            earlier.Add((ctx, run));
            withheld.Add(run);
        }

        withheld.Select(r => r.SSS.EE).Should().Equal(expected, "SSS per payroll");
        withheld.Select(r => r.PHIC.EE).Should().Equal(expected.Select(x => x / 2), "PhilHealth per payroll");
        withheld.Select(r => r.HDMF.EE).Should().Equal(expected.Select(x => x / 2), "Pag-IBIG per payroll");
        withheld.Sum(r => r.SSS.EE).Should().Be(1_350, "the month is withheld exactly once");
        withheld.Sum(r => r.SSS.ER).Should().Be(2_840, "and so is the employer's share");
    }

    public static TheoryData<Group> Groups() => new() { WeeklySameMonth, WeeklyCrossMonth, MonthlyMonthEnd, MonthlyCrossMonth };

    [Theory]
    [MemberData(nameof(Groups))]
    public void EveryPayrollOfTheMonth_IsCreditedToMarch(Group group)
    {
        foreach (var (from, to) in group.Periods)
        {
            var ctx = Build(group, SalaryType.FIXED, StatutoryDeductionSchedule.PerPayroll, Guid.NewGuid(), from, to, 7_500, 0);
            ctx.PayrollLine.StatutoryCreditDate.Month.Should().Be(3, $"{from}–{to} is March's");
        }
    }

    [Fact]
    public void Monthly25th_LastMonthsContributions_AreNotTakenOffThisMonths()
    {
        var employee = Guid.NewGuid();
        var february = Build(MonthlyCrossMonth, SalaryType.FIXED, StatutoryDeductionSchedule.PerPayroll, employee,
            new DateOnly(2025, 1, 26), new DateOnly(2025, 2, 25), 30_000, 0);
        var februaryResult = new DeductionPipeline().Run(february);
        var march = Build(MonthlyCrossMonth, SalaryType.FIXED, StatutoryDeductionSchedule.PerPayroll, employee,
            new DateOnly(2025, 2, 26), new DateOnly(2025, 3, 25), 30_000, 0);
        Feed(march, february, februaryResult);

        var marchResult = new DeductionPipeline().Run(march);

        februaryResult.SSS.EE.Should().Be(1_350);
        marchResult.SSS.EE.Should().Be(1_350, "February's payroll and contributions belong to February");
    }

    // ── Setup ────────────────────────────────────────────────────────────────

    private static DeductionPayloadContext Build(
        Group group, SalaryType salaryType, StatutoryDeductionSchedule schedule, Guid employeeId, DateOnly from, DateOnly to, decimal gross, decimal absences)
    {
        var ctx = CreateContext(salaryType, group.Frequency, gross, from, to,
            monthlyRate: MonthlyRate, schedule: schedule, employeeId: employeeId);
        foreach (var (day, eom) in group.Cutoffs) AddCutoff(ctx, day, isEndOfMonth: eom);
        ctx.PayrollLine.AbsencesAmount = absences;
        // As EmployeePayrollLineService credits the line, under the company default.
        ctx.PayrollLine.StatutoryCreditDate = StatutoryMonthResolver.CreditDate(
            ctx.Employee.PayrollGroup, from, to, CrossMonthStatutoryCreditPolicy.CutoffStartMonth, null);
        SetSSSRate(ctx, ComputationBasis.Table);
        SeedSSSTable(ctx, SssBrackets);
        SetPHICRate(ctx, ComputationBasis.Table);
        SeedPHICTable(ctx, PhicBrackets);
        SetHDMFRate(ctx, ComputationBasis.Table);
        SeedHDMFTable(ctx, HdmfBrackets);
        return ctx;
    }

    /// <summary>Feeds an earlier payroll into a later one's payload: its payroll row and its ledger rows, credited where its line was.</summary>
    private static void Feed(DeductionPayloadContext later, DeductionPayloadContext earlier, DeductionPipeData result)
    {
        var key = new EmployeeKey(later.Employee.Id);
        var id = later.Employee.Id;
        later.Payload.PostedPriorPayrolls[key] =
        [
            .. later.Payload.PostedPriorPayrolls.GetValueOrDefault(key) ?? [],
            new Payroll
            {
                EmployeeId = id, PayrollType = PayrollType.Regular,
                PayPeriodStart = earlier.Payload.FromDate, PayPeriodEnd = earlier.Payload.ToDate, PayrollDate = earlier.Payload.ToDate,
                GrossIncome = earlier.PayrollLine.GrossIncome, AbsencesAmount = earlier.PayrollLine.AbsencesAmount,
            },
        ];
        var creditedTo = earlier.PayrollLine.StatutoryCreditDate;
        if (result.SSS.EE > 0)
            later.Payload.SSSContribution[key] = [.. later.Payload.SSSContribution.GetValueOrDefault(key) ?? [],
                new SSSContributionModel { EmployeeId = id, EE = result.SSS.EE, ER = result.SSS.ER, EC = result.SSS.EC, PayrollDate = creditedTo }];
        if (result.PHIC.EE > 0)
            later.Payload.PHICContribution[key] = [.. later.Payload.PHICContribution.GetValueOrDefault(key) ?? [],
                new PHICContributionModel { EmployeeId = id, EmployeeShare = result.PHIC.EE, EmployerShare = result.PHIC.ER, PayrollDate = creditedTo }];
        if (result.HDMF.EE > 0)
            later.Payload.HDMFContribution[key] = [.. later.Payload.HDMFContribution.GetValueOrDefault(key) ?? [],
                new HDMFContributionModel { EmployeeId = id, EmployeeShare = result.HDMF.EE, EmployerShare = result.HDMF.ER, PayrollDate = creditedTo }];
    }
}
