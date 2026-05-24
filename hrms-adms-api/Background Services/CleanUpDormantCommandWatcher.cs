namespace Hrms.adms.Services;
public class CleanUpDormantCommandWatcher : BackgroundService
{
    private readonly TimeSpan _loopDelay = TimeSpan.FromMinutes(5);
    private readonly IServiceScopeFactory _factory;

    public CleanUpDormantCommandWatcher(IServiceScopeFactory factory)
    {
        _factory = factory;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var scope = _factory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<CommandService>();
                var data = await service.FindOldCommandAsync();
                if (data != null && data.Any())
                {
                    foreach (var command in data)
                    {
                        service.Delete(command.Id);
                    }
                    await service.CommitChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error in getting dormant command {0}", ex.Message);
            }
            await Task.Delay(_loopDelay, stoppingToken);
        }
    }
}
