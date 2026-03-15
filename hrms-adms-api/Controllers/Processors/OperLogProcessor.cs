namespace Hrms.adms.Controllers.Processors;
public class OperLogProcessor : ICDataProcessor
{
    private readonly BioTemplateService _bioTemplateService;

    public OperLogProcessor(BioTemplateService bioTemplateService)
    {
        _bioTemplateService = bioTemplateService;
    }

    public async Task ProcessAsync(BioPayload payload, CancellationToken token = default)
    {
        // 1. Split by newline to separate individual records
        var lines = payload.RawData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var templatesToRegister = new List<CreateBiometricTemplate>();

        foreach (var line in lines)
        {
            // 2. Identify the type per line
            if (line.Contains("FP PIN"))
            {
                var fp = ParseTemplate(line, "FP PIN", BiometricType.Fingerprint);
                if (fp != null) templatesToRegister.Add(fp);
            }
            else if (line.Contains("FACE PIN"))
            {
                var face = ParseTemplate(line, "FACE PIN", BiometricType.Face);
                if (face != null) templatesToRegister.Add(face);
            }
        }

        // 3. Save everything found in this one payload
        if (templatesToRegister.Any())
        {
            await _bioTemplateService.AddRangeTemplate(templatesToRegister, token);
        }
    }

    private CreateBiometricTemplate? ParseTemplate(string line, string pinKey, BiometricType type)
    {
        var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in parts)
        {
            var kv = part.Split('=');
            if (kv.Length == 2)
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