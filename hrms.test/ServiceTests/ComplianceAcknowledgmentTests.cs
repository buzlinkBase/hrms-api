using Hrms.Core.Services.Compliance;
using Hrms.Domain;
using hrms.test.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace hrms.test.ServiceTests;

/// <summary>
/// Settings that differ from the Philippine statutory (DOLE) defaults: which ones the rules flag, and
/// that saving one is never refused but logged -- who set which value and when.
/// </summary>
public class ComplianceAcknowledgmentTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly ComplianceActor Admin = new(Guid.NewGuid(), "admin@acme.ph", "203.0.113.7");
    private readonly Clock _clock = new();

    private static IReadOnlyList<StatutoryDeviation> OvertimeAt(decimal rate) =>
        StatutoryDeviationRules.Evaluate(new RateSubject(new Dictionary<RateType, decimal> { [RateType.OVERTIME] = rate }));

    private sealed class FixedTerms(bool live) : ITermsLiveness
    {
        public Task<bool> IsLiveAsync(CancellationToken token = default) => Task.FromResult(live);
    }

    // Each read is a minute after the last, so "the last logged value" is never a tie.
    private sealed class Clock : TimeProvider
    {
        public static readonly DateTimeOffset Start = new(2026, 10, 31, 0, 0, 0, TimeSpan.Zero);
        private int _ticks;
        public override DateTimeOffset GetUtcNow() => Start.AddMinutes(_ticks++);
    }

    private async Task WithServiceAsync(
        SqliteHrmsContext db, Func<ComplianceAcknowledgmentService, Task> action, bool commit = true, bool live = true)
    {
        // Production-like: the tenant interceptor stamps TenantId on the new rows, as in the API.
        await using var context = db.NewProductionLikeContext(CompanyId);
        var uow = new UnitOfWorkService(context);
        var service = new ComplianceAcknowledgmentService(uow, new FixedTerms(live), _clock);
        await action(service);
        if (commit) await service.CommitChangesAsync();
    }

    private static async Task<List<Hrms.Domain.Entities.ComplianceAcknowledgment>> RowsAsync(SqliteHrmsContext db)
    {
        await using var read = db.NewContext(CompanyId);
        return await read.ComplianceAcknowledgments.IgnoreQueryFilters().AsNoTracking().ToListAsync();
    }

    // ── Rules ────────────────────────────────────────────────────────────────

    [Fact]
    public void Additive_IsADeviation_CompoundedIsNot()
    {
        StatutoryDeviationRules.Evaluate(new CompanyPolicySubject(OtNdCalculationMethod.Additive))
            .Should().ContainSingle().Which.Key.Should().Be("policy.ot-nd-method");
        StatutoryDeviationRules.Evaluate(new CompanyPolicySubject(OtNdCalculationMethod.Compounded)).Should().BeEmpty();
    }

    [Theory]
    [InlineData(1.10, true)]
    [InlineData(1.25, false)]
    [InlineData(1.50, false)]
    public void Overtime_BelowTheDoleFloor_IsADeviation(decimal rate, bool flagged)
    {
        var deviations = OvertimeAt(rate);

        if (flagged)
        {
            var deviation = deviations.Should().ContainSingle().Subject;
            deviation.Key.Should().Be("rate.OVERTIME");
            deviation.StatutoryValue.Should().Be("1.25");
            deviation.ConfiguredValue.Should().Be("1.10");
        }
        else
        {
            deviations.Should().BeEmpty();
        }
    }

    [Fact]
    public void EveryRateType_HasAStatutoryFloor()
    {
        Enum.GetValues<RateType>().Should().OnlyContain(t => RateBelowStatutoryFloorRule.Floors.ContainsKey(t),
            "a new rate type needs its DOLE minimum, or it is never checked");
    }

    [Fact]
    public void TheDefaultRates_AreNotDeviations()
    {
        var defaults = new Dictionary<RateType, decimal>
        {
            [RateType.REGULAR] = 1.0m, [RateType.NIGHTDIFF] = 1.1m, [RateType.OVERTIME] = 1.25m,
            [RateType.RESTDAY_DUTY] = 1.3m, [RateType.LEGAL_HOLIDAY] = 1.0m, [RateType.LEGAL_HOLIDAY_DUTY] = 2.0m,
            [RateType.SPECIAL_WORKING] = 1.0m, [RateType.SPECIAL_NON_WORKING] = 1.3m,
            [RateType.RESTDAY_SPECIAL] = 1.5m, [RateType.HOLIDAY_OT] = 1.3m,
        };

        StatutoryDeviationRules.Evaluate(new RateSubject(defaults)).Should().BeEmpty();
    }

    [Fact]
    public void AStatutoryCap_IsADeviation_NoCapIsNot()
    {
        StatutoryDeviationRules.Evaluate(new StatutoryCapSubject(500m, null, 0m))
            .Should().ContainSingle().Which.Key.Should().Be("cap.sss");
        StatutoryDeviationRules.Evaluate(new StatutoryCapSubject(null, null, null)).Should().BeEmpty();
    }

    // ── Logging ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ANewDeviation_IsSavedWithoutAsking_AndLogsWhoWhenAndWhat()
    {
        using var db = new SqliteHrmsContext();

        await WithServiceAsync(db, s =>
            s.RecordDeviationsAsync(ComplianceAcknowledgmentService.CompanyScope, OvertimeAt(1.1m), Admin));

        var row = (await RowsAsync(db)).Should().ContainSingle().Subject;
        row.TenantId.Should().Be(CompanyId);
        row.Scope.Should().Be("company");
        row.SettingKey.Should().Be("rate.OVERTIME");
        row.StatutoryValue.Should().Be("1.25");
        row.ConfiguredValue.Should().Be("1.10");
        row.UserEmail.Should().Be("admin@acme.ph");
        row.IpAddress.Should().Be("203.0.113.7");
        row.AcknowledgedAtUtc.Should().Be(Clock.Start.UtcDateTime);
    }

    [Fact]
    public async Task ReSavingTheSameValue_LogsNothing_ADifferentValueIsLogged()
    {
        using var db = new SqliteHrmsContext();
        await WithServiceAsync(db, s =>
            s.RecordDeviationsAsync(ComplianceAcknowledgmentService.CompanyScope, OvertimeAt(1.1m), Admin));

        await WithServiceAsync(db, s =>
            s.RecordDeviationsAsync(ComplianceAcknowledgmentService.CompanyScope, OvertimeAt(1.1m), Admin));
        await WithServiceAsync(db, s =>
            s.RecordDeviationsAsync(ComplianceAcknowledgmentService.CompanyScope, OvertimeAt(1.0m), Admin));

        (await RowsAsync(db)).Select(r => r.ConfiguredValue).Should().BeEquivalentTo(["1.10", "1.00"]);
    }

    [Fact]
    public async Task ChangingBackToAnEarlierValue_IsLoggedAgain()
    {
        using var db = new SqliteHrmsContext();
        foreach (var rate in new[] { 1.1m, 1.0m, 1.1m })
            await WithServiceAsync(db, s =>
                s.RecordDeviationsAsync(ComplianceAcknowledgmentService.CompanyScope, OvertimeAt(rate), Admin));

        (await RowsAsync(db)).OrderBy(r => r.AcknowledgedAtUtc).Select(r => r.ConfiguredValue)
            .Should().Equal("1.10", "1.00", "1.10");
    }

    [Fact]
    public async Task AClientsLog_IsSeparateFromTheCompanys()
    {
        using var db = new SqliteHrmsContext();
        await WithServiceAsync(db, s =>
            s.RecordDeviationsAsync(ComplianceAcknowledgmentService.ClientScope(Guid.NewGuid()), OvertimeAt(1.1m), Admin));

        await WithServiceAsync(db, s =>
            s.RecordDeviationsAsync(ComplianceAcknowledgmentService.CompanyScope, OvertimeAt(1.1m), Admin));

        (await RowsAsync(db)).Select(r => r.Scope).Should().Contain("company").And.HaveCount(2);
    }

    [Fact]
    public async Task List_IsNewestFirst()
    {
        using var db = new SqliteHrmsContext();
        await WithServiceAsync(db, s =>
            s.RecordDeviationsAsync(ComplianceAcknowledgmentService.CompanyScope,
                StatutoryDeviationRules.Evaluate(new CompanyPolicySubject(OtNdCalculationMethod.Additive)), Admin));
        await WithServiceAsync(db, s =>
            s.RecordDeviationsAsync(ComplianceAcknowledgmentService.CompanyScope, OvertimeAt(1.1m), Admin));

        ComplianceAcknowledgmentPage? page = null;
        await WithServiceAsync(db, async s => page = await s.ListAsync(1, 20), commit: false);

        page!.Total.Should().Be(2);
        page.Items.Select(i => i.SettingKey).Should().Equal("rate.OVERTIME", "policy.ot-nd-method");
    }

    // ── The Terms' live switch ───────────────────────────────────────────────

    [Fact]
    public async Task WhileTheTermsAreNotLive_NothingIsLogged()
    {
        using var db = new SqliteHrmsContext();

        await WithServiceAsync(db, s =>
            s.RecordDeviationsAsync(ComplianceAcknowledgmentService.CompanyScope, OvertimeAt(1.1m), Admin), live: false);

        (await RowsAsync(db)).Should().BeEmpty();
    }
}
