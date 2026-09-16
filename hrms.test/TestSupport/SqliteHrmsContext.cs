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

    public HrmsContext NewContext(Guid? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<HrmsContext>()
            .UseSqlite(_connectionString, x => x.UseNetTopologySuite())
            .Options;
        return new HrmsContext(options, null!, new FakeTenantProvider(tenantId ?? Guid.Empty), null!);
    }

    public void Dispose() => _keepAlive.Dispose();

    private sealed class FakeTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid TenantId { get; private set; } = tenantId;
        public void SetTenantId(Guid newTenantId) => TenantId = newTenantId;
    }
}
