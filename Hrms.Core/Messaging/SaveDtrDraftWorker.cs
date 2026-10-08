using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace Hrms.Core.Messaging;

/// <summary>
/// Carries out a queued Calculate DTR "Save Draft" (see <see cref="DtrDraftSaveService"/>) outside
/// the HTTP request. The save is one transaction, so a failure leaves nothing behind; it is not
/// retried automatically (a retry of a deterministic failure would only repeat it) -- the requester
/// gets a "Save Failed" notice with the reason and can simply save again.
/// </summary>
public class SaveDtrDraftWorker : IConsumer<SaveDtrDraftRequested>
{
    private readonly DtrDraftSaveService _saver;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITenantProvider _tenantProvider;
    private readonly TenantConnectionStringInfo _connectionInfo;

    public SaveDtrDraftWorker(
        DtrDraftSaveService saver,
        IServiceScopeFactory scopeFactory,
        ITenantProvider tenantProvider,
        TenantConnectionStringInfo connectionInfo)
    {
        _saver = saver;
        _scopeFactory = scopeFactory;
        _tenantProvider = tenantProvider;
        _connectionInfo = connectionInfo;
    }

    public async Task Consume(ConsumeContext<SaveDtrDraftRequested> context)
    {
        var request = context.Message;
        var token = context.CancellationToken;

        try
        {
            var result = await _saver.SaveAsync(request, token);
            Log.Information("DTR draft saved [{Tenant}] {From}..{To}: {Employees} employee(s), {Rows} row(s), batch {Batch}",
                _tenantProvider.TenantId, request.FromDate, request.ToDate, result.EmployeeCount, result.RowCount, result.BatchCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Error(ex, "DTR draft save failed [{Tenant}] {From}..{To}", _tenantProvider.TenantId, request.FromDate, request.ToDate);
            await NotifyFailureAsync(request, ex, token);
        }
    }

    // The failed save's unit of work is abandoned (its transaction rolls back when the scope ends), so
    // the notice goes out through a fresh scope set to this message's company, like DailyBenefitsWorker's steps.
    private async Task NotifyFailureAsync(SaveDtrDraftRequested request, Exception ex, CancellationToken token)
    {
        var reason = ex is ValidationException
            ? ex.Message
            : "Something went wrong while saving. Nothing was saved -- please try again.";
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var services = scope.ServiceProvider;
            services.GetRequiredService<ITenantProvider>().SetTenantId(_tenantProvider.TenantId);
            var connection = services.GetRequiredService<TenantConnectionStringInfo>();
            connection.TenantId = _connectionInfo.TenantId;
            connection.ConnectionString = _connectionInfo.ConnectionString;

            await services.GetRequiredService<DtrDraftSaveService>()
                .NotifyFailureAsync(request.GeneratedByEmployeeId, reason, token);
        }
        catch (Exception notifyEx) when (notifyEx is not OperationCanceledException)
        {
            Log.Error(notifyEx, "DTR draft save failed and the requester could not be told [{Tenant}]", _tenantProvider.TenantId);
        }
    }
}

public class SaveDtrDraftWorkerDefinition : ConsumerDefinition<SaveDtrDraftWorker>
{
    public SaveDtrDraftWorkerDefinition()
    {
        EndpointName = "hrms-save-dtr-draft-que";
        // One at a time per instance: a second save of the same cutoff then finds the first one's rows
        // already saved and skips them instead of racing it.
        ConcurrentMessageLimit = 1;
    }
}
