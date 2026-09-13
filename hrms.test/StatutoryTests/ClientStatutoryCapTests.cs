using hrms.test.TestSupport;

namespace hrms.test.StatutoryTests;

/// <summary>
/// Setup > Client > Settings > Statutory Capping — a per-client maximum monthly EE (employee)
/// deduction for SSS/PhilHealth/Pag-IBIG, aggregated across every cutoff in the month via the
/// same GetBalance mechanism that already nets a bracket's monthly EE against
/// Context.Payload.SSSContribution/PHICContribution/HDMFContribution. Null or a value &lt;= 0
/// (the default, and what GeneralSettingsUtil.ParsePositiveDecimalOrNull always maps 0 to)
/// means uncapped. See StatutoryCapHelper.ApplyClientCap.
/// </summary>
public class ClientStatutoryCapTests : TestContextBase
{
    private static readonly SSSModel[] SSSBrackets =
    {
        SSSBracket(0, 999_999, 1_800, 3_790, 10),
    };
    private static readonly PHICModel[] PHICBrackets =
    {
        PHICBracket(0, 999_999, 900, 900),
    };
    private static readonly HDMFModel[] HDMFBrackets =
    {
        HDMFBracket(0, 999_999, 200, 200),
    };

    [Fact]
    public void SSS_CapBelowBracketAmount_ReducesEEOnly_NotER()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 50_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetSSSRate(ctx, ComputationBasis.Table);
        SeedSSSTable(ctx, SSSBrackets);
        SetClientStatutoryCap(ctx, StatutoryCapType.SSS, 1_000);

        var result = new DeductionPipeline().Run(ctx);

        result.SSS.EE.Should().Be(1_000); // capped, not the bracket's 1,800
        result.SSS.ER.Should().Be(3_790); // employer share is never capped
    }

    [Fact]
    public void SSS_CapAboveBracketAmount_DoesNotChangeAnything()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 50_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetSSSRate(ctx, ComputationBasis.Table);
        SeedSSSTable(ctx, SSSBrackets);
        SetClientStatutoryCap(ctx, StatutoryCapType.SSS, 5_000);

        new DeductionPipeline().Run(ctx).SSS.EE.Should().Be(1_800); // bracket amount wins, cap has no effect
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
    public void SSS_CapAggregatesAcrossCutoffs_SecondCutoffOnlyGetsRemainingRoom()
    {
        // Non-final cutoffs still split off the UNCAPPED bracket/divisor (1,800/2 = 900) —
        // the cap only acts as a ceiling on that share (Math.Min(perInstance, balance) inside
        // StatutoryHelper.CalcRemainingBalance), same as an ordinary monthly balance would.
        // Here 900 is under the 1,000 cap, so cutoff 1 is unaffected; the cap only bites once
        // the running monthly total (900) gets close to it, on cutoff 2.
        var empId = NewEmployeeId();
        var clientId = Guid.NewGuid();

        var cutoff1 = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 25_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15), employeeId: empId);
        cutoff1.Employee.ClientId = clientId;
        AddCutoff(cutoff1, 15, label: "1st Cutoff"); AddCutoff(cutoff1, 31, isEndOfMonth: true, label: "2nd Cutoff");
        SetSSSRate(cutoff1, ComputationBasis.Table);
        SeedSSSTable(cutoff1, SSSBrackets);
        cutoff1.Payload.ClientStatutoryCaps[new ClientStatutoryCapKey(clientId, StatutoryCapType.SSS)] = 1_000;
        var result1 = new DeductionPipeline().Run(cutoff1);
        result1.SSS.EE.Should().Be(900); // 1,800 / 2 cutoffs, still under the 1,000 cap

        var cutoff2 = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 25_000, new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31), employeeId: empId);
        cutoff2.Employee.ClientId = clientId;
        AddCutoff(cutoff2, 15, label: "1st Cutoff"); AddCutoff(cutoff2, 31, isEndOfMonth: true, label: "2nd Cutoff");
        SetSSSRate(cutoff2, ComputationBasis.Table);
        SeedSSSTable(cutoff2, SSSBrackets);
        cutoff2.Payload.ClientStatutoryCaps[new ClientStatutoryCapKey(clientId, StatutoryCapType.SSS)] = 1_000;
        AddPriorPayroll(cutoff2, 25_000);
        AddSSSContribution(cutoff2, 900, 1_895, 5);

        var result2 = new DeductionPipeline().Run(cutoff2);
        result2.SSS.EE.Should().Be(100); // 1,000 cap - 900 already withheld this month, NOT 1,800 - 900

        (result1.SSS.EE + result2.SSS.EE).Should().Be(1_000); // the whole month never exceeds the cap
    }

    [Fact]
    public void PHIC_CapBelowBracketAmount_ReducesEEOnly()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 50_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetPHICRate(ctx, ComputationBasis.Table);
        SeedPHICTable(ctx, PHICBrackets);
        SetClientStatutoryCap(ctx, StatutoryCapType.PhilHealth, 400);

        var result = new DeductionPipeline().Run(ctx);

        result.PHIC.EE.Should().Be(400);
        result.PHIC.ER.Should().Be(900);
    }

    [Fact]
    public void HDMF_CapBelowBracketAmount_ReducesEEOnly()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 50_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetHDMFRate(ctx, ComputationBasis.Table);
        SeedHDMFTable(ctx, HDMFBrackets);
        SetClientStatutoryCap(ctx, StatutoryCapType.PagIbig, 100);

        var result = new DeductionPipeline().Run(ctx);

        result.HDMF.EE.Should().Be(100);
        result.HDMF.ER.Should().Be(200);
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
