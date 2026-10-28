using hrms.test.TestSupport;

namespace hrms.test.StatutoryTests;

/// <summary>
/// Every Statutory Deduction Release Schedule (50/50 split, First cutoff, Last cutoff) on a
/// semi-monthly group whose periods stay within the month (15 / end of month) and on groups whose
/// periods cross into the next month (10 / 25 and 1 / 15), for a Fixed employee with No DTR, a
/// Fixed employee with DTR absences and a Variable employee. Each case runs the month's two
/// payrolls in order -- the first one's payroll and ledger rows fed into the second, credited
/// where its line was -- and checks what each withholds for SSS, PhilHealth and Pag-IBIG, and
/// that together they come to exactly the month's table amount.
/// </summary>
public class StatutoryScheduleMatrixTests : TestContextBase
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

    /// <summary>A payroll group: its cutoffs, and the month's two periods.</summary>
    public sealed record Group(string Name, (int Day, bool Eom)[] Cutoffs, DateOnly FirstFrom, DateOnly FirstTo, DateOnly SecondFrom, DateOnly SecondTo)
    {
        public override string ToString() => Name;
    }

    private static readonly Group SameMonth = new("15 / end of month",
        [(15, false), (31, true)],
        new(2025, 3, 1), new(2025, 3, 15), new(2025, 3, 16), new(2025, 3, 31));

    private static readonly Group TenTwentyFive = new("10 / 25 (cross month)",
        [(10, false), (25, false)],
        new(2025, 2, 26), new(2025, 3, 10), new(2025, 3, 11), new(2025, 3, 25));

    private static readonly Group OneFifteen = new("1 / 15 (cross month)",
        [(1, false), (15, false)],
        new(2025, 2, 16), new(2025, 3, 1), new(2025, 3, 2), new(2025, 3, 15));

    /// <summary>An employee: salary type, monthly rate, each payroll's gross and absences.</summary>
    public sealed record Pay(string Name, SalaryType SalaryType, decimal MonthlyRate, decimal FirstGross, decimal FirstAbsences, decimal SecondGross)
    {
        public override string ToString() => Name;
    }

    // Fixed, No DTR needed: always half the monthly rate, nothing deducted for attendance.
    private static readonly Pay FixedNoDtr = new("Fixed, No DTR", SalaryType.FIXED, 30_000, 15_000, 0, 15_000);

    // Fixed with DTR: 2,000 of absences on the first payroll; the month's gross is 28,000 (still bracket B).
    private static readonly Pay FixedWithDtr = new("Fixed, with DTR absences", SalaryType.FIXED, 30_000, 13_000, 2_000, 15_000);

    // Variable: 15,000 earned by the first payroll (bracket A on its own), 30,000 by month end (bracket B).
    private static readonly Pay Variable = new("Variable", SalaryType.VARIABLE, 30_000, 15_000, 0, 15_000);

    /// <summary>Group, employee, schedule, and the SSS EE each payroll should withhold (month total 1,350).</summary>
    public static TheoryData<Group, Pay, StatutoryDeductionSchedule, decimal, decimal> Cases()
    {
        var data = new TheoryData<Group, Pay, StatutoryDeductionSchedule, decimal, decimal>();
        foreach (var group in new[] { SameMonth, TenTwentyFive, OneFifteen })
        {
            // Fixed: the first payroll brackets off the whole month projected from the monthly rate (less
            // known absences), so the 50/50 split is a real half.
            foreach (var pay in new[] { FixedNoDtr, FixedWithDtr })
            {
                data.Add(group, pay, StatutoryDeductionSchedule.PerPayroll, 675, 675);
                data.Add(group, pay, StatutoryDeductionSchedule.FirstHalfMonth, 1_350, 0);
                data.Add(group, pay, StatutoryDeductionSchedule.SecondHalfMonth, 0, 1_350);
            }
            // Variable: the first payroll brackets off what was actually earned so far (15,000 ->
            // 900), withheld whole; the last payroll trues the month up to bracket B.
            data.Add(group, Variable, StatutoryDeductionSchedule.PerPayroll, 900, 450);
            data.Add(group, Variable, StatutoryDeductionSchedule.FirstHalfMonth, 900, 450);
            data.Add(group, Variable, StatutoryDeductionSchedule.SecondHalfMonth, 0, 1_350);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EachScheduleWithholdsTheMonthOnce(
        Group group, Pay pay, StatutoryDeductionSchedule schedule, decimal expectedFirst, decimal expectedSecond)
    {
        var employee = Guid.NewGuid();
        var first = Build(group, pay, schedule, employee, group.FirstFrom, group.FirstTo, pay.FirstGross, pay.FirstAbsences);
        var result1 = new DeductionPipeline().Run(first);

        var second = Build(group, pay, schedule, employee, group.SecondFrom, group.SecondTo, pay.SecondGross, 0);
        FeedEarlierRun(second, first, result1);
        var result2 = new DeductionPipeline().Run(second);

        result1.SSS.EE.Should().Be(expectedFirst, "SSS on the first payroll");
        result2.SSS.EE.Should().Be(expectedSecond, "SSS on the second payroll");
        result1.PHIC.EE.Should().Be(expectedFirst / 2, "PhilHealth on the first payroll");
        result2.PHIC.EE.Should().Be(expectedSecond / 2, "PhilHealth on the second payroll");
        result1.HDMF.EE.Should().Be(expectedFirst / 2, "Pag-IBIG on the first payroll");
        result2.HDMF.EE.Should().Be(expectedSecond / 2, "Pag-IBIG on the second payroll");

        (result1.SSS.EE + result2.SSS.EE).Should().Be(1_350, "the month is withheld exactly once");
        (result1.SSS.ER + result2.SSS.ER).Should().Be(2_840, "and so is the employer's share");
    }

    [Theory]
    [MemberData(nameof(GroupsOnly))]
    public void BothPayrollsOfTheMonth_AreCreditedToTheSameMonth(Group group)
    {
        var first = Build(group, FixedNoDtr, StatutoryDeductionSchedule.PerPayroll, Guid.NewGuid(), group.FirstFrom, group.FirstTo, 15_000, 0);
        var second = Build(group, FixedNoDtr, StatutoryDeductionSchedule.PerPayroll, Guid.NewGuid(), group.SecondFrom, group.SecondTo, 15_000, 0);

        StatutoryMonthResolver.SameMonth(first.PayrollLine.StatutoryCreditDate, second.PayrollLine.StatutoryCreditDate).Should().BeTrue();
        first.PayrollLine.StatutoryCreditDate.Month.Should().Be(3, "March's two payrolls");
    }

    public static TheoryData<Group> GroupsOnly() => new() { SameMonth, TenTwentyFive, OneFifteen };

    [Theory]
    [MemberData(nameof(GroupsOnly))]
    public void TheFirstPayrollIsTheFirstCutoff_AndTheSecondIsTheLast(Group group)
    {
        var resolver = new CutoffPolicyResolver();
        var first = Build(group, FixedNoDtr, StatutoryDeductionSchedule.PerPayroll, Guid.NewGuid(), group.FirstFrom, group.FirstTo, 15_000, 0);
        var second = Build(group, FixedNoDtr, StatutoryDeductionSchedule.PerPayroll, Guid.NewGuid(), group.SecondFrom, group.SecondTo, 15_000, 0);

        (resolver.IsFirstCutoff(first), resolver.IsLastCutoff(first)).Should().Be((true, false));
        (resolver.IsFirstCutoff(second), resolver.IsSecondCutoff(second), resolver.IsLastCutoff(second)).Should().Be((false, true, true));
    }

    // ── Setup ────────────────────────────────────────────────────────────────

    private static DeductionPayloadContext Build(
        Group group, Pay pay, StatutoryDeductionSchedule schedule, Guid employeeId, DateOnly from, DateOnly to, decimal gross, decimal absences)
    {
        var ctx = CreateContext(pay.SalaryType, PayrollFrequency.SEMI_MONTHLY, gross, from, to,
            monthlyRate: pay.MonthlyRate, schedule: schedule, employeeId: employeeId);
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

    private static void FeedEarlierRun(DeductionPayloadContext later, DeductionPayloadContext earlier, DeductionPipeData result)
    {
        var key = new EmployeeKey(later.Employee.Id);
        var id = later.Employee.Id;
        later.Payload.PostedPriorPayrolls[key] =
        [
            new Payroll
            {
                EmployeeId = id, PayrollType = PayrollType.Regular,
                PayPeriodStart = earlier.Payload.FromDate, PayPeriodEnd = earlier.Payload.ToDate, PayrollDate = earlier.Payload.ToDate,
                GrossIncome = earlier.PayrollLine.GrossIncome, AbsencesAmount = earlier.PayrollLine.AbsencesAmount,
            },
        ];
        var creditedTo = earlier.PayrollLine.StatutoryCreditDate;
        if (result.SSS.EE > 0)
            later.Payload.SSSContribution[key] =
                [new SSSContributionModel { EmployeeId = id, EE = result.SSS.EE, ER = result.SSS.ER, EC = result.SSS.EC, PayrollDate = creditedTo }];
        if (result.PHIC.EE > 0)
            later.Payload.PHICContribution[key] =
                [new PHICContributionModel { EmployeeId = id, EmployeeShare = result.PHIC.EE, EmployerShare = result.PHIC.ER, PayrollDate = creditedTo }];
        if (result.HDMF.EE > 0)
            later.Payload.HDMFContribution[key] =
                [new HDMFContributionModel { EmployeeId = id, EmployeeShare = result.HDMF.EE, EmployerShare = result.HDMF.ER, PayrollDate = creditedTo }];
    }
}
