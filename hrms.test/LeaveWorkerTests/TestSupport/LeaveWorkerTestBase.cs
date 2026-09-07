using System.Linq.Expressions;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using MassTransit;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace hrms.test.LeaveWorkerTests.TestSupport;

/// <summary>
/// Shared mocking helpers for the Leave Workers (MassTransit IConsumer classes under
/// Hrms.Core\Messaging\Leave Workers\) — the first tests in this project to exercise
/// IUnitOfWorkService/IRepository-backed code, so this establishes the pattern rather than
/// reusing an existing one. IRepository declares generic METHODS (Find&lt;T&gt;, Add&lt;T&gt;,
/// etc.) on a non-generic interface, so each entity type used by a worker needs its own
/// SeedFind&lt;T&gt; call. repo.Find&lt;T&gt;(predicate) is wired to compile and apply the
/// predicate against a live in-memory backing list on every call (rather than trying to match
/// a specific expression), then wraps the filtered result through
/// MockQueryable.NSubstitute.BuildMockDbSet so EF's async LINQ surface — ToListAsync,
/// AnyAsync, FirstOrDefaultAsync, AsNoTracking, Include — all work against it exactly as they
/// would against a real DbSet.
/// </summary>
public abstract class LeaveWorkerTestBase
{
    protected static Guid NewId() => Guid.NewGuid();

    protected static IRepository CreateRepo() => Substitute.For<IRepository>();

    protected static IUnitOfWorkService CreateUow(IRepository repo)
    {
        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);
        uow.CommitChangesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        return uow;
    }

    protected static ILogger<T> CreateLogger<T>() => Substitute.For<ILogger<T>>();

    // Wires repo.Find<T>(predicate) against a live List<T> — later mutations (e.g. this same
    // test seeding more rows before a second Consume call) are picked up on the next query,
    // matching how a real DbSet would behave across two calls within one unit of work.
    protected static List<T> SeedFind<T>(IRepository repo, params T[] items) where T : class, IEntity
    {
        var backing = new List<T>(items);
        repo.Find<T>(Arg.Any<Expression<Func<T, bool>>>())
            .Returns(call =>
            {
                var predicate = call.Arg<Expression<Func<T, bool>>>().Compile();
                return backing.Where(predicate).ToList().BuildMockDbSet();
            });
        return backing;
    }

    // Captures every entity handed to repo.Add<T>/AddRange<T> for a given T, in call order —
    // the workers never requery what they just added, they only Add then Commit, so a plain
    // capture list (rather than feeding adds back into SeedFind's backing list) is sufficient
    // and keeps the two concerns (seeded/queryable state vs. what-got-written assertions)
    // separate.
    protected static List<T> CaptureAdds<T>(IRepository repo) where T : class, IEntity
    {
        var captured = new List<T>();
        repo.When(r => r.Add(Arg.Any<T>())).Do(call => captured.Add(call.Arg<T>()));
        repo.When(r => r.AddRange(Arg.Any<IEnumerable<T>>())).Do(call => captured.AddRange(call.Arg<IEnumerable<T>>()));
        return captured;
    }

    protected static ConsumeContext<T> CreateConsumeContext<T>(T message, CancellationToken token = default) where T : class
    {
        var context = Substitute.For<ConsumeContext<T>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(token);
        return context;
    }

    protected static Leave BuildLeave(
        AccrualBasis accrualBasis,
        double credits = 1,
        double accrualRate = 0,
        PaySource paySource = PaySource.Company,
        CarryOverType carryOverType = CarryOverType.Forfeit,
        double carryOverMaxDays = 0,
        int minServiceMonths = 0,
        string description = "Test Leave") => new()
        {
            Id = NewId(),
            Description = description,
            AccrualBasis = accrualBasis,
            Credits = credits,
            AccrualRate = accrualRate,
            PaySource = paySource,
            CarryOverType = carryOverType,
            CarryOverMaxDays = carryOverMaxDays,
            MinServiceMonths = minServiceMonths,
        };

    protected static LeaveCredits BuildCredits(
        Leave leave,
        Guid employeeId,
        int periodYear,
        decimal granted,
        decimal used = 0,
        decimal balance = -1) => new()
        {
            Id = NewId(),
            EmployeeId = employeeId,
            LeaveId = leave.Id,
            Leave = leave,
            PeriodYear = periodYear,
            FromDate = new DateTime(periodYear, 1, 1),
            ToDate = new DateTime(periodYear, 12, 31),
            Granted = granted,
            Used = used,
            Balance = balance < 0 ? granted - used : balance,
        };

    protected static Employee BuildEmployee(
        EmploymentStatus status = EmploymentStatus.Regular,
        DateOnly? hireDate = null) => new()
        {
            Id = NewId(),
            EmploymentStatus = status,
            HireDate = hireDate ?? new DateOnly(2020, 1, 1),
        };
}
