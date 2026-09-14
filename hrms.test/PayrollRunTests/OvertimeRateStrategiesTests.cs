using Hrms.Core.Policies.DTRPolicies;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// Setup > Client > Settings > Rate Multipliers — a client-negotiated flat OT rate for one
/// holiday/rest-day OT category (Legal Holiday OT, Special Holiday OT, etc.), decoupled from the
/// standard day-type x HOLIDAY_OT compounding used everywhere else. See
/// ClientOverrideOtRateStrategy and CompoundedOtRateStrategy (Hrms.Core/Policies/DTRPolicies/
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

    // --- CompoundedOtRateStrategy (the standard, untouched formula) ------------------------

    [Fact]
    public void Compounded_LegalHoliday_UsesCompanyDayTypeAndHolidayOtRates()
    {
        var context = CreateContext();
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);
        var strategy = new CompoundedOtRateStrategy(RateType.LEGAL_HOLIDAY_DUTY, RateType.HOLIDAY_OT);

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
        var strategy = new CompoundedOtRateStrategy(RateType.SPECIAL_NON_WORKING, RateType.HOLIDAY_OT);

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
        var strategy = new CompoundedOtRateStrategy(RateType.LEGAL_HOLIDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY, RateType.HOLIDAY_OT);

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
        var strategy = new CompoundedOtRateStrategy(RateType.LEGAL_HOLIDAY_DUTY, RateType.HOLIDAY_OT);

        var (dayRate, fullRate) = strategy.Resolve(context);

        dayRate.Should().Be(1.50m);
        fullRate.Should().Be(1.95m); // 1.50 * 1.30
    }

    [Fact]
    public void Compounded_NothingConfigured_FallsBackToHardcodedDefaults()
    {
        var context = CreateContext();
        var strategy = new CompoundedOtRateStrategy(RateType.LEGAL_HOLIDAY_DUTY, RateType.HOLIDAY_OT);

        var (dayRate, fullRate) = strategy.Resolve(context);

        dayRate.Should().Be(1.0m); // GetRate's fallback for a day-type component
        fullRate.Should().Be(1.25m); // GetRate's fallback for the OT component
    }

    // --- ClientOverrideOtRateStrategy (the new decorator) -----------------------------------

    [Fact]
    public void Override_NotConfigured_IsANoOp_ReturnsExactlyWhatStandardStrategyComputed()
    {
        var context = CreateContext();
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);
        var standard = new CompoundedOtRateStrategy(RateType.LEGAL_HOLIDAY_DUTY, RateType.HOLIDAY_OT);
        var decorated = new ClientOverrideOtRateStrategy(RateType.LEGAL_HOLIDAY_OT, standard);

        var (dayRate, fullRate) = decorated.Resolve(context);

        dayRate.Should().Be(2.00m);
        fullRate.Should().Be(2.60m); // identical to the undecorated standard strategy -- Radisson Blu's case
    }

    [Fact]
    public void Override_ClientConfigured_ReplacesOnlyFullRate_DayRateStaysFromStandardStrategy()
    {
        // The "1.25 flat" client group from the spreadsheet: a flat 1.25x Legal Holiday OT
        // total, while their regular (non-OT) Legal Holiday day rate stays the standard 2.00x.
        var clientId = Guid.NewGuid();
        var context = CreateContext(clientId);
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);
        SetClientRate(context, clientId, RateType.LEGAL_HOLIDAY_OT, 1.25m);
        var standard = new CompoundedOtRateStrategy(RateType.LEGAL_HOLIDAY_DUTY, RateType.HOLIDAY_OT);
        var decorated = new ClientOverrideOtRateStrategy(RateType.LEGAL_HOLIDAY_OT, standard);

        var (dayRate, fullRate) = decorated.Resolve(context);

        dayRate.Should().Be(2.00m); // regular Legal Holiday pay is never touched by this override
        fullRate.Should().Be(1.25m); // the OT hours are paid at the client's flat negotiated rate
    }

    [Fact]
    public void Override_MetroRetailGroup_LegalAndSpecialBothResolveToTheirSpreadsheetTotals()
    {
        var clientId = Guid.NewGuid();
        var context = CreateContext(clientId);
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);
        SetClientRate(context, clientId, RateType.LEGAL_HOLIDAY_OT, 2.25m); // 1.25 + 1 flat, per spreadsheet
        SetClientRate(context, clientId, RateType.SPECIAL_HOLIDAY_OT, 1.625m); // 1.25 x 1.30, per spreadsheet

        var legal = new ClientOverrideOtRateStrategy(RateType.LEGAL_HOLIDAY_OT,
            new CompoundedOtRateStrategy(RateType.LEGAL_HOLIDAY_DUTY, RateType.HOLIDAY_OT));
        var special = new ClientOverrideOtRateStrategy(RateType.SPECIAL_HOLIDAY_OT,
            new CompoundedOtRateStrategy(RateType.SPECIAL_NON_WORKING, RateType.HOLIDAY_OT));

        legal.Resolve(context).FullRate.Should().Be(2.25m);
        special.Resolve(context).FullRate.Should().Be(1.625m);
    }

    [Fact]
    public void Override_OnlyAffectsItsOwnCategory_OtherCategoriesUnaffected()
    {
        // Setting LEGAL_HOLIDAY_OT for a client must not leak into Special Holiday OT (a
        // different RateType/strategy instance) for that same client.
        var clientId = Guid.NewGuid();
        var context = CreateContext(clientId);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);
        SetClientRate(context, clientId, RateType.LEGAL_HOLIDAY_OT, 1.25m);

        var special = new ClientOverrideOtRateStrategy(RateType.SPECIAL_HOLIDAY_OT,
            new CompoundedOtRateStrategy(RateType.SPECIAL_NON_WORKING, RateType.HOLIDAY_OT));

        special.Resolve(context).FullRate.Should().Be(1.69m); // untouched -- no SPECIAL_HOLIDAY_OT override was set
    }
}
