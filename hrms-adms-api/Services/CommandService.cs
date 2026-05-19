using System.Net.WebSockets;

namespace Hrms.adms.Services;

public class CommandService : BaseService<DeviceCommand>
{
    private readonly DeviceService _service;
    public CommandService(IUnitOfWorkService uow,
        DeviceService service) : base(uow)
    {
        _service = service;
    }
    public async Task CreateCommand(List<DeviceCommand> commands)
    {
        await Repository.AddRangeAsync(commands);
        await CommitChangesAsync();
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
