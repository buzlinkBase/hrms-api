using Hrms.Infrastructure;
using hrms.test.TestSupport;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Onepunch.Common.Lib.Exceptions;

namespace hrms.test.SetupTests;

/// <summary>
/// A localized special holiday can cover several project sites (HolidayAreas). Saving replaces
/// the set, legal holidays never keep sites, unknown sites are rejected, and the DTR read
/// (GetAllHolidays) carries the sites through.
/// </summary>
public class HolidayServiceAreaTests
{
    private static readonly DateOnly Day = new(2026, 9, 9);

    private static async Task<(SqliteHrmsContext Db, List<Guid> Sites)> SeedSitesAsync(int count)
    {
        var db = new SqliteHrmsContext();
        await using var seed = db.NewContext();
        var sites = Enumerable.Range(1, count)
            .Select(i => new CostCenters { Id = Guid.NewGuid(), Code = $"S{i}", Name = $"Site {i}" })
            .ToList();
        seed.Areas.AddRange(sites);
        await seed.SaveChangesAsync();
        return (db, sites.Select(x => x.Id).ToList());
    }

    private static HolidayService Service(HrmsContext context) =>
        new(new UnitOfWorkService(context), new Mapper(TestMapping.GlobalSettings));

    private static CreateHoliday Special(IEnumerable<Guid> areaIds) => new()
    {
        Description = "Osmeña Day",
        HolType = HolidayType.SPECIAL,
        WorkType = HolidayWorkType.NonWorking,
        HolDate = Day,
        AreaIds = areaIds.ToList(),
        Status = "Active",
    };

    private static async Task<List<Guid>> StoredSitesAsync(SqliteHrmsContext db, Guid holidayId)
    {
        await using var read = db.NewContext();
        return await read.HolidayAreas.Where(x => x.HolidayId == holidayId).Select(x => x.AreaId).ToListAsync();
    }

    [Fact]
    public async Task Add_SpecialWithSeveralSites_StoresEachSiteOnce()
    {
        var (db, sites) = await SeedSitesAsync(3);
        using var _ = db;

        HolidayModel created;
        await using (var context = db.NewProductionLikeContext())
        {
            created = await Service(context).AddAsync(Special([.. sites, sites[0]]), default);
        }

        created.AreaIds.Should().BeEquivalentTo(sites);
        (await StoredSitesAsync(db, created.Id)).Should().BeEquivalentTo(sites);
    }

    [Fact]
    public async Task Update_ReplacesTheSiteSet()
    {
        var (db, sites) = await SeedSitesAsync(3);
        using var _ = db;
        HolidayModel created;
        await using (var context = db.NewProductionLikeContext())
        {
            created = await Service(context).AddAsync(Special([sites[0], sites[1]]), default);
        }

        await using (var context = db.NewProductionLikeContext())
        {
            var update = new UpdateHoliday
            {
                Id = created.Id, Description = "Osmeña Day", HolType = HolidayType.SPECIAL,
                WorkType = HolidayWorkType.NonWorking, HolDate = Day, AreaIds = [sites[1], sites[2]], Status = "Active",
            };
            var updated = await Service(context).UpdateAsync(update, default);
            updated.AreaIds.Should().BeEquivalentTo([sites[1], sites[2]]);
        }

        (await StoredSitesAsync(db, created.Id)).Should().BeEquivalentTo([sites[1], sites[2]]);
    }

    [Fact]
    public async Task Update_ToLegal_DropsAllSites()
    {
        var (db, sites) = await SeedSitesAsync(2);
        using var _ = db;
        HolidayModel created;
        await using (var context = db.NewProductionLikeContext())
        {
            created = await Service(context).AddAsync(Special(sites), default);
        }

        await using (var context = db.NewProductionLikeContext())
        {
            var update = new UpdateHoliday
            {
                Id = created.Id, Description = "Osmeña Day", HolType = HolidayType.LEGAL,
                WorkType = HolidayWorkType.NonWorking, HolDate = Day, AreaIds = sites, Status = "Active",
            };
            await Service(context).UpdateAsync(update, default);
        }

        (await StoredSitesAsync(db, created.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Add_UnknownSite_IsRejected()
    {
        var (db, sites) = await SeedSitesAsync(1);
        using var _ = db;
        await using var context = db.NewProductionLikeContext();

        var act = () => Service(context).AddAsync(Special([sites[0], Guid.NewGuid()]), default);

        await act.Should().ThrowAsync<GuardException>();
    }

    [Fact]
    public async Task GetAllHolidays_CarriesTheSitesForTheDtr()
    {
        var (db, sites) = await SeedSitesAsync(2);
        using var _ = db;
        await using (var context = db.NewProductionLikeContext())
        {
            await Service(context).AddAsync(Special(sites), default);
        }

        await using var read = db.NewContext();
        var holidays = await Service(read).GetAllHolidays(Day, Day, default);

        holidays.Should().ContainSingle().Which.AreaIds.Should().BeEquivalentTo(sites);
    }
}
