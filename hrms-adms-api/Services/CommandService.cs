using System.Configuration;
using System.Net.WebSockets;

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
        await CommitChangesAsync();
    }

    public async Task<List<DeviceCommand>> FindAllPending(string sn)
    {
        var data = await GetQueryable(x=>x.SN==sn)
            .ToListAsync();
        return data;
    }
    public async Task<List<DeviceCommand>> FindOldCommandAsync()
    {
        if (!int.TryParse(_configuration["CLEAN_UP_AGE"], out int maxAgeMinutes))
        {
            maxAgeMinutes = 480; // 8 hours default
        }
        DateTime cutoffTime = DateTime.UtcNow.AddMinutes(-maxAgeMinutes);
        // 3. Query records created BEFORE the cutoff point and execute asynchronously
        var data = await GetQueryable(x => x.CreatedAt < cutoffTime)
            .ToListAsync();
        return data;
    }

    public async Task<List<DeviceCommand>> GetAllCommandsAsync(string sn)
    {
        var device = await _service.FindSnAsync(sn,CancellationToken.None);
        if (device == null) return new List<DeviceCommand>(); 
        return GetQueryable(x => x.SN == sn)
            .Skip(0)
            .Take(30)
            .ToList();
    }
    public void Delete(Guid Id)
    {
        GetQueryable(x => x.Id == Id)
            .ExecuteDelete();
    }
}
