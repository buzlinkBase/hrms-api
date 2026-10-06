using hrms.test.TestSupport;

namespace hrms.test.StatutoryTests.SSS;

public class SSSFixedPerPayrollBasisTests : TestContextBase
{
    [Fact]
    public void FixedPerPayrollBasis_UsesFlatConfiguredRate_IgnoringGross()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 999_999, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        SetSSSRate(ctx, ComputationBasis.FixedPerPayroll, ee: 900, er: 1_890, ec: 10);

        var result = new DeductionPipeline().Run(ctx);
        result.SSS.EE.Should().Be(900);
        result.SSS.ER.Should().Be(1_890);
        result.SSS.EC.Should().Be(10);
        result.SSS.TotalER.Should().Be(1_900); // ER + EC combined
    }

    [Fact]
    public void FixedPerPayrollBasis_IsIncludedInRunningTotal_SoTotalDeductionsAndNetPayAreCorrect()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 13_540.50m, new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31));
        SetSSSRate(ctx, ComputationBasis.FixedPerPayroll, ee: 500, er: 1_000, ec: 10);
        SetPHICRate(ctx, ComputationBasis.FixedPerPayroll, ee: 181.35m, er: 181.35m);
        SetHDMFRate(ctx, ComputationBasis.FixedPerPayroll, ee: 100, er: 100);

        var result = new DeductionPipeline().Run(ctx);

        result.RunningTotal.Should().Be(781.35m);
        result.RemainingGrossBalance.Should().Be(13_540.50m - 781.35m);
    }

    [Fact]
    public void FixedPerPayrollBasis_IsBlocked_WhenItWouldCutBelowMinimumTakeHomeFloor()
    {
        // Gross 1,000, 10% floor (default) = 100. A 950 SSS deduction would leave 50.
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 1_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        SetSSSRate(ctx, ComputationBasis.FixedPerPayroll, ee: 950, er: 1_890, ec: 10);

        var result = new DeductionPipeline().Run(ctx);

        result.SSS.EE.Should().Be(0);
        result.RunningTotal.Should().Be(0);
        result.IsLimit.Should().BeTrue();
    }
}
