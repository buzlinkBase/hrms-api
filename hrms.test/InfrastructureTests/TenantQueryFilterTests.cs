using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace hrms.test.InfrastructureTests;

/// <summary>
/// HrmsContext's tenant + soft-delete filters read the tenant per query (TenantQueryFilters), so a
/// single EF model serves every tenant. Previously the tenant Guid was baked into the model as a
/// constant and TenantModelCacheKeyFactory cached a full model (plus its compiled-query cache) per
/// tenant for the life of the process -- memory grew with every tenant the server touched.
/// </summary>
public class TenantQueryFilterTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private static async Task<SqliteHrmsContext> SeedAsync()
    {
        var db = new SqliteHrmsContext();
        await using var seed = db.NewContext();
        seed.PayrollGroups.AddRange(
            new PayrollGroup { Id = Guid.NewGuid(), TenantId = TenantA, Name = "A1" },
            new PayrollGroup { Id = Guid.NewGuid(), TenantId = TenantA, Name = "A-deleted", DeletedAt = DateTime.UtcNow },
            new PayrollGroup { Id = Guid.NewGuid(), TenantId = TenantB, Name = "B1" });
        await seed.SaveChangesAsync();
        return db;
    }

    [Fact]
    public void EveryTenant_SharesOneModel()
    {
        using var db = new SqliteHrmsContext();
        using var a = db.NewContext(TenantA);
        using var b = db.NewContext(TenantB);
        using var none = db.NewContext();

        a.Model.Should().BeSameAs(b.Model);
        a.Model.Should().BeSameAs(none.Model);
    }

    [Fact]
    public async Task EachTenant_SeesOnlyItsOwnRows_OnTheSharedModel()
    {
        using var db = await SeedAsync();

        await using (var a = db.NewContext(TenantA))
            (await a.PayrollGroups.Select(x => x.Name).ToListAsync()).Should().BeEquivalentTo(["A1"]);

        await using (var b = db.NewContext(TenantB))
            (await b.PayrollGroups.Select(x => x.Name).ToListAsync()).Should().BeEquivalentTo(["B1"]);
    }

    [Fact]
    public async Task NoTenant_SeesEveryTenant_ButStillHidesSoftDeleted()
    {
        using var db = await SeedAsync();
        await using var context = db.NewContext();

        (await context.PayrollGroups.Select(x => x.Name).ToListAsync())
            .Should().BeEquivalentTo(["A1", "B1"], "background jobs run without a tenant, exactly as before");
    }

    [Fact]
    public async Task TenantSetAfterTheContextWasCreated_IsHonouredByLaterQueries()
    {
        using var db = await SeedAsync();
        var provider = new SwitchableTenantProvider();
        await using var context = db.NewContext(provider);

        (await context.PayrollGroups.CountAsync()).Should().Be(2);

        provider.SetTenantId(TenantB);
        (await context.PayrollGroups.Select(x => x.Name).ToListAsync())
            .Should().BeEquivalentTo(["B1"], "workers set the tenant on an already-resolved context");
    }

    [Fact]
    public async Task IgnoreQueryFilters_StillBypassesBoth()
    {
        using var db = await SeedAsync();
        await using var context = db.NewContext(TenantA);

        (await context.PayrollGroups.IgnoreQueryFilters().CountAsync()).Should().Be(3);
    }

    private sealed class SwitchableTenantProvider : ITenantProvider
    {
        public Guid TenantId { get; private set; }
        public void SetTenantId(Guid tenantId) => TenantId = tenantId;
    }
}
