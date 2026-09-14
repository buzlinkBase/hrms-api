using Hrms.Core.Policies.DTRPolicies;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// End-to-end wiring tests for the 7 holiday/rest-day OT policies (Hrms.Core/Policies/
/// DTRPolicies/OvertimeCategoryPolicies.cs) — proves SingleCategoryOTPolicy's Template Method
/// correctly composes with each policy's IOtRateStrategy (CompoundedOtRateStrategy, optionally
/// wrapped by ClientOverrideOtRateStrategy), not just the strategies in isolation
/// (OvertimeRateStrategiesTests). Covers the three concrete scenarios from the client's
/// "Holiday Overtime Computation Per Detachment" spreadsheet.
/// </summary>
public class OvertimeCategoryPolicyTests
{
    private const decimal DailyRate = 800m;
    private const double ShiftHours = 8;
    private const decimal HourlyRate = 100m; // DailyRate / ShiftHours

    private static PayrollContext CreateContext(Guid? clientId, double legalHolOtHours = 0, double specialHolOtHours = 0)
    {
        return new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun
            {
                ClientId = clientId,
                DailyRate = DailyRate,
                Settings = new EmployeeSettingModel { IsEligibleForOvertime = true },
            },
            DailyRecord = new DailyRecordRunModel
            {
                ShiftWorkingHour = ShiftHours,
                LegalHolOTHours = legalHolOtHours,
                SpecialHolOTHours = specialHolOtHours,
            },
            Payload = new CalculatorPayload(),
        };
    }

    private static void SetCompanyRate(PayrollContext context, RateType type, decimal rate)
        => context.Payload.PremiumRates[type] = rate;

    private static void SetClientRate(PayrollContext context, Guid clientId, RateType type, decimal rate)
        => context.Payload.ClientPremiumRates[new ClientRateKey(clientId, type)] = rate;

    [Fact]
    public void RadissonBlu_NoOverrideConfigured_MatchesSpreadsheetViaStandardFormulaAlone()
    {
        var context = CreateContext(clientId: Guid.NewGuid(), legalHolOtHours: 2, specialHolOtHours: 2);
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);

        var legalLine = new LegalHolOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);
        var specialLine = new SpecialNonWorkingOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        legalLine.Value.Should().Be(HourlyRate * 2 * 2.60m);
        specialLine.Value.Should().Be(HourlyRate * 2 * 1.69m);
    }

    [Fact]
    public void StandardFlatGroup_ClientOverrideConfigured_BothCategoriesLandOnOnePointTwoFive()
    {
        var clientId = Guid.NewGuid();
        var context = CreateContext(clientId, legalHolOtHours: 3, specialHolOtHours: 3);
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);
        SetClientRate(context, clientId, RateType.LEGAL_HOLIDAY_OT, 1.25m);
        SetClientRate(context, clientId, RateType.SPECIAL_HOLIDAY_OT, 1.25m);

        var legalLine = new LegalHolOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);
        var specialLine = new SpecialNonWorkingOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        legalLine.Value.Should().Be(HourlyRate * 3 * 1.25m);
        specialLine.Value.Should().Be(HourlyRate * 3 * 1.25m);
        // OTPremium is honestly negative here -- this client's negotiated OT rate (1.25) is
        // below their own standard day rate (2.00/1.30) -- see the plan's reporting caveat.
        legalLine.OTPremium.Should().Be(HourlyRate * 3 * (1.25m - 2.00m));
    }

    [Fact]
    public void MetroRetailGroup_ClientOverrideConfigured_MatchesSpreadsheetsAdditiveFormulasExactly()
    {
        var clientId = Guid.NewGuid();
        var context = CreateContext(clientId, legalHolOtHours: 4, specialHolOtHours: 4);
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);
        SetClientRate(context, clientId, RateType.LEGAL_HOLIDAY_OT, 2.25m);
        SetClientRate(context, clientId, RateType.SPECIAL_HOLIDAY_OT, 1.625m);

        var legalLine = new LegalHolOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);
        var specialLine = new SpecialNonWorkingOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        // Spreadsheet: rate/8*1.25*hrs + rate/8*hrs == rate/8*hrs*2.25
        legalLine.Value.Should().Be(HourlyRate * 4 * 1.25m + HourlyRate * 4);
        // Spreadsheet: rate/8*1.25*hrs + (rate/8*1.25*hrs)*30% == rate/8*hrs*1.625
        specialLine.Value.Should().Be(HourlyRate * 4 * 1.25m + (HourlyRate * 4 * 1.25m) * 0.30m);
    }

    [Fact]
    public void OverrideForOneClient_DoesNotAffectAnotherClientWithNoOverride()
    {
        var overriddenClientId = Guid.NewGuid();
        var plainClientId = Guid.NewGuid();

        var overriddenContext = CreateContext(overriddenClientId, legalHolOtHours: 1);
        SetCompanyRate(overriddenContext, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(overriddenContext, RateType.HOLIDAY_OT, 1.30m);
        SetClientRate(overriddenContext, overriddenClientId, RateType.LEGAL_HOLIDAY_OT, 1.25m);

        var plainContext = CreateContext(plainClientId, legalHolOtHours: 1);
        plainContext.Payload = overriddenContext.Payload; // share the same company/client rate dictionaries
        SetCompanyRate(plainContext, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(plainContext, RateType.HOLIDAY_OT, 1.30m);

        var overriddenLine = new LegalHolOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), overriddenContext);
        var plainLine = new LegalHolOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), plainContext);

        overriddenLine.Value.Should().Be(HourlyRate * 1 * 1.25m);
        plainLine.Value.Should().Be(HourlyRate * 1 * 2.60m); // unaffected by the other client's override
    }

    [Fact]
    public void ZeroHours_ContributesNothing_RegardlessOfOverride()
    {
        var clientId = Guid.NewGuid();
        var context = CreateContext(clientId, legalHolOtHours: 0);
        SetClientRate(context, clientId, RateType.LEGAL_HOLIDAY_OT, 1.25m);

        var line = new LegalHolOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        line.Value.Should().Be(0m);
    }

    [Fact]
    public void NotEligibleForOvertime_ContributesNothing_RegardlessOfOverride()
    {
        var clientId = Guid.NewGuid();
        var context = CreateContext(clientId, legalHolOtHours: 4);
        context.Employee.Settings = new EmployeeSettingModel { IsEligibleForOvertime = false };
        SetClientRate(context, clientId, RateType.LEGAL_HOLIDAY_OT, 1.25m);

        var line = new LegalHolOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        line.Value.Should().Be(0m);
    }
}
