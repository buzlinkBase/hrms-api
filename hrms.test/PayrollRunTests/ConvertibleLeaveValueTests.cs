using Hrms.Core.Services;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// LeaveLedgerService.ComputeConvertibleLeaveValue — one leave type's rate-weighted
/// convertible cash value: the balance capped at MaxCashConversionDays (uncapped when null),
/// then weighted by CashConversionRate. See GenerateLastPayAsync's leave conversion component.
/// </summary>
public class ConvertibleLeaveValueTests
{
    [Fact]
    public void NoCap_FullBalanceWeightedByRate()
    {
        LeaveLedgerService.ComputeConvertibleLeaveValue(balance: 15, maxCashConversionDays: null, cashConversionRate: 1.0m)
            .Should().Be(15);
    }

    [Fact]
    public void BalanceUnderCap_UsesFullBalance()
    {
        LeaveLedgerService.ComputeConvertibleLeaveValue(balance: 5, maxCashConversionDays: 10, cashConversionRate: 1.0m)
            .Should().Be(5);
    }

    [Fact]
    public void BalanceOverCap_ClampsToCap()
    {
        LeaveLedgerService.ComputeConvertibleLeaveValue(balance: 20, maxCashConversionDays: 10, cashConversionRate: 1.0m)
            .Should().Be(10);
    }

    [Fact]
    public void BalanceExactlyAtCap_UsesCap()
    {
        LeaveLedgerService.ComputeConvertibleLeaveValue(balance: 10, maxCashConversionDays: 10, cashConversionRate: 1.0m)
            .Should().Be(10);
    }

    [Fact]
    public void FractionalRate_WeightsCappedDays()
    {
        LeaveLedgerService.ComputeConvertibleLeaveValue(balance: 20, maxCashConversionDays: 10, cashConversionRate: 0.5m)
            .Should().Be(5);
    }

    [Fact]
    public void ZeroBalance_IsZero()
    {
        LeaveLedgerService.ComputeConvertibleLeaveValue(balance: 0, maxCashConversionDays: 10, cashConversionRate: 1.0m)
            .Should().Be(0);
    }
}
