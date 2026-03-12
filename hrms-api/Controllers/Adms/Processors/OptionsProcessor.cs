namespace Hrms.Api.Controllers.Adms;

public class OptionsProcessor : ICDataProcessor
{
    private readonly ILogger<OptionsProcessor> _logger;
    public OptionsProcessor(ILogger<OptionsProcessor> logger)
    {
        _logger = logger;
    }
    public async Task ProcessAsync(BioPayload payload, CancellationToken token = default)
    {
        //var deviceConfig = data.Split(',')
        //       .Select(part => part.Split('='))
        //       .Where(part => part.Length >= 2)
        //       .ToDictionary(
        //           split => split[0].Trim(),
        //           split => split[1].Trim()
        //       );

        //Console.WriteLine($"Device: {deviceConfig["~DeviceName"]}");
        //Console.WriteLine($"Firmware: {deviceConfig["FWVersion"]}");

        //// Handling missing or empty values safely
        //if (deviceConfig.TryGetValue("MAC", out string mac))
        //{
        //    Console.WriteLine($"MAC Address: {mac}");
        //}

        await Task.CompletedTask;
    }
}
