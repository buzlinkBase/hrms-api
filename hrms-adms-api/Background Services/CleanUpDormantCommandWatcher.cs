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
                // CRITICAL: wrapping in 'using' ensures the scope and its resolved 
                // CommandService are cleanly disposed of as soon as the closing bracket } is hit.
                using (var scope = _factory.CreateScope())
                {
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
                } // <--- Scope is safely disposed of here every 5 minutes
            }
            catch (Exception ex)
            {
                // Cleaned up logging interpolation syntax
                Log.Error(ex, "Error in getting dormant command: {Message}", ex.Message);
            }

            // Put the delay outside the try-catch block so a failure doesn't 
            // cause an infinite rapid-fire loop crash.
            await Task.Delay(_loopDelay, stoppingToken);
        }
    }
}