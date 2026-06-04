namespace Hrms.adms.Services.Processors;

public class UserInforTableProcessor(DeviceUserService service) : ICDataProcessor
{
    private static readonly char[] LineSeparators = ['\n', '\r'];

    public async Task ProcessAsync(BioPayload payload, CancellationToken token = default)
    {
        var tenantId = payload.Info.DeviceInfo.TenantId;
        var users = new List<DeviceUser>();

        var lines = payload.RawData.Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var fields = line.Split('\t');
            var kv = new Dictionary<string, string>();
            foreach (var field in fields)
            {
                var pair = field.Split('=', 2);
                if (pair.Length == 2)
                    kv[pair[0].Trim()] = pair[1].Trim();
            }

            users.Add(new DeviceUser
            {
                SN = payload.SN,
                TenantId = tenantId,
                UserPin = kv.GetValueOrDefault("PIN", "0"),
                Name = kv.GetValueOrDefault("Name", "Unknown"),
                Priority = int.TryParse(kv.GetValueOrDefault("Pri"), out int p) ? p : 0,
                Password = kv.GetValueOrDefault("Passwd", ""),
                Card = kv.GetValueOrDefault("Card", ""),
            });
        }

        if (users.Count == 0) return;
        await service.UpsertAsync(users, token);
    }
}
