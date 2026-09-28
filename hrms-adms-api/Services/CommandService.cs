namespace Hrms.adms.Services;

public class CommandService : BaseService<DeviceCommand>
{
    private readonly DeviceService _service;
    private readonly IConfiguration _configuration;

    public CommandService(IUnitOfWorkService uow,
        DeviceService service,
        IConfiguration configuration) : base(uow)
    {
        _service = service;
        _configuration = configuration;
    }
    public async Task CreateCommand(List<DeviceCommand> commands)
    {
        await Repository.AddRangeAsync(commands);
        await SaveChangesAsync();
        await CommitChangesAsync();
    }

    public async Task<List<CommandReturnModel>> FindAllPending(string sn, Guid tenantId)
    {
        var data = await GetQueryable(x => x.SN == sn && x.TenantId == tenantId)
            .Select(x => new CommandReturnModel()
            {
                Id = x.Id,
                SN = x.SN,
                CommandType = x.CommandType
            })
            .ToListAsync();
        return data;
    }

    /// <summary>
    /// Deletes every command created before the cleanup cutoff in one statement -- no rows are
    /// loaded into memory. Runs without a tenant (background job), so it spans all tenants.
    /// </summary>
    public async Task<int> DeleteOldCommandsAsync(CancellationToken token = default)
    {
        if (!int.TryParse(_configuration["CLEAN_UP_AGE"], out int maxAgeMinutes))
        {
            maxAgeMinutes = 480; // 8 hours default
        }
        DateTime cutoffTime = DateTime.UtcNow.AddMinutes(-maxAgeMinutes);
        return await GetQueryable(x => x.CreatedAt < cutoffTime)
            .ExecuteDeleteAsync(token);
    }

    public async Task<List<DeviceCommand>> GetAllCommandsAsync(string sn)
    {
        var device = await _service.FindSnAsync(sn, CancellationToken.None);
        if (device == null) return new List<DeviceCommand>();
        return GetQueryable(x => x.SN == sn)
            .OrderBy(x => x.Id)
            .Skip(0)
            .Take(30)
            .ToList();
    }
    // A device acknowledging a command it ran (AdmsController): a bulk delete, deliberately not
    // audited -- that's the device's bookkeeping, not anyone's action.
    public async Task DeleteAsync(Guid Id)
    {
        await GetQueryable(x => x.Id == Id)
             .ExecuteDeleteAsync();
    }

    // An admin cancelling a queued command: removed through the change tracker so it's recorded
    // in the audit trail (DeviceCommand is audited on create and delete).
    public async Task CancelAsync(Guid Id, CancellationToken token = default)
    {
        var command = await GetQueryable(x => x.Id == Id).FirstOrDefaultAsync(token);
        if (command != null)
            await RemoveAsync(command);
    }
}
public record CommandReturnModel
{
    public Guid Id { get; set; }
    public string SN { get; set; }
    public string CommandType { get; set; }
}