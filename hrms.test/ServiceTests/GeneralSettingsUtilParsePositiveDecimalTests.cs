using DTR.Core;

namespace hrms.test.ServiceTests;

public class GeneralSettingsUtilParsePositiveDecimalTests
{
    [Fact]
    public void ParsesAPositiveValue()
    {
        GeneralSettingsUtil.ParsePositiveDecimalOrNull("1500.50").Should().Be(1500.50m);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-100")]
    [InlineData("not-a-number")]
    public void ReturnsNull_ForMissingZeroNegativeOrUnparseableInput(string? input)
    {
        GeneralSettingsUtil.ParsePositiveDecimalOrNull(input).Should().BeNull();
    }
}
