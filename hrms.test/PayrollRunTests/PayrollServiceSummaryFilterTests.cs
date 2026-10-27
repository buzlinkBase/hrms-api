using Hrms.Core.Services;
using Hrms.Domain;
using Hrms.Domain.Entities;
using hrms.test.TestSupport;
using MapsterMapper;
using NSubstitute;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// The Payroll Summary printout (PayrollsController.PrintSummary) used to query by the batch's
/// period dates only, so it ignored the screen's batch, Status, Client and Payroll Group filters
/// and could mix in other runs over the same period. GetAsync now takes the same filters, matching
/// Client/Payroll Group on the row's own snapshot.
/// </summary>
public class PayrollServiceSummaryFilterTests
{
    private static Payroll Row(Guid batchId, ApprovalStatus status, Guid? clientId, Guid? payrollGroupId, string name) => new()
    {
        Id = Guid.NewGuid(),
        EmployeeId = Guid.NewGuid(),
        FullName = name,
        PayrollBatchId = batchId,
        ApprovalStatus = status,
        ClientId = clientId,
        PayrollGroupId = payrollGroupId,
        PayPeriodStart = new DateOnly(2026, 9, 1),
        PayPeriodEnd = new DateOnly(2026, 9, 15),
        PayrollDate = new DateOnly(2026, 9, 15),
    };

    [Fact]
    public async Task GetAsync_AppliesBatchStatusClientAndPayrollGroupFilters()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        var otherBatchId = Guid.NewGuid();
        var clientA = Guid.NewGuid();
        var clientB = Guid.NewGuid();
        var groupA = Guid.NewGuid();
        var groupB = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Payrolls.AddRange(
                Row(batchId, ApprovalStatus.Approved, clientA, groupA, "Match"),
                Row(batchId, ApprovalStatus.Approved, clientB, groupA, "Other client"),
                Row(batchId, ApprovalStatus.Approved, clientA, groupB, "Other group"),
                // Same period, different run (e.g. a 13th Month run) -- must not bleed in.
                Row(otherBatchId, ApprovalStatus.Approved, clientA, groupA, "Other batch"),
                Row(otherBatchId, ApprovalStatus.ForApproval, clientA, groupA, "Other batch pending"));
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = new PayrollService(new UnitOfWorkService(context), Substitute.For<IMapper>());

        var filtered = await service.GetAsync(null, null, null, clientA, groupA, batchId, CancellationToken.None, ApprovalStatus.Approved);
        filtered.Select(x => x.FullName).Should().Equal("Match");

        var wholeBatch = await service.GetAsync(null, null, null, null, null, batchId, CancellationToken.None);
        wholeBatch.Should().HaveCount(3);

        var wrongStatus = await service.GetAsync(null, null, null, null, null, batchId, CancellationToken.None, ApprovalStatus.Declined);
        wrongStatus.Should().BeEmpty();

        var byDateAndStatus = await service.GetAsync(
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 15), null, null, null, null, CancellationToken.None, ApprovalStatus.ForApproval);
        byDateAndStatus.Select(x => x.FullName).Should().Equal("Other batch pending");
    }

    [Fact]
    public async Task GetAsync_EmployeeIds_KeepsOnlyThoseEmployees_AndEmptyMeansAll()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        var picked = Row(batchId, ApprovalStatus.Approved, null, null, "Picked");
        var alsoPicked = Row(batchId, ApprovalStatus.Approved, null, null, "Also picked");
        var notPicked = Row(batchId, ApprovalStatus.Approved, null, null, "Not picked");

        await using (var seed = db.NewContext())
        {
            seed.Payrolls.AddRange(picked, alsoPicked, notPicked);
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = new PayrollService(new UnitOfWorkService(context), Substitute.For<IMapper>());

        var filtered = await service.GetAsync(null, null, null, null, null, batchId, CancellationToken.None,
            employeeIds: [picked.EmployeeId, alsoPicked.EmployeeId]);
        filtered.Select(x => x.FullName).Should().BeEquivalentTo("Picked", "Also picked");

        var none = await service.GetAsync(null, null, null, null, null, batchId, CancellationToken.None,
            employeeIds: []);
        none.Should().HaveCount(3);
    }
}
