using hrms.test.TestSupport;
using Hrms.Core.Services.Subscriptions;
using Hrms.Domain.Entities.EmployeeEntities;
using Onepunch.Common.Lib.Subscriptions;

namespace hrms.test.SubscriptionTests;

/// <summary>
/// What Tenant Service reads for a downgrade check: per company, employees not separated (soft-
/// deleted ones excluded) and feature usage -- counted across companies, past the per-company
/// query filter.
/// </summary>
public class SubscriptionUsageServiceTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();

    [Fact]
    public async Task CountsEachCompany_ActiveEmployeesOnly()
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
        {
            var groupA = new PayrollGroup { Id = Guid.NewGuid(), TenantId = CompanyA, Name = "A" };
            var groupB = new PayrollGroup { Id = Guid.NewGuid(), TenantId = CompanyB, Name = "B" };
            seed.PayrollGroups.AddRange(groupA, groupB);
            seed.Employees.AddRange(
                new Employee { Id = Guid.NewGuid(), TenantId = CompanyA, PayrollGroupId = groupA.Id, EmploymentStatus = EmploymentStatus.Regular },
                new Employee { Id = Guid.NewGuid(), TenantId = CompanyA, PayrollGroupId = groupA.Id, EmploymentStatus = EmploymentStatus.Probationary },
                new Employee { Id = Guid.NewGuid(), TenantId = CompanyA, PayrollGroupId = groupA.Id, EmploymentStatus = EmploymentStatus.Resigned },
                new Employee { Id = Guid.NewGuid(), TenantId = CompanyA, PayrollGroupId = groupA.Id, EmploymentStatus = EmploymentStatus.Regular, DeletedAt = DateTime.UtcNow },
                new Employee { Id = Guid.NewGuid(), TenantId = CompanyB, PayrollGroupId = groupB.Id, EmploymentStatus = EmploymentStatus.Contract });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var usage = await new SubscriptionUsageService(new UnitOfWorkService(context))
            .GetUsageAsync([CompanyA, CompanyB, Guid.NewGuid()], CancellationToken.None);

        usage.Should().HaveCount(3);
        usage.Single(u => u.TenantId == CompanyA).Used[EntitlementKeys.EmployeesMax].Should().Be(2);
        usage.Single(u => u.TenantId == CompanyB).Used[EntitlementKeys.EmployeesMax].Should().Be(1);
        usage.Single(u => u.TenantId != CompanyA && u.TenantId != CompanyB).Used[EntitlementKeys.EmployeesMax].Should().Be(0);
        usage.Single(u => u.TenantId == CompanyA).Used.Keys.Should().Contain(
            [EntitlementKeys.PayrollCore, EntitlementKeys.PayrollLoansIncome, EntitlementKeys.ApprovalsMultilevel, EntitlementKeys.Announcements, EntitlementKeys.ClientsAgency]);
    }

    [Fact]
    public async Task ActiveEmployeeCounter_AddsUpTheAccountsCompanies_AndNoOthers()
    {
        var outsider = Guid.NewGuid();
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
        {
            var groupA = new PayrollGroup { Id = Guid.NewGuid(), TenantId = CompanyA, Name = "A" };
            var groupB = new PayrollGroup { Id = Guid.NewGuid(), TenantId = CompanyB, Name = "B" };
            var groupO = new PayrollGroup { Id = Guid.NewGuid(), TenantId = outsider, Name = "O" };
            seed.PayrollGroups.AddRange(groupA, groupB, groupO);
            seed.Employees.AddRange(
                new Employee { Id = Guid.NewGuid(), TenantId = CompanyA, PayrollGroupId = groupA.Id, EmploymentStatus = EmploymentStatus.Regular },
                new Employee { Id = Guid.NewGuid(), TenantId = CompanyA, PayrollGroupId = groupA.Id, EmploymentStatus = EmploymentStatus.Resigned },
                new Employee { Id = Guid.NewGuid(), TenantId = CompanyA, PayrollGroupId = groupA.Id, EmploymentStatus = EmploymentStatus.Regular, DeletedAt = DateTime.UtcNow },
                new Employee { Id = Guid.NewGuid(), TenantId = CompanyB, PayrollGroupId = groupB.Id, EmploymentStatus = EmploymentStatus.Contract },
                new Employee { Id = Guid.NewGuid(), TenantId = outsider, PayrollGroupId = groupO.Id, EmploymentStatus = EmploymentStatus.Regular });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var counter = new ActiveEmployeeCounter(new UnitOfWorkService(context));

        (await counter.CountAsync([CompanyA, CompanyB], CancellationToken.None)).Should().Be(2);
        (await counter.CountAsync([CompanyA], CancellationToken.None)).Should().Be(1);
    }

    [Fact]
    public async Task NoCompanies_NothingToCount()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();

        (await new SubscriptionUsageService(new UnitOfWorkService(context)).GetUsageAsync([], CancellationToken.None))
            .Should().BeEmpty();
    }
}
