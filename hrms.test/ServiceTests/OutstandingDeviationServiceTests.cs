using Hrms.Core.Services.Compliance;
using Hrms.Domain;
using Hrms.Domain.Entities;
using hrms.test.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace hrms.test.ServiceTests;

/// <summary>
/// Company Settings > Acknowledged Deviations' "Not confirmed" list: settings in effect that differ
/// from the DOLE defaults with no acknowledgment on record (e.g. saved before acknowledgments were
/// asked for, or while the Terms weren't live), and confirming them in one go.
/// </summary>
public class OutstandingDeviationServiceTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly ComplianceActor Admin = new(Guid.NewGuid(), "admin@acme.ph", null);

    private static readonly StatutoryDeviation Additive =
        StatutoryDeviationRules.Evaluate(new CompanyPolicySubject(OtNdCalculationMethod.Additive)).Single();

    private sealed class FixedTerms(TermsLivenessState state) : ITermsLiveness
    {
        public Task<bool> IsLiveAsync(CancellationToken token = default) => Task.FromResult(state == TermsLivenessState.Live);
        public Task<TermsLivenessState> GetStateAsync(CancellationToken token = default) => Task.FromResult(state);
    }

    private sealed class FixedSource(params DeviationFinding[] findings) : IDeviationSource
    {
        public Task<IEnumerable<DeviationFinding>> FindAsync(CancellationToken token) => Task.FromResult<IEnumerable<DeviationFinding>>(findings);
    }

    private static async Task<T> WithServiceAsync<T>(SqliteHrmsContext db, IDeviationSource source,
        Func<OutstandingDeviationService, Task<T>> action, TermsLivenessState state = TermsLivenessState.Live)
    {
        await using var context = db.NewProductionLikeContext(CompanyId);
        var uow = new UnitOfWorkService(context);
        var terms = new FixedTerms(state);
        var service = new OutstandingDeviationService(uow, new ComplianceAcknowledgmentService(uow, terms), terms, [source]);
        var result = await action(service);
        await service.CommitChangesAsync();
        return result;
    }

    [Fact]
    public async Task UnacknowledgedDeviation_IsListed_WithWhereItIsSet()
    {
        using var db = new SqliteHrmsContext();
        var source = new FixedSource(new DeviationFinding(ComplianceAcknowledgmentService.CompanyScope, Additive, "Attendance & Payroll Policy"));

        var result = await WithServiceAsync(db, source, s => s.ListAsync());

        result.TermsState.Should().Be(nameof(TermsLivenessState.Live));
        var item = result.Items.Should().ContainSingle().Subject;
        item.SettingKey.Should().Be("policy.ot-nd-method");
        item.ConfiguredValue.Should().Be("Additive");
        item.Sources.Should().Equal("Attendance & Payroll Policy");
    }

    [Fact]
    public async Task SameSettingInSeveralPlaces_IsOneRow_ListingEachPlace()
    {
        using var db = new SqliteHrmsContext();
        var shiftDeviation = new StatutoryDeviation("shift.nd-threshold", "Night differential threshold", "None", "30", "");
        var source = new FixedSource(
            new DeviationFinding(ComplianceAcknowledgmentService.CompanyScope, shiftDeviation, "Shift: Night"),
            new DeviationFinding(ComplianceAcknowledgmentService.CompanyScope, shiftDeviation, "Shift: Graveyard"));

        var result = await WithServiceAsync(db, source, s => s.ListAsync());

        var item = result.Items.Should().ContainSingle().Subject;
        item.Sources.Should().Equal("Shift: Night", "Shift: Graveyard");
        item.SourceCount.Should().Be(2);
    }

    [Fact]
    public async Task ConfirmAll_RecordsThem_AndTheyLeaveTheList()
    {
        using var db = new SqliteHrmsContext();
        var source = new FixedSource(new DeviationFinding(ComplianceAcknowledgmentService.CompanyScope, Additive, "Attendance & Payroll Policy"));

        var confirmed = await WithServiceAsync(db, source, s => s.ConfirmAllAsync(Admin));
        var after = await WithServiceAsync(db, source, s => s.ListAsync());

        confirmed.Should().Be(1);
        after.Items.Should().BeEmpty();
        await using var read = db.NewContext(CompanyId);
        var ack = (await read.ComplianceAcknowledgments.IgnoreQueryFilters().AsNoTracking().ToListAsync())
            .Should().ContainSingle().Subject;
        ack.SettingKey.Should().Be("policy.ot-nd-method");
        ack.UserEmail.Should().Be("admin@acme.ph");
    }

    [Theory]
    [InlineData(TermsLivenessState.NotLive)]
    [InlineData(TermsLivenessState.Unreachable)]
    public async Task WhileTheTermsArentLive_ItSaysWhy_AndConfirmRecordsNothing(TermsLivenessState state)
    {
        using var db = new SqliteHrmsContext();
        var source = new FixedSource(new DeviationFinding(ComplianceAcknowledgmentService.CompanyScope, Additive, "Attendance & Payroll Policy"));

        var result = await WithServiceAsync(db, source, s => s.ListAsync(), state);
        var confirmed = await WithServiceAsync(db, source, s => s.ConfirmAllAsync(Admin), state);

        result.TermsState.Should().Be(state.ToString());
        result.Items.Should().ContainSingle();
        confirmed.Should().Be(0);
        await using var read = db.NewContext(CompanyId);
        (await read.ComplianceAcknowledgments.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task EveryRegisteredSource_ReadsTheRealSchema_AndFindsASavedAdditiveMethod()
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewProductionLikeContext(CompanyId))
        {
            seed.GeneralSettings.Add(new GeneralSetting
            {
                IdentityType = "Company",
                Description = SettingKey.OtNdCalculationMethod.ToString(),
                Value = OtNdCalculationMethod.Additive.ToString(),
            });
            await seed.SaveChangesAsync();
        }
        await using var context = db.NewProductionLikeContext(CompanyId);
        var uow = new UnitOfWorkService(context);
        var settings = new DTR.Core.GeneralSettingService(uow);
        var compliance = new ComplianceAcknowledgmentService(uow, new FixedTerms(TermsLivenessState.Live));
        IDeviationSource[] sources =
        [
            new Hrms.Api.Providers.CompanyPolicyDeviationSource(settings, new DTR.Core.CompanyPolicyService()),
            new CompanyRateDeviationSource(uow),
            new ClientDeviationSource(uow, settings),
            new TimeShiftDeviationSource(uow),
            new LeaveDeviationSource(uow),
            new EmployeePayDeviationSource(uow, new EmployeePayDeviationService(uow, compliance)),
        ];

        var findings = new List<DeviationFinding>();
        foreach (var source in sources)
            findings.AddRange(await source.FindAsync(CancellationToken.None));

        findings.Should().Contain(f => f.Deviation.Key == "policy.ot-nd-method" && f.Source == "Attendance & Payroll Policy");
    }

    [Fact]
    public async Task CompanyRates_BelowTheDoleFloor_AreFound()
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewProductionLikeContext(CompanyId))
        {
            seed.PremiumRates.AddRange(
                new RateTable { Type = RateType.OVERTIME, Rate = 1.10m },
                new RateTable { Type = RateType.NIGHTDIFF, Rate = 1.10m });
            await seed.SaveChangesAsync();
        }
        await using var context = db.NewProductionLikeContext(CompanyId);

        var findings = (await new CompanyRateDeviationSource(new UnitOfWorkService(context)).FindAsync(CancellationToken.None)).ToList();

        var finding = findings.Should().ContainSingle().Subject;
        finding.Deviation.Key.Should().Be("rate.OVERTIME");
        finding.Source.Should().Be("Rate Multipliers");
    }
}
