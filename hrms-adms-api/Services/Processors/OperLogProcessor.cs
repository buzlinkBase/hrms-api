namespace Hrms.adms.Services.Processors;

public class OperLogProcessor : ICDataProcessor
{
    private readonly TemplateService _bioTemplateService;

    public OperLogProcessor(TemplateService bioTemplateService,
        ITenantProvider tenantProvider)
    {
        _bioTemplateService = bioTemplateService;
    }
    public async Task ProcessAsync(BioPayload payload, CancellationToken token = default)
    {
        var lines = payload.RawData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var templatesToRegister = new List<CreateBiometricTemplate>();
        foreach (var line in lines)
        {
            if (line.Contains("FP PIN"))
            {
                var fp = await ParseTemplateAsync(line, "FP PIN", BiometricType.Fingerprint, payload.Info.DeviceInfo.TenantId);
                if (fp != null)
                {
                    templatesToRegister.Add(fp);
                }
            }
            else if (line.Contains("FACE PIN"))
            {
                var face = await ParseTemplateAsync(line, "FACE PIN", BiometricType.Face, payload.Info.DeviceInfo.TenantId);
                if (face != null)
                {
                    templatesToRegister.Add(face);
                }
            }
        }
        if (templatesToRegister.Any())
        {
            await _bioTemplateService.AddRangeTemplate(templatesToRegister, token);
        }
    }

    private async Task<CreateBiometricTemplate?> ParseTemplateAsync(string line, string pinKey, BiometricType type, Guid tenantId)
    {
        var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in parts)
        {
            var kv = part.Split('=');
            if (kv.Length > 0 && (kv.Length == 2 || kv[0] == "TMP"))
            {
                data.TryAdd(kv[0].Trim(), kv[1].Trim());
            }
        }

        // Validate essential fields exist
        if (data.TryGetValue(pinKey, out var pin) &&
            data.TryGetValue("FID", out var fid) &&
            data.TryGetValue("SIZE", out var size) &&
            data.TryGetValue("TMP", out var template))
        {
            return new CreateBiometricTemplate
            {
                TenantId = tenantId,
                BioId = int.Parse(pin),
                BioIndex = int.Parse(fid),
                BioType = type,
                TemplateData = template,
                TemplateSize = int.Parse(size)
            };
        }
        return null;
    }
}