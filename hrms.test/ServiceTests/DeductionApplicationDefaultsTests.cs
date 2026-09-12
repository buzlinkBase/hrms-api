using Hrms.Domain.Entities;

namespace hrms.test.ServiceTests;

/// <summary>
/// DeductionApplication.ProcessBy is a NOT NULL database column, but neither
/// CreateDeductionApplication nor UpdateDeductionApplication (nor the admin/portal Loan and
/// Deduction Application forms) carry a value for it -- there's no "who processed this" input
/// anywhere in the UI. Without a constructor default, Mapster's Map&lt;DeductionApplication&gt;()
/// leaves ProcessBy at its CLR default (null), and saving throws a NOT NULL constraint
/// violation on submit. See OtherIncomeApplication for the identical, already-working pattern
/// this mirrors.
/// </summary>
public class DeductionApplicationDefaultsTests
{
    [Fact]
    public void ProcessBy_DefaultsToEmptyString_NotNull()
    {
        var application = new DeductionApplication();

        application.ProcessBy.Should().Be("");
    }
}
