using Hrms.adms.api.Controllers;

namespace Hrms.adms.api.Controllers.Processors;

public class UserInforTableProcessor : ICDataProcessor
{
    public async Task ProcessAsync(BioPayload payload, CancellationToken token = default)
    {
        var lines = payload.RawData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var fields = line.Split('\t');
            var userDict = new Dictionary<string, string>();
            foreach (var field in fields)
            {
                var kv = field.Split('=', 2); 
                if (kv.Length == 2)
                {
                    userDict[kv[0].Trim()] = kv[1].Trim();
                }
            }
            var user = new UserRegistration
            {
                UserPin = userDict.GetValueOrDefault("USER PIN", "0"),
                Name = userDict.GetValueOrDefault("Name", "Unknown"),
                Priority = int.TryParse(userDict.GetValueOrDefault("Pri"), out int p) ? p : 0,
                Password = userDict.GetValueOrDefault("Passwd", ""),
                Card = userDict.GetValueOrDefault("Card", "")
            };
        }
    }
}

