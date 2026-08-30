using hrms.test.TestSupport;

namespace hrms.test.StatutoryTests.PHIC;

public class PHICFixedPerPayrollBasisTests : TestContextBase
{
    [Fact]
    public void FixedPerPayrollBasis_UsesFlatConfiguredRate_IgnoringGross()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 999_999, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        SetPHICRate(ctx, ComputationBasis.FixedPerPayroll, ee: 450, er: 450);

        var result = new DeductionPipeline().Run(ctx);
        result.PHIC.EE.Should().Be(450);
        result.PHIC.ER.Should().Be(450);
    }
}
