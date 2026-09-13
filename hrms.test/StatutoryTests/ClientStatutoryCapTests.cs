using hrms.test.TestSupport;

namespace hrms.test.StatutoryTests;

/// <summary>
/// Setup > Client > Settings > Statutory Capping — a per-client maximum EE (employee) share
/// for SSS/PhilHealth/Pag-IBIG. Capping is applied entirely in GetTable, by downgrading to the
/// highest bracket whose EE/EmployeeShare still respects the cap — EE and ER always come from
/// that SAME real government-table row, so a capped employee reads as an ordinary lower-bracket
/// match (not a partial EE-only override with a mismatched ER). GetBalance's usual monthly
/// aggregation (netting against Context.Payload.SSSContribution/PHICContribution/
/// HDMFContribution across the month's cutoffs) then applies unchanged to whichever bracket
/// GetTable resolved. Null or a value &lt;= 0 (what GeneralSettingsUtil.ParsePositiveDecimalOrNull
/// always maps 0 to) means uncapped. See StatutoryCapHelper.GetClientCap.
/// </summary>
public class ClientStatutoryCapTests : TestContextBase
{
    // Three brackets so a cap can force a downgrade to a real, lower, self-consistent row —
    // same shape as SSSTableCalculatorTests' own bracket table.
    private static readonly SSSModel[] SSSBrackets =
    {
        SSSBracket(0, 19_999, 900, 1_890, 10),
        SSSBracket(20_000, 34_999, 1_350, 2_840, 10),
        SSSBracket(35_000, 999_999, 1_800, 3_790, 10),
    };
    private static readonly PHICModel[] PHICBrackets =
    {
        PHICBracket(0, 19_999, 400, 400),
        PHICBracket(20_000, 999_999, 900, 900),
    };
    private static readonly HDMFModel[] HDMFBrackets =
    {
        HDMFBracket(0, 19_999, 100, 100),
        HDMFBracket(20_000, 999_999, 200, 200),
    };

    [Fact]
    public void SSS_CapForcesDowngrade_UsesConsistentLowerBracket_BothEEAndER()
    {
        // Natural bracket for a 50,000 gross is the top one (EE 1,800 / ER 3,790). A 1,000 cap
        // disqualifies it and the 1,350 middle bracket too -- only the 900 bracket qualifies.
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 50_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetSSSRate(ctx, ComputationBasis.Table);
        SeedSSSTable(ctx, SSSBrackets);
        SetClientStatutoryCap(ctx, StatutoryCapType.SSS, 1_000);

        var result = new DeductionPipeline().Run(ctx);

        result.SSS.EE.Should().Be(900); // downgraded bracket's EE, not a raw 1,800->1,000 override
        result.SSS.ER.Should().Be(1_890); // ER comes from the SAME downgraded bracket, not 3,790
    }

    [Fact]
    public void SSS_CapAboveBracketAmount_DoesNotChangeAnything()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 50_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetSSSRate(ctx, ComputationBasis.Table);
        SeedSSSTable(ctx, SSSBrackets);
        SetClientStatutoryCap(ctx, StatutoryCapType.SSS, 5_000);

        var result = new DeductionPipeline().Run(ctx);
        result.SSS.EE.Should().Be(1_800); // natural bracket wins, cap has no effect
        result.SSS.ER.Should().Be(3_790);
    }

    [Fact]
    public void SSS_ZeroCap_MeansUncapped()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 50_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetSSSRate(ctx, ComputationBasis.Table);
        SeedSSSTable(ctx, SSSBrackets);
        SetClientStatutoryCap(ctx, StatutoryCapType.SSS, 0);

        new DeductionPipeline().Run(ctx).SSS.EE.Should().Be(1_800);
    }

    [Fact]
    public void SSS_CapBelowEvenLowestBracket_FallsBackToLowestBracket()
    {
        // A cap set below every bracket's EE can't produce a qualifying downgrade -- falls
        // back to the lowest bracket rather than zeroing the deduction out entirely.
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 50_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetSSSRate(ctx, ComputationBasis.Table);
        SeedSSSTable(ctx, SSSBrackets);
        SetClientStatutoryCap(ctx, StatutoryCapType.SSS, 500);

        var result = new DeductionPipeline().Run(ctx);
        result.SSS.EE.Should().Be(900);
        result.SSS.ER.Should().Be(1_890);
    }

    [Fact]
    public void SSS_CapAggregatesAcrossCutoffs_UsesDowngradedBracketConsistently()
    {
        // FIXED brackets off the projected MonthlyRate (30,000 default -> the 1,350/2,840
        // middle bracket) every cutoff, regardless of that cutoff's own gross. A 1,000 cap
        // disqualifies the middle bracket, so BOTH cutoffs independently downgrade to the same
        // 900/1,890 bracket -- the monthly total lands on the downgraded bracket's own EE/ER
        // (900/1,890), not the raw cap value (1,000) and not the original bracket (1,350/2,840).
        var empId = NewEmployeeId();
        var clientId = Guid.NewGuid();

        var cutoff1 = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 25_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15), employeeId: empId);
        cutoff1.Employee.ClientId = clientId;
        AddCutoff(cutoff1, 15, label: "1st Cutoff"); AddCutoff(cutoff1, 31, isEndOfMonth: true, label: "2nd Cutoff");
        SetSSSRate(cutoff1, ComputationBasis.Table);
        SeedSSSTable(cutoff1, SSSBrackets);
        cutoff1.Payload.ClientStatutoryCaps[new ClientStatutoryCapKey(clientId, StatutoryCapType.SSS)] = 1_000;
        var result1 = new DeductionPipeline().Run(cutoff1);
        result1.SSS.EE.Should().Be(450); // 900 (downgraded EE) / 2 cutoffs
        result1.SSS.ER.Should().Be(945); // 1,890 (downgraded ER) / 2 cutoffs

        var cutoff2 = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 25_000, new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31), employeeId: empId);
        cutoff2.Employee.ClientId = clientId;
        AddCutoff(cutoff2, 15, label: "1st Cutoff"); AddCutoff(cutoff2, 31, isEndOfMonth: true, label: "2nd Cutoff");
        SetSSSRate(cutoff2, ComputationBasis.Table);
        SeedSSSTable(cutoff2, SSSBrackets);
        cutoff2.Payload.ClientStatutoryCaps[new ClientStatutoryCapKey(clientId, StatutoryCapType.SSS)] = 1_000;
        AddPriorPayroll(cutoff2, 25_000);
        AddSSSContribution(cutoff2, 450, 945, 5);

        var result2 = new DeductionPipeline().Run(cutoff2);
        result2.SSS.EE.Should().Be(450); // 900 downgraded total - 450 already withheld
        result2.SSS.ER.Should().Be(945); // 1,890 downgraded total - 945 already withheld

        (result1.SSS.EE + result2.SSS.EE).Should().Be(900);
        (result1.SSS.ER + result2.SSS.ER).Should().Be(1_890);
    }

    [Fact]
    public void PHIC_CapForcesDowngrade_UsesConsistentLowerBracket_BothEEAndER()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 50_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetPHICRate(ctx, ComputationBasis.Table);
        SeedPHICTable(ctx, PHICBrackets);
        SetClientStatutoryCap(ctx, StatutoryCapType.PhilHealth, 500);

        var result = new DeductionPipeline().Run(ctx);

        result.PHIC.EE.Should().Be(400); // downgraded bracket, not 900 capped down to 500
        result.PHIC.ER.Should().Be(400);
    }

    [Fact]
    public void HDMF_CapForcesDowngrade_UsesConsistentLowerBracket_BothEEAndER()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 50_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetHDMFRate(ctx, ComputationBasis.Table);
        SeedHDMFTable(ctx, HDMFBrackets);
        SetClientStatutoryCap(ctx, StatutoryCapType.PagIbig, 150);

        var result = new DeductionPipeline().Run(ctx);

        result.HDMF.EE.Should().Be(100); // downgraded bracket, not 200 capped down to 150
        result.HDMF.ER.Should().Be(100);
    }

    [Fact]
    public void NoClientId_CapNeverApplies()
    {
        // Employee.ClientId left null (e.g. an internal, non-client-billed employee) — a cap
        // keyed by a client's Guid can never match, so behavior is identical to uncapped.
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 50_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetSSSRate(ctx, ComputationBasis.Table);
        SeedSSSTable(ctx, SSSBrackets);
        ctx.Payload.ClientStatutoryCaps[new ClientStatutoryCapKey(Guid.NewGuid(), StatutoryCapType.SSS)] = 100;

        new DeductionPipeline().Run(ctx).SSS.EE.Should().Be(1_800);
    }
}
