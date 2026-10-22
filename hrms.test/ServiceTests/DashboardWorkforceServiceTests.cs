using Hrms.Core.Services;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;

namespace hrms.test.ServiceTests;

/// <summary>
/// The Dashboard's workforce charts count current employees only, split by employment status,
/// branch and how long they've been employed.
/// </summary>
public class DashboardWorkforceServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 17);

    [Theory]
    [InlineData("2026-10-17", "Under 1 year")]
    [InlineData("2025-10-18", "Under 1 year")] // one day short of a year
    [InlineData("2025-10-17", "1 to 3 years")]
    [InlineData("2023-10-17", "3 to 5 years")]
    [InlineData("2021-10-17", "5 to 10 years")]
    [InlineData("2016-10-17", "10+ years")]
    [InlineData("2027-01-01", "Under 1 year")] // hire date in the future
    public void TenureCounts_PlacesAHireDateInItsBand(string hired, string band)
    {
        var counts = DashboardWorkforceService.TenureCounts([DateOnly.Parse(hired)], Today);

        counts.Single(c => c.Count == 1).Label.Should().Be(band);
        counts.Sum(c => c.Count).Should().Be(1);
    }

    [Fact]
    public async Task GetMix_CountsCurrentEmployeesOnly_BySplit()
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
        {
            var group = new PayrollGroup { Id = Guid.NewGuid() };
            var branch = new Branch { Id = Guid.NewGuid(), Name = "Manila" };
            var site = new CostCenters { Id = Guid.NewGuid(), Code = "HALL", Name = "City Hall" };
            seed.PayrollGroups.Add(group);
            seed.Branches.Add(branch);
            seed.Areas.Add(site);
            seed.Employees.AddRange(
                new Employee { Id = Guid.NewGuid(), PayrollGroupId = group.Id, BranchId = branch.Id, AreaId = site.Id, EmploymentStatus = EmploymentStatus.Regular, HireDate = new DateOnly(2020, 1, 1) },
                new Employee { Id = Guid.NewGuid(), PayrollGroupId = group.Id, BranchId = branch.Id, EmploymentStatus = EmploymentStatus.Regular, HireDate = new DateOnly(2026, 6, 1) },
                new Employee { Id = Guid.NewGuid(), PayrollGroupId = group.Id, EmploymentStatus = EmploymentStatus.Probationary, HireDate = new DateOnly(2026, 8, 1) },
                new Employee { Id = Guid.NewGuid(), PayrollGroupId = group.Id, BranchId = branch.Id, AreaId = site.Id, EmploymentStatus = EmploymentStatus.Regular, HireDate = new DateOnly(2015, 1, 1), DateResigned = new DateTime(2026, 5, 1) });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var mix = await new DashboardWorkforceService(new UnitOfWorkService(context)).GetMixAsync(Today, default);

        mix.EmploymentStatus.Should().BeEquivalentTo(
        [
            new DashboardCount { Label = "Regular", Count = 2 },
            new DashboardCount { Label = "Probationary", Count = 1 },
        ], o => o.WithStrictOrdering());
        mix.Branches.Should().BeEquivalentTo(
        [
            new DashboardCount { Label = "Manila", Count = 2 },
            new DashboardCount { Label = "No branch", Count = 1 },
        ], o => o.WithStrictOrdering());
        mix.Sites.Should().BeEquivalentTo(
        [
            new DashboardCount { Label = "No site", Count = 2 },
            new DashboardCount { Label = "City Hall", Count = 1 },
        ], o => o.WithStrictOrdering());
        mix.Tenure.Single(t => t.Label == "Under 1 year").Count.Should().Be(2);
        mix.Tenure.Single(t => t.Label == "5 to 10 years").Count.Should().Be(1);
        mix.Tenure.Sum(t => t.Count).Should().Be(3);
    }
}
