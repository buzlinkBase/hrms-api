using DTR.Core;

namespace hrms.test.DTRTests;

/// <summary>
/// VirtualTimeComposer.IsEligibleForVirtualAttendance — gates whether a full "worked a shift"
/// virtual attendance block gets injected for a leave day, which feeds RegularDayPay as if the
/// employee physically worked. OneTime-payout leave (e.g. a Shared-funded SSS maternity lump
/// sum) is paid entirely through PayrollProcessorService.ApplyOneTimeLeavePayoutsToGross
/// instead — injecting virtual attendance for those days would double-pay VARIABLE employees
/// on top of the lump sum. See the maternity-leave bug fix.
/// </summary>
public class VirtualAttendanceEligibilityTests
{
    private static LeaveApplication Leave(PaySource paySource, PayoutMode payoutMode, PayType payType = PayType.WithPay) => new()
    {
        Leave = new Leave { Description = "Test Leave", PaySource = paySource },
        PayoutMode = payoutMode,
        PayType = payType,
    };

    [Fact]
    public void SharedPaySource_OneTime_IsNotEligible()
    {
        // The exact repro case — a Shared-funded (SSS Maternity, per the PaySource enum's own
        // documented example) OneTime leave must not get virtual attendance.
        VirtualTimeComposer.IsEligibleForVirtualAttendance(Leave(PaySource.Shared, PayoutMode.OneTime))
            .Should().BeFalse();
    }

    [Fact]
    public void CompanyPaySource_OneTime_IsNotEligible()
    {
        VirtualTimeComposer.IsEligibleForVirtualAttendance(Leave(PaySource.Company, PayoutMode.OneTime))
            .Should().BeFalse();
    }

    [Fact]
    public void SharedPaySource_PerDay_IsEligible()
    {
        // Regression guard — ordinary PerDay leave must still get virtual attendance.
        VirtualTimeComposer.IsEligibleForVirtualAttendance(Leave(PaySource.Shared, PayoutMode.PerDay))
            .Should().BeTrue();
    }

    [Fact]
    public void CompanyPaySource_PerDay_IsEligible()
    {
        VirtualTimeComposer.IsEligibleForVirtualAttendance(Leave(PaySource.Company, PayoutMode.PerDay))
            .Should().BeTrue();
    }

    [Fact]
    public void GovernmentPaySource_IsNeverEligible_RegardlessOfPayoutMode()
    {
        // Already-correct pre-existing behavior — regression guard.
        VirtualTimeComposer.IsEligibleForVirtualAttendance(Leave(PaySource.Government, PayoutMode.PerDay))
            .Should().BeFalse();
        VirtualTimeComposer.IsEligibleForVirtualAttendance(Leave(PaySource.Government, PayoutMode.OneTime))
            .Should().BeFalse();
    }

    [Fact]
    public void WithoutPay_IsNeverEligible_RegardlessOfPayoutMode()
    {
        VirtualTimeComposer.IsEligibleForVirtualAttendance(Leave(PaySource.Company, PayoutMode.PerDay, PayType.WithoutPay))
            .Should().BeFalse();
        VirtualTimeComposer.IsEligibleForVirtualAttendance(Leave(PaySource.Shared, PayoutMode.OneTime, PayType.WithoutPay))
            .Should().BeFalse();
    }
}
