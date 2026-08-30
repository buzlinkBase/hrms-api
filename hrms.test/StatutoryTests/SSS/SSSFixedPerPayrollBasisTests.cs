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
        result.SSS.ER.Should().Be(1_900); // ER + EC combined for the Fixed path
    }
}
