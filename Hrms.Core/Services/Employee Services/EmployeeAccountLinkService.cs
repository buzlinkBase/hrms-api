using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hrms.Core.Services;

/// <summary>
/// Saves the link between a login account and the employee it was matched to by email.
///
/// Employee.UserId is what push notifications (Clients.User), department/position approver
/// lookups and "is this person on the portal" checks rely on. It's set when an invitation is
/// accepted (UserOnboardedWorker) -- but that worker never actually linked anyone until its guard
/// was fixed, so accounts that joined earlier are only ever matched by email at request time.
/// This heals them the first time such a request comes in.
///
/// The link is queued, not written inline: the calling request's unit of work holds its own
/// transaction (usually a read that never commits), and writing the same row from a second
/// connection while it's open could wait on that transaction's locks. The background
/// <see cref="EmployeeAccountLinkWorker"/> writes it moments later in its own committed unit of
/// work, pointed at the same company database as the request (that connection is resolved per
/// request by TenantDatabaseMiddleware into the scoped TenantConnectionStringInfo).
/// </summary>
public class EmployeeAccountLinkService
{
    private readonly EmployeeAccountLinkQueue _queue;
    private readonly TenantConnectionStringInfo _currentConnection;

    public EmployeeAccountLinkService(EmployeeAccountLinkQueue queue, TenantConnectionStringInfo currentConnection)
    {
        _queue = queue;
        _currentConnection = currentConnection;
    }

    public void RequestLink(Guid employeeId, Guid userId) =>
        _queue.TryEnqueue(new EmployeeAccountLink(employeeId, userId, _currentConnection.TenantId, _currentConnection.ConnectionString));
}

public sealed record EmployeeAccountLink(Guid EmployeeId, Guid UserId, Guid TenantId, string? ConnectionString);

/// <summary>
/// Pending links, shared by requests and the worker. Bounded and lossy on purpose: a dropped
/// request is simply retried the next time that person is resolved by email.
/// </summary>
public sealed class EmployeeAccountLinkQueue
{
    private readonly Channel<EmployeeAccountLink> _channel = Channel.CreateBounded<EmployeeAccountLink>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public bool TryEnqueue(EmployeeAccountLink link) => _channel.Writer.TryWrite(link);

    public ChannelReader<EmployeeAccountLink> Reader => _channel.Reader;
}

public sealed class EmployeeAccountLinkWorker : BackgroundService
{
    private readonly EmployeeAccountLinkQueue _queue;
    private readonly IServiceScopeFactory _scopes;

    public EmployeeAccountLinkWorker(EmployeeAccountLinkQueue queue, IServiceScopeFactory scopes)
    {
        _queue = queue;
        _scopes = scopes;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var link in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await LinkAsync(link, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Retried naturally the next time this person is resolved by email.
                Log.Warning(ex, "EmployeeAccountLinkWorker: could not link employee {EmployeeId} to user {UserId}", link.EmployeeId, link.UserId);
            }
        }
    }

    /// <summary>Links only an employee that isn't linked yet; a no-op otherwise, so repeats are harmless.</summary>
    public async Task LinkAsync(EmployeeAccountLink link, CancellationToken token)
    {
        using var scope = _scopes.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<TenantConnectionStringInfo>();
        connection.TenantId = link.TenantId;
        connection.ConnectionString = link.ConnectionString;
        // No HTTP request here: tell the tenant provider which company this is for too, so the
        // tenant query filter matches the employee's company.
        scope.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantId(link.TenantId);

        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>();
        await uow.Context.Employees
            .Where(x => x.Id == link.EmployeeId && x.UserId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, link.UserId), token);
        await uow.CommitChangesAsync("", token);
    }
}
