using Hrms.Core.Policies.DTRPolicies;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// Setup > Client > Settings > Rate Multipliers — a client-negotiated OT PREMIUM for one
/// holiday/rest-day OT category (Legal Holiday OT, Special Holiday OT, etc.), compounding with
/// that same category's day-type rate exactly the way the shared HOLIDAY_OT rate does, just
/// per-category instead of shared. When a client leaves a category's own premium blank, it falls
/// back to HOLIDAY_OT's own full resolution (client override, else company-wide, else
/// RATE_DEFAULT.HOLIDAY_OT) — see CompoundedOtRateStrategy (Hrms.Core/Policies/DTRPolicies/
/// OvertimeRateStrategies.cs).
///
/// These strategies only read context.Employee.ClientId and context.Payload.PremiumRates/
/// ClientPremiumRates, so a minimal PayrollContext (no DailyRecord/eligibility wiring) is enough
/// to exercise them directly.
/// </summary>
public class OvertimeRateStrategiesTests
{
    private static PayrollContext CreateContext(Guid? clientId = null)
    {
        return new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun { ClientId = clientId },
            Payload = new CalculatorPayload(),
        };
    }

    private static void SetCompanyRate(PayrollContext context, RateType type, decimal rate)
        => context.Payload.PremiumRates[type] = rate;

    private static void SetClientRate(PayrollContext context, Guid clientId, RateType type, decimal rate)
        => context.Payload.ClientPremiumRates[new ClientRateKey(clientId, type)] = rate;

    // --- CompoundedOtRateStrategy, otType = HOLIDAY_OT directly (no fallback needed) --------

    [Fact]
    public void Compounded_LegalHoliday_UsesCompanyDayTypeAndHolidayOtRates()
    {
        var context = CreateContext();
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);
        var strategy = new CompoundedOtRateStrategy(RateType.HOLIDAY_OT, null, RateType.LEGAL_HOLIDAY_DUTY);

        var (dayRate, fullRate) = strategy.Resolve(context);

        dayRate.Should().Be(2.00m);
        fullRate.Should().Be(2.60m); // matches Radisson Blu's spreadsheet figure exactly
    }

    [Fact]
    public void Compounded_SpecialHoliday_UsesCompanyDayTypeAndHolidayOtRates()
    {
        var context = CreateContext();
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);
        var strategy = new CompoundedOtRateStrategy(RateType.HOLIDAY_OT, null, RateType.SPECIAL_NON_WORKING);

        var (dayRate, fullRate) = strategy.Resolve(context);

        dayRate.Should().Be(1.30m);
        fullRate.Should().Be(1.69m); // matches Radisson Blu's spreadsheet figure exactly
    }

    [Fact]
    public void Compounded_DoubleLegal_SquaresTheDayTypeRate()
    {
        var context = CreateContext();
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);
        var strategy = new CompoundedOtRateStrategy(RateType.HOLIDAY_OT, null, RateType.LEGAL_HOLIDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY);

        var (dayRate, fullRate) = strategy.Resolve(context);

        dayRate.Should().Be(4.00m); // 2.00 * 2.00
        fullRate.Should().Be(5.20m); // 4.00 * 1.30
    }

    [Fact]
    public void Compounded_ClientRateOverridesCompanyRate_ForTheDayTypeComponent()
    {
        var clientId = Guid.NewGuid();
        var context = CreateContext(clientId);
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);
        SetClientRate(context, clientId, RateType.LEGAL_HOLIDAY_DUTY, 1.50m); // e.g. a different day-type rate for this client
        var strategy = new CompoundedOtRateStrategy(RateType.HOLIDAY_OT, null, RateType.LEGAL_HOLIDAY_DUTY);

        var (dayRate, fullRate) = strategy.Resolve(context);

        dayRate.Should().Be(1.50m);
        fullRate.Should().Be(1.95m); // 1.50 * 1.30
    }

    [Fact]
    public void Compounded_NothingConfigured_FallsBackToRateDefaultConstants()
    {
        // Regression guard: this used to fall back to a blanket 1.0m for EVERY day-type
        // component regardless of which RateType it actually was, and 1.25m for the OT
        // component even when it was HOLIDAY_OT (RATE_DEFAULT.HOLIDAY_OT = 1.30m, not 1.25m) --
        // silently underpaying an unconfigured Legal Holiday as if it were a Regular day. Each
        // fallback now resolves through RATE_DEFAULT.For(type) instead of a hardcoded literal.
        var context = CreateContext();
        var strategy = new CompoundedOtRateStrategy(RateType.HOLIDAY_OT, null, RateType.LEGAL_HOLIDAY_DUTY);

        var (dayRate, fullRate) = strategy.Resolve(context);

        dayRate.Should().Be(2.00m); // RATE_DEFAULT.LEGAL_HOLIDAY_DUTY
        fullRate.Should().Be(2.60m); // 2.00 x RATE_DEFAULT.HOLIDAY_OT (1.30)
    }

    [Fact]
    public void Compounded_NothingConfigured_PlainOvertimeFallsBackToOvertimeDefault_NotHolidayOt()
    {
        var context = CreateContext();
        var strategy = new CompoundedOtRateStrategy(RateType.OVERTIME, null);

        var (dayRate, fullRate) = strategy.Resolve(context);

        dayRate.Should().Be(1.0m); // no day-type component in this category at all
        fullRate.Should().Be(1.25m); // RATE_DEFAULT.OVERTIME, not HOLIDAY_OT's 1.30
    }

    // --- CompoundedOtRateStrategy, otType = a per-category premium, falling back to HOLIDAY_OT -

    [Fact]
    public void Fallback_PremiumNotConfigured_UsesHolidayOtsFullResolution()
    {
        var context = CreateContext();
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);
        var strategy = new CompoundedOtRateStrategy(RateType.LEGAL_HOLIDAY_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.LEGAL_HOLIDAY_DUTY);

        var (dayRate, fullRate) = strategy.Resolve(context);

        dayRate.Should().Be(2.00m);
        fullRate.Should().Be(2.60m); // no LEGAL_HOLIDAY_OT_PREMIUM override -- falls back to the shared HOLIDAY_OT (1.30)
    }

    [Fact]
    public void Fallback_ClientConfiguredThePremium_ReplacesOnlyTheOtTier_DayRateStaysFromDayType()
    {
        var clientId = Guid.NewGuid();
        var context = CreateContext(clientId);
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);
        SetClientRate(context, clientId, RateType.LEGAL_HOLIDAY_OT_PREMIUM, 1.50m);
        var strategy = new CompoundedOtRateStrategy(RateType.LEGAL_HOLIDAY_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.LEGAL_HOLIDAY_DUTY);

        var (dayRate, fullRate) = strategy.Resolve(context);

        dayRate.Should().Be(2.00m); // regular Legal Holiday pay is never touched by this override
        fullRate.Should().Be(3.00m); // 2.00 x 1.50, this client's own OT premium
    }

    [Fact]
    public void Fallback_UnconfiguredPremium_UsesTheClientsOwnHolidayOtOverride_NotTheCompanyDefault()
    {
        // Proves the fallback goes through HOLIDAY_OT's own FULL client-or-company resolution,
        // not a flat RATE_DEFAULT constant -- a client who overrode HOLIDAY_OT itself but left
        // this one category's premium blank should still see their own HOLIDAY_OT, not the
        // company default.
        var clientId = Guid.NewGuid();
        var context = CreateContext(clientId);
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m); // company default
        SetClientRate(context, clientId, RateType.HOLIDAY_OT, 1.40m); // this client's own HOLIDAY_OT override
        // No LEGAL_HOLIDAY_OT_PREMIUM override configured.
        var strategy = new CompoundedOtRateStrategy(RateType.LEGAL_HOLIDAY_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.LEGAL_HOLIDAY_DUTY);

        var (_, fullRate) = strategy.Resolve(context);

        fullRate.Should().Be(2.80m); // 2.00 x 1.40 -- the client's own HOLIDAY_OT, not the company's 1.30
    }

    [Fact]
    public void Fallback_OverrideOnlyAffectsItsOwnCategory_OtherCategoriesUnaffected()
    {
        // Setting LEGAL_HOLIDAY_OT_PREMIUM for a client must not leak into Special Holiday's own
        // premium (a different RateType/strategy instance) for that same client.
        var clientId = Guid.NewGuid();
        var context = CreateContext(clientId);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);
        SetClientRate(context, clientId, RateType.LEGAL_HOLIDAY_OT_PREMIUM, 1.50m);

        var special = new CompoundedOtRateStrategy(RateType.SPECIAL_HOLIDAY_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.SPECIAL_NON_WORKING);

        special.Resolve(context).FullRate.Should().Be(1.69m); // untouched -- falls back to HOLIDAY_OT, no SPECIAL_HOLIDAY_OT_PREMIUM override
    }

    // --- ResolveRawOtRate (segregated "OT Base" figure) -- same fallback chain, OT tier alone -

    [Fact]
    public void ResolveRawOtRate_PremiumNotConfigured_FallsBackToHolidayOt()
    {
        var context = CreateContext();
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);
        var strategy = new CompoundedOtRateStrategy(RateType.LEGAL_HOLIDAY_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.LEGAL_HOLIDAY_DUTY);

        strategy.ResolveRawOtRate(context).Should().Be(1.30m);
    }

    [Fact]
    public void ResolveRawOtRate_ClientConfigured_ReturnsThePremiumDirectly()
    {
        // Unlike the old flat-total override, the premium IS the OT-tier-alone figure already --
        // no day-rate decomposition needed.
        var clientId = Guid.NewGuid();
        var context = CreateContext(clientId);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);
        SetClientRate(context, clientId, RateType.LEGAL_HOLIDAY_OT_PREMIUM, 1.50m);
        var strategy = new CompoundedOtRateStrategy(RateType.LEGAL_HOLIDAY_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.LEGAL_HOLIDAY_DUTY);

        strategy.ResolveRawOtRate(context).Should().Be(1.50m);
    }
}
