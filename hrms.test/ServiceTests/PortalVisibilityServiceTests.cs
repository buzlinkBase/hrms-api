using DTR.Core;
using Hrms.Core.Services;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using Hrms.Infrastructure;
using hrms.test.TestSupport;

namespace hrms.test.ServiceTests;

/// <summary>
/// Company Settings > Payroll Settings switches for the My Portal pages: all on until the company
/// saves a switch off, and each switch only affects its own page.
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

    private static readonly (string Key, PortalPage Page)[] Switches =
    [
        (PayrollSettingsIdentity.KeyPortalShowThirteenthMonth, PortalPage.ThirteenthMonth),
        (PayrollSettingsIdentity.KeyPortalShowCashBond, PortalPage.CashBond),
        (PayrollSettingsIdentity.KeyPortalShowUniformAllowance, PortalPage.UniformAllowance),
        (PayrollSettingsIdentity.KeyPortalShowAnnouncements, PortalPage.Announcements),
        (PayrollSettingsIdentity.KeyPortalShowCoworkers, PortalPage.Coworkers),
        (PayrollSettingsIdentity.KeyPortalShowDtr, PortalPage.Dtr),
        (PayrollSettingsIdentity.KeyPortalShowIncompletePunches, PortalPage.IncompletePunches),
        (PayrollSettingsIdentity.KeyPortalShowRawAttendance, PortalPage.RawAttendance),
        (PayrollSettingsIdentity.KeyPortalShowShiftSchedule, PortalPage.ShiftSchedule),
        (PayrollSettingsIdentity.KeyPortalShowPayslips, PortalPage.Payslips),
        (PayrollSettingsIdentity.KeyPortalShowLeaveCredits, PortalPage.LeaveCredits),
        (PayrollSettingsIdentity.KeyPortalShowDeductions, PortalPage.Deductions),
        (PayrollSettingsIdentity.KeyPortalShowLeaveApplications, PortalPage.LeaveApplications),
        (PayrollSettingsIdentity.KeyPortalShowOvertime, PortalPage.Overtime),
        (PayrollSettingsIdentity.KeyPortalShowOfficialBusiness, PortalPage.OfficialBusiness),
        (PayrollSettingsIdentity.KeyPortalShowPassSlip, PortalPage.PassSlip),
        (PayrollSettingsIdentity.KeyPortalShowLoanApplications, PortalPage.LoanApplications),
    ];

    public static TheoryData<string, PortalPage> SwitchData()
    {
        var data = new TheoryData<string, PortalPage>();
        foreach (var (key, page) in Switches) data.Add(key, page);
        return data;
    }

    [Fact]
    public void EverySwitchIsCoveredByATest()
    {
        // Punch is opt-in (off until switched on), so it has its own tests below.
        Switches.Select(s => s.Page).Should().BeEquivalentTo(Enum.GetValues<PortalPage>().Where(p => p != PortalPage.Punch));
    }

    [Fact]
    public async Task PunchIsHiddenUntilTheCompanySwitchesItOn()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();

        var result = await Service(context).GetAsync();

        result.ShowPunch.Should().BeFalse();
        result.IsVisible(PortalPage.Punch).Should().BeFalse();
    }

    [Fact]
    public async Task PunchIsVisible_WhenTheCompanySwitchesItOn_AndNothingElseChanges()
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
            await Save(seed, PayrollSettingsIdentity.KeyPortalShowPunch, "True");

        await using var context = db.NewContext();
        var result = await Service(context).GetAsync();

        result.IsVisible(PortalPage.Punch).Should().BeTrue();
        result.Should().Be(new PortalVisibilityModel { ShowPunch = true });
    }

    [Fact]
    public async Task AnUnreadablePunchValueFallsBackToHidden()
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
            await Save(seed, PayrollSettingsIdentity.KeyPortalShowPunch, "maybe");

        await using var context = db.NewContext();
        var result = await Service(context).GetAsync();

        result.ShowPunch.Should().BeFalse();
    }

    [Fact]
    public async Task EveryPageIsVisibleUntilTheCompanySwitchesItOff()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();

        var result = await Service(context).GetAsync();

        result.Should().Be(new PortalVisibilityModel());
        Enum.GetValues<PortalPage>().Where(p => p != PortalPage.Punch).Should().OnlyContain(p => result.IsVisible(p));
    }

    [Theory]
    [MemberData(nameof(SwitchData))]
    public async Task SwitchingOnePageOffLeavesTheOthersVisible(string key, PortalPage page)
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
            await Save(seed, key, "False");

        await using var context = db.NewContext();
        var result = await Service(context).GetAsync();

        result.IsVisible(page).Should().BeFalse();
        Enum.GetValues<PortalPage>().Where(p => p != page && p != PortalPage.Punch).Should().OnlyContain(p => result.IsVisible(p));
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
