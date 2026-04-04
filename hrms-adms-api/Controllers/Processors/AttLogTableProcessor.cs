namespace Hrms.adms.Controllers.Processors;
public interface ICDataProcessor
{
    Task ProcessAsync(BioPayload payload, CancellationToken token = default);
}
public class AttLogTableProcessor : ICDataProcessor
{
    private readonly IPublishEndpoint _publisher;
    public AttLogTableProcessor(IPublishEndpoint publisher)
    {
        _publisher = publisher;
    }

    public async Task ProcessAsync(BioPayload payload, CancellationToken token = default)
    {
        var atts = new List<CreateAttendancePayload>();
        var lines = payload.RawData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var fields = line.Split('\t');
            if (fields.Length >= 2)
            {
                var bioId = int.TryParse(fields[0], out int id) ? id : 0;
                var attendance = new CreateAttendancePayload()
                {
                    BioId = bioId,
                    WorkDateTime = DateTime.TryParse(fields[1], out DateTime dt) ? dt : DateTime.Now,
                    TenantId = payload.DeviceInfo.TenantId,
                    BranchId = payload.DeviceInfo.BranchId,
                    ClientId = payload.DeviceInfo.ClientId,
                    DepartmentId = payload.DeviceInfo.DepartmentId,
                    DeviceName = payload.SN,
                };
                atts.Add(attendance);


            }
        }
        if (!atts.Any()) return;
        await _publisher.Publish(atts);
    }
}
