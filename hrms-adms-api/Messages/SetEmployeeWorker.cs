using Hrms.adms.Models.DTO;
using Hrms.adms.Services;

namespace Hrms.adms.Messages;

public class SetEmployeeWorker : IConsumer<DeviceCommandWrapper<List<SetEmployeeCommandPayload>>>
{
    private readonly CommandService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly IConfiguration _configuration;

    public SetEmployeeWorker(CommandService service,
        ITenantProvider tenantProvider,
        IConfiguration configuration)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _configuration = configuration;
    }
    public async Task Consume(ConsumeContext<DeviceCommandWrapper<List<SetEmployeeCommandPayload>>> context)
    {
        var messages = context.Message;
        ISystemClockService clockService = new SystemClockService(_configuration);
        var formatter = new ZKTecoCommandFormatter(clockService);
        var devCommands = new List<DeviceCommand>();
        foreach (var item in messages.Commands)
        {
            var id = Guid.NewGuid();
            var syncEmpCmd = new DeviceCommandFormmaterPayload
            {
                Id = id,
                Command = "SYNC_EMPLOYEES",
                CommandPayload = "",
                Parameters = new()
            {
                {"employee_code",item.BioId},
                {"name", item.Name },
                {"card_no", item.Card},
                {"privilege",item.Privilege },
                {"password", item.Password},
                {"group_code","1" },
            }
            };
            var resultCommand = formatter.Format(syncEmpCmd);
            var devcommand = new DeviceCommand()
            {
                Id = id,
                Commands = "SYNC_EMPLOYEES",
                SN = messages.DeviceSN,
                TenantId = _tenantProvider.TenantId,
            };
            devCommands.Add(devcommand);
        }
        await _service.CreateCommand(devCommands);
    }
}

