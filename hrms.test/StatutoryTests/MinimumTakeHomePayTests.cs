using hrms.test.TestSupport;

namespace hrms.test.StatutoryTests;

/// <summary>
/// Setup > Company Policy > Minimum Take-Home Pay — no deduction (statutory or scheduled) may
/// push an employee's remaining gross balance below RequiredTakehomePercentage of their gross
/// income for the period. See DeductionValidator.CanApply.
/// </summary>
public class MinimumTakeHomePayTests : TestContextBase
{
    private static readonly SSSModel[] Brackets =
    {
        SSSBracket(0, 999_999, 4_500, 9_500, 30),
    };

    [Fact]
    public void Deduction_Allowed_WhenRemainingBalanceStaysAtOrAboveFloor()
    {
        // Gross 50,000, 10% floor (default) = 5,000. A 4,500 SSS deduction leaves 45,500,
        // comfortably above the floor.
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 50_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetSSSRate(ctx, ComputationBasis.Table);
        SeedSSSTable(ctx, Brackets);

        var result = new DeductionPipeline().Run(ctx);

        result.SSS.EE.Should().Be(4_500);
    }

    [Fact]
    public void Deduction_Blocked_WhenItWouldCutBelowFloor()
    {
        // Gross 4,700, 10% floor (default) = 470. A 4,500 SSS deduction would leave 200,
        // below the floor -- the deduction is refused (zeroed) instead of applied.
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 4_700, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetSSSRate(ctx, ComputationBasis.Table);
        SeedSSSTable(ctx, Brackets);

        var result = new DeductionPipeline().Run(ctx);

        result.SSS.EE.Should().Be(0);
        result.IsLimit.Should().BeTrue();
    }

    [Fact]
    public void BlockedStatutoryDeduction_HaltsThePipeline_SkippingLaterDeductionsToo()
    {
        // IsLimit is a pipeline-wide stop flag (every DeductionPolicy/calculator checks it on
        // entry) -- once SSS hits the floor, PHIC/HDMF/ScheduledDeductions must not run either,
        // not just SSS itself.
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 4_700, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetSSSRate(ctx, ComputationBasis.Table);
        SeedSSSTable(ctx, Brackets);
        SetPHICRate(ctx, ComputationBasis.Table);
        SeedPHICTable(ctx, PHICBracket(0, 999_999, 200, 200));

        var key = new EmployeeKey(ctx.Employee.Id);
        ctx.Payload.Deductions[key] = new List<DeductionInfo> { new() { Amount = 10, Remarks = "Scheduled" } };

        var result = new DeductionPipeline().Run(ctx);

        result.SSS.EE.Should().Be(0);
        result.PHIC.EE.Should().Be(0); // never reached -- SSS already raised IsLimit
        result.ScheduledDeductions.Should().BeEmpty();
        result.IsLimit.Should().BeTrue();
    }

    [Fact]
    public void Deduction_Allowed_WhenRemainingBalanceLandsExactlyOnFloor()
    {
        // Gross 45,000, 10% floor = 4,500. A 4,500 deduction leaves exactly 40,500 (>= floor
        // of 4,500) -- the floor is inclusive, not a strict cutoff.
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 45_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetSSSRate(ctx, ComputationBasis.Table);
        SeedSSSTable(ctx, Brackets);

        var result = new DeductionPipeline().Run(ctx);

        result.SSS.EE.Should().Be(4_500);
    }

    [Fact]
    public void ZeroPercentFloor_OnlyBlocksDeductionsThatWouldGoNegative()
    {
        // A 0% floor still means "don't cut below zero" -- a deduction larger than the entire
        // remaining balance is still refused rather than driving RemainingGrossBalance negative.
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 4_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetSSSRate(ctx, ComputationBasis.Table);
        SeedSSSTable(ctx, Brackets);
        SetRequiredTakehomePercentage(ctx, 0);

        var result = new DeductionPipeline().Run(ctx);

        // Bracket EE is 4,500 but gross is only 4,000 -- even a 0% floor can't let this apply.
        result.SSS.EE.Should().Be(0);
        result.IsLimit.Should().BeTrue();
    }

    [Fact]
    public void HigherPercentageFloor_BlocksADeductionThatWouldOtherwiseBeAllowed()
    {
        // Gross 50,000, EE 4,500 leaves 45,500 remaining. A 90% floor (=45,000) still allows
        // it; raising the floor further to 92% (=46,000) blocks the same deduction.
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 50_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetSSSRate(ctx, ComputationBasis.Table);
        SeedSSSTable(ctx, Brackets);
        SetRequiredTakehomePercentage(ctx, 92);

        var result = new DeductionPipeline().Run(ctx);

        result.SSS.EE.Should().Be(0);
        result.IsLimit.Should().BeTrue();
    }

    [Fact]
    public void ScheduledDeduction_StopsAndFlagsIsLimit_WhenFloorWouldBeBreached()
    {
        // No statutory deductions in play -- gross 10,000, 10% floor = 1,000. First scheduled
        // deduction (loan, 6,000) leaves 4,000, still fine. Second (5,000) would leave -1,000,
        // well below the floor, so it's refused and IsLimit is raised; the first still applied.
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 10_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);

        var key = new EmployeeKey(ctx.Employee.Id);
        ctx.Payload.Deductions[key] = new List<DeductionInfo>
        {
            new() { Amount = 6_000, Remarks = "First" },
            new() { Amount = 5_000, Remarks = "Second" },
        };

        var result = new DeductionPipeline().Run(ctx);

        result.ScheduledDeductions.Should().ContainSingle(x => x.Remarks == "First");
        result.RunningTotal.Should().Be(6_000);
        result.IsLimit.Should().BeTrue();
    }
}
