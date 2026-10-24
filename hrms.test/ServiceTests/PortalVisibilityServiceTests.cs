using DTR.Core;
using Hrms.Core.Services;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using Hrms.Infrastructure;
using hrms.test.TestSupport;

namespace hrms.test.ServiceTests;

/// <summary>
/// Company Settings > Payroll Settings switches for the optional My Portal pay pages: all on until
/// the company saves a switch off, and each switch only affects its own page.
/// </summary>
public class PortalVisibilityServiceTests
{
    private static PortalVisibilityService Service(HrmsContext context) =>
        new(new GeneralSettingService(new UnitOfWorkService(context)));

    private static async Task Save(HrmsContext seed, string key, string value)
    {
        seed.Add(new GeneralSetting
        {
            IdentityType = PayrollSettingsIdentity.IdentityType,
            Description = key,
            Value = value,
        });
        await seed.SaveChangesAsync();
    }

    [Fact]
    public async Task EveryPageIsVisibleUntilTheCompanySwitchesItOff()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();

        var result = await Service(context).GetAsync();

        result.Should().Be(new PortalVisibilityModel(true, true, true));
    }

    [Theory]
    [InlineData(PayrollSettingsIdentity.KeyPortalShowThirteenthMonth, false, true, true)]
    [InlineData(PayrollSettingsIdentity.KeyPortalShowCashBond, true, false, true)]
    [InlineData(PayrollSettingsIdentity.KeyPortalShowUniformAllowance, true, true, false)]
    public async Task SwitchingOnePageOffLeavesTheOthersVisible(
        string key, bool thirteenth, bool cashBond, bool uniform)
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
            await Save(seed, key, "False");

        await using var context = db.NewContext();
        var result = await Service(context).GetAsync();

        result.Should().Be(new PortalVisibilityModel(thirteenth, cashBond, uniform));
    }

    [Fact]
    public async Task AnUnreadableValueFallsBackToVisible()
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
            await Save(seed, PayrollSettingsIdentity.KeyPortalShowCashBond, "maybe");

        await using var context = db.NewContext();
        var result = await Service(context).GetAsync();

        result.ShowCashBond.Should().BeTrue();
    }
}
