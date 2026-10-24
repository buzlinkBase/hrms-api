using BuzlinkRepository;
using Hrms.Core;
using Hrms.Core.Services;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using hrms.test.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace hrms.test.ServiceTests;

/// <summary>Tagging Project Sites (Areas) to a client: optional on both sides, one client per site.</summary>
public class ClientAreaTagTests
{
    private static async Task<(Guid ClientA, Guid ClientB, Guid Hall, Guid Plant)> SeedAsync(SqliteHrmsContext db)
    {
        await using var ctx = db.NewContext();
        var a = new Client { Id = Guid.NewGuid(), Code = "A", Name = "Client A" };
        var b = new Client { Id = Guid.NewGuid(), Code = "B", Name = "Client B" };
        var hall = new CostCenters { Id = Guid.NewGuid(), Code = "HALL", Name = "City Hall" };
        var plant = new CostCenters { Id = Guid.NewGuid(), Code = "PLANT", Name = "Plant" };
        ctx.Clients.AddRange(a, b);
        ctx.Areas.AddRange(hall, plant);
        await ctx.SaveChangesAsync();
        return (a.Id, b.Id, hall.Id, plant.Id);
    }

    private static UpdateClient Payload(Guid id, string name, List<Guid>? areaIds, bool reassign = false) =>
        new() { Id = id, Code = name, Name = name, AreaIds = areaIds, ReassignAreas = reassign };

    private static async Task<List<Guid>> TaggedAsync(SqliteHrmsContext db, Guid clientId)
    {
        await using var ctx = db.NewContext();
        return await ctx.Areas.Where(x => x.ClientId == clientId).Select(x => x.Id).ToListAsync();
    }

    [Fact]
    public async Task Creating_a_client_tags_its_areas_and_none_is_fine_too()
    {
        using var db = new SqliteHrmsContext();
        var (_, _, hall, plant) = await SeedAsync(db);

        Guid withAreas;
        await using (var ctx = db.NewContext())
        {
            var client = new Client { Id = Guid.NewGuid(), Code = "C", Name = "Client C" };
            await new ClientService(new UnitOfWorkService(ctx)).AddAsync(client, [hall, plant], false, CancellationToken.None);
            withAreas = client.Id;
        }
        (await TaggedAsync(db, withAreas)).Should().BeEquivalentTo([hall, plant]);

        await using var plain = db.NewContext();
        var noAreas = new Client { Id = Guid.NewGuid(), Code = "D", Name = "Client D" };
        await new ClientService(new UnitOfWorkService(plain)).AddAsync(noAreas, null, false, CancellationToken.None);
        (await TaggedAsync(db, noAreas.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Update_replaces_the_tagged_areas_and_null_leaves_them_alone()
    {
        using var db = new SqliteHrmsContext();
        var (a, _, hall, plant) = await SeedAsync(db);

        await using (var ctx = db.NewContext())
            await new ClientService(new UnitOfWorkService(ctx)).UpdateAsync(Payload(a, "Client A", [hall, plant]), CancellationToken.None);
        (await TaggedAsync(db, a)).Should().BeEquivalentTo([hall, plant]);

        await using (var ctx = db.NewContext())
            await new ClientService(new UnitOfWorkService(ctx)).UpdateAsync(Payload(a, "Client A", [plant]), CancellationToken.None);
        (await TaggedAsync(db, a)).Should().BeEquivalentTo([plant]);

        await using (var ctx = db.NewContext())
            await new ClientService(new UnitOfWorkService(ctx)).UpdateAsync(Payload(a, "Client A", null), CancellationToken.None);
        (await TaggedAsync(db, a)).Should().BeEquivalentTo([plant], "a caller that omits areas must not wipe them");

        await using (var ctx = db.NewContext())
            await new ClientService(new UnitOfWorkService(ctx)).UpdateAsync(Payload(a, "Client A", []), CancellationToken.None);
        (await TaggedAsync(db, a)).Should().BeEmpty();
    }

    [Fact]
    public async Task An_area_tagged_to_another_client_needs_an_explicit_reassign()
    {
        using var db = new SqliteHrmsContext();
        var (a, b, hall, _) = await SeedAsync(db);
        await using (var ctx = db.NewContext())
            await new ClientService(new UnitOfWorkService(ctx)).UpdateAsync(Payload(a, "Client A", [hall]), CancellationToken.None);

        await using (var ctx = db.NewContext())
        {
            var service = new ClientService(new UnitOfWorkService(ctx));
            await FluentActions.Awaiting(() => service.UpdateAsync(Payload(b, "Client B", [hall]), CancellationToken.None))
                .Should().ThrowAsync<ValidationException>().WithMessage("*City Hall*");
        }
        (await TaggedAsync(db, a)).Should().BeEquivalentTo([hall]);

        await using (var ctx = db.NewContext())
            await new ClientService(new UnitOfWorkService(ctx)).UpdateAsync(Payload(b, "Client B", [hall], reassign: true), CancellationToken.None);
        (await TaggedAsync(db, b)).Should().BeEquivalentTo([hall]);
        (await TaggedAsync(db, a)).Should().BeEmpty();
    }

    [Fact]
    public async Task An_unknown_area_is_rejected()
    {
        using var db = new SqliteHrmsContext();
        var (a, _, _, _) = await SeedAsync(db);

        await using var ctx = db.NewContext();
        var service = new ClientService(new UnitOfWorkService(ctx));
        await FluentActions.Awaiting(() => service.UpdateAsync(Payload(a, "Client A", [Guid.NewGuid()]), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }
}
