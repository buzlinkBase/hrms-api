using hrms.test.TestSupport;

namespace hrms.test.StatutoryTests.HDMF;

public class HDMFFixedPerPayrollBasisTests : TestContextBase
{
    [Fact]
    public void FixedPerPayrollBasis_UsesFlatConfiguredRate_IgnoringGross()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 999_999, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        SetHDMFRate(ctx, ComputationBasis.FixedPerPayroll, ee: 450, er: 450);

        var result = new DeductionPipeline().Run(ctx);
        result.HDMF.EE.Should().Be(450);
        result.HDMF.ER.Should().Be(450);
    }
}
