namespace Hrms.Core.Services;

public class DatParser : IFileParser
{

    private readonly ITenantProvider _tenantProvider;
    public DatParser(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
    }

    public async Task<List<CreateAttendancePayload>> Parse(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        var batch = Guid.NewGuid();
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var parsedLogs = new List<CreateAttendancePayload>();
        foreach (var line in lines)
        {
            var parts = line.Split('\t');
            if (parts.Length >= 2 && int.TryParse(parts[0], out var id) && DateTime.TryParse(parts[1], out var timestamp))
            {
                parsedLogs.Add(new CreateAttendancePayload
                {
                    BioId = id,
                    WorkDateTime = timestamp,
                    TenantId = _tenantProvider.TenantId,
                    BatchId = batch,
                    DeviceName = "Dat file",
                });
            }
        }
        return parsedLogs;
    }
}