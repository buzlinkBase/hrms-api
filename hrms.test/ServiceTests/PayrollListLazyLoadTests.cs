using hrms.test.TestSupport;
using MapsterMapper;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// The Payroll Summary list serializes Payroll entities directly; with lazy-loading proxies the
/// serializer fired a DeductionCollection query per row. GetForListAsync switches lazy loading off
/// for the rest of the request so only what the query included is serialized.
/// </summary>
public class PayrollListLazyLoadTests
{
    [Fact]
    public async Task GetForListAsync_DisablesLazyLoadingForTheResponse()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = new PayrollService(uow, Substitute.For<IMapper>());

        context.ChangeTracker.LazyLoadingEnabled.Should().BeTrue();

        var data = await service.GetForListAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), null, null, null, null, CancellationToken.None);

        data.Should().BeEmpty();
        context.ChangeTracker.LazyLoadingEnabled.Should().BeFalse();
    }
}
