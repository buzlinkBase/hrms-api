using BuzlinkRepository;
using Hrms.Core.Services;
using Hrms.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace hrms.test.TestSupport;

// A real, relational HrmsContext backed by a SQLite in-memory database — needed for any test
// that exercises Database.BeginTransactionAsync/CommitAsync/RollbackAsync, or IUnitOfWorkService's
// own transaction handling (see PayrollBatchLifecycleServiceTests). EF Core's own InMemory
// provider (used everywhere else in this project) deliberately rejects those relational-only
// APIs ("Relational-specific methods can only be used when the context is using a relational
// database provider"), so it cannot exercise this code path at all -- SQLite is the lightest real
// relational provider available, and its shared-cache in-memory mode keeps it from touching disk.
//
// Uses a NAMED shared-cache in-memory database (mode=memory&cache=shared): every NewContext()
// call opens its own genuinely separate connection with real transactional isolation from the
// others, while all of them point at the same in-memory database. That database is destroyed
// once its last connection closes, so _keepAlive holds one open for the lifetime of this
// instance.
//
// Deliberately does NOT expose a single shared Context/IUnitOfWorkService -- IUnitOfWorkService
// (BuzlinkRepository) opens its own ambient DB transaction the moment it's constructed, which
// then holds a write lock for its entire lifetime. A test needs to seed "pre-existing" data
// (e.g. a PayrollBatch a prior, already-completed request created) via its OWN NewContext() call,
// fully committed and disposed, BEFORE constructing the IUnitOfWorkService under test -- exactly
// like a real Post request's scope starting fresh only after an earlier Generate request's own
// scope already committed and ended. Seeding through the same IUnitOfWorkService the test then
// exercises would (a) risk a lock conflict against that IUnitOfWorkService's own ambient
// transaction, and (b) put the seed inside that same transaction, so a later rollback in the
// method under test would incorrectly wipe the seed too.
/// <summary>Counts every SQL command EF runs (reads, writes and scalars).</summary>
public sealed class SqlCommandCounter : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
{
    public int Count { get; private set; }

    public void Reset() => Count = 0;

    public override Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> ReaderExecuting(
        System.Data.Common.DbCommand command,
        Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
        Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result)
    {
        Count++;
        return result;
    }

    public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
        System.Data.Common.DbCommand command,
        Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
        Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Count++;
        return ValueTask.FromResult(result);
    }
}

public sealed class SqliteHrmsContext : IDisposable
{
    private readonly SqliteConnection _keepAlive;
    private readonly string _connectionString;

    public SqliteHrmsContext()
    {
        _connectionString = $"Data Source=file:{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();

        using var schemaContext = NewContext();
        schemaContext.Database.EnsureCreated();
    }

    /// <summary>For registering HrmsContext in a test service container against this same database.</summary>
    public string ConnectionString => _connectionString;

    public HrmsContext NewContext(Guid? tenantId = null) =>
        NewContext(new FakeTenantProvider(tenantId ?? Guid.Empty));

    public HrmsContext NewContext(ITenantProvider tenantProvider) => NewContext(tenantProvider, null);

    /// <summary>A context whose SQL commands are counted -- for proving a code path's query count
    /// does not grow with the amount of data.</summary>
    public HrmsContext NewCountingContext(SqlCommandCounter counter)
    {
        var tenantProvider = new FakeTenantProvider(Guid.Empty);
        var options = new DbContextOptionsBuilder<HrmsContext>()
            .UseSqlite(_connectionString, x => x.UseNetTopologySuite())
            .AddInterceptors(counter)
            .Options;
        return new HrmsContext(options, null!, tenantProvider, null!, null);
    }

    /// <summary>A context whose query filter reads the given confidential-access scope.</summary>
    public HrmsContext NewContext(IConfidentialAccess? confidentialAccess, Guid? tenantId = null) =>
        NewContext(new FakeTenantProvider(tenantId ?? Guid.Empty), confidentialAccess);

    /// <summary>
    /// A context set up the way the API's is (ServiceRegitrations): lazy-loading proxies plus the
    /// soft-delete and tenant interceptors -- for code whose behaviour depends on them, such as an
    /// update that reads a navigation property it never Included.
    /// </summary>
    public HrmsContext NewProductionLikeContext(Guid? tenantId = null)
    {
        var tenantProvider = new FakeTenantProvider(tenantId ?? Guid.Empty);
        var options = new DbContextOptionsBuilder<HrmsContext>()
            .UseSqlite(_connectionString, x => x.UseNetTopologySuite())
            .UseLazyLoadingProxies(true)
            .AddInterceptors(new SoftDeleteInterceptor(), new ApplyTenantInterceptor(tenantProvider))
            .Options;
        return new HrmsContext(options, null!, tenantProvider, null!, null);
    }

    private HrmsContext NewContext(ITenantProvider tenantProvider, IConfidentialAccess? confidentialAccess)
    {
        var options = new DbContextOptionsBuilder<HrmsContext>()
            .UseSqlite(_connectionString, x => x.UseNetTopologySuite())
            .Options;
        return new HrmsContext(options, null!, tenantProvider, null!, confidentialAccess);
    }

    public void Dispose() => _keepAlive.Dispose();

    private sealed class FakeTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid TenantId { get; private set; } = tenantId;
        public void SetTenantId(Guid newTenantId) => TenantId = newTenantId;
    }
}
