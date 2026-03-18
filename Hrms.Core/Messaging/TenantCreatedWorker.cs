using MassTransit;
using Microsoft.AspNetCore.Http;

namespace Hrms.Core.Messaging;

public class TenantCreatedWorker : IConsumer<TenantCreatedPayload>
{
    private readonly IPublishEndpoint _publisher;
    private readonly TenantConnectionInfo _connectionInfo;
    private readonly IUnitOfWorkService _unitOfWork;
    private readonly IDigitalOceanDbService _oceanDbService;

    public TenantCreatedWorker(IPublishEndpoint publisher,
        TenantConnectionInfo connectionInfo,
        IUnitOfWorkService unitOfWork,
        IDigitalOceanDbService oceanDbService)
    {
        _publisher = publisher;
        _connectionInfo = connectionInfo;
        _unitOfWork = unitOfWork;
        _oceanDbService = oceanDbService;
    }

    public async Task Consume(ConsumeContext<TenantCreatedPayload> context)
    {
        ConnectionModel? connectionModel = null;
        var message = context.Message;
        string dbName = $"hrms_{message.TenantId:N}";
        try
        {
            connectionModel = await _oceanDbService.CreateTenantDatabaseAsync(dbName);
            if (connectionModel == null)
            {
                Log.Logger.Error($"hrms unable to create db for {message.TenantId} - {message.TenantName}");
                throw new Exception("Cannot create connection");
            }
            var connection = new CreateConnectionString
            {
                ConnetionString = connectionModel.ConnectionString,
                RawConnection = connectionModel.RawConnectionString,
                Environment = "Production",
                ServiceOwner = "hrms",
                TenantId = message.TenantId,
            };
            _connectionInfo.TenantId = message.TenantId;
            _connectionInfo.ConnectionString = connectionModel.ConnectionString;
            await _publisher.Publish(connection);
            await _unitOfWork.CommitChangesAsync("", context.CancellationToken);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Consumer failed. Starting rollback for Tenant {TenantId}", context.Message.TenantId);
            if (connectionModel != null)
            {
                var deleted = await _oceanDbService.DeleteTenantDatabaseAsync(dbName);
                if (!deleted)
                {
                    // This is a big deal - DB exists but couldn't be deleted
                    Log.Logger.Fatal("CRITICAL: Rollback failed! Manual cleanup required for DB: {DbName}", dbName);
                }
            }
            throw;
        }
    }
}
