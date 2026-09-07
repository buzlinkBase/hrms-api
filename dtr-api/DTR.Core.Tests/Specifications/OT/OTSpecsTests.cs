using DTR.Core.Tests.TestSupport;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace DTR.Core.Tests.Specifications.OT;

/// <summary>
/// IsAppliedOTSpec / IsSystemAutoComputeOT — the two gates AppliedOvertimePolicy /
/// AutoComputeOvertimePolicy are wrapped in.
/// </summary>
public class OTSpecsTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 1, 8, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 1, 16, 0, 0);

    [Fact]
    public void IsAppliedOTSpec_OTApplicationExistsForShiftDate_ReturnsTrue()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        ApplyOvertime(context, new OverTimeApplication { EmployeeId = context.Payload.Data.Employee.Id, OTDate = context.Payload.Data.CurrentShift.ShiftDate, Employee = new Employee() });

        new IsAppliedOTSpec().IsSatisfiedBy(TimeRange.Empty, context).Should().BeTrue();
    }

    [Fact]
    public void IsAppliedOTSpec_NoOTApplication_ReturnsFalse()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);

        new IsAppliedOTSpec().IsSatisfiedBy(TimeRange.Empty, context).Should().BeFalse();
    }

    [Fact]
    public void IsSystemAutoComputeOT_ShiftWithOTTrue_ReturnsTrue()
    {
        var context = CreateContext(ShiftStart, ShiftEnd); // WithOT defaults true
        new IsSystemAutoComputeOT().IsSatisfiedBy(TimeRange.Empty, context).Should().BeTrue();
    }

    [Fact]
    public void IsSystemAutoComputeOT_ShiftWithOTFalse_ReturnsFalse()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        context.Payload.Data.CurrentShift.WithOT = false;

        new IsSystemAutoComputeOT().IsSatisfiedBy(TimeRange.Empty, context).Should().BeFalse();
    }
}
