using DTR.Core;
using Hrms.adms.Insfrastructure;
using Microsoft.EntityFrameworkCore;

namespace hrms.test.InfrastructureTests;

/// <summary>
/// Smaller guards from the memory-growth audit: one shared adms model across tenants, and
/// one-employee DTR lookups for holiday eligibility.
/// </summary>
public class MemoryGrowthRegressionTests
{
    [Fact]
    public void AdmsContext_EveryTenantSharesOneModel()
    {
        var options = new DbContextOptionsBuilder<AdmsContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var a = new AdmsContext(options, new FixedTenant(Guid.NewGuid()));
        using var b = new AdmsContext(options, new FixedTenant(Guid.NewGuid()));

        a.Model.Should().BeSameAs(b.Model);
        a.CurrentTenantId.Should().NotBe(b.CurrentTenantId);
    }

    [Fact]
    public void HolidayAdjacentDayLookup_IsScopedToOneEmployeeAndDay()
    {
        var employeeId = Guid.NewGuid();
        var date = new DateOnly(2026, 12, 24);

        var payload = DTRCalcService.GetEmployeeDayPayload(employeeId, date);

        payload.EmployeeId.Should().Be(employeeId);
        payload.FromDate.Should().Be(date);
        payload.ToDate.Should().Be(date);
        payload.BranchId.Should().BeNull();
        payload.ClientId.Should().BeNull();
        payload.PayrollGroupId.Should().BeNull();
        payload.DepartmentId.Should().BeNull();
        payload.OperationAreaId.Should().BeNull();
    }

    private sealed class FixedTenant(Guid tenantId) : ITenantProvider
    {
        public Guid TenantId { get; } = tenantId;
        public void SetTenantId(Guid newTenantId) { }
    }
}
