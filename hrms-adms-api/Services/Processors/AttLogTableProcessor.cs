using Hrms.adms.Services;

namespace Hrms.adms.Services.Processors;

public interface ICDataProcessor
{
    Task ProcessAsync(BioPayload payload, CancellationToken token = default);
}
public class AttLogTableProcessor : ICDataProcessor
{
    private readonly IPublishEndpoint _publisher;
    private readonly AttendanceService _service;

    public AttLogTableProcessor(IPublishEndpoint publisher, AttendanceService service)
    {
        _publisher = publisher;
        _service = service;
    }
    public async Task ProcessAsync(BioPayload payload, CancellationToken token = default)
    {
        var atts = new List<CreateAttendancePayload>();
        var lines = payload.RawData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var batch = Guid.NewGuid().ToString("N");
        foreach (var line in lines)
        {
            var fields = line.Split('\t');
            if (fields.Length >= 2)
            {
                var bioId = int.TryParse(fields[0], out int id) ? id : 0;
                var attendance = new CreateAttendancePayload
                {
                    BioId = bioId,
                    BatchId = batch,
                    WorkDateTime = DateTime.TryParse(fields[1], out DateTime dt) ? dt : DateTime.Now,
                    TenantId = payload.Info.DeviceInfo.TenantId,
                    BranchId = payload.Info.DeviceInfo.BranchId,
                    ClientId = payload.Info.DeviceInfo.ClientId,
                    DepartmentId = payload.Info.DeviceInfo.DepartmentId,
                    OperationAreaId = payload.Info.DeviceInfo.OperationAreaId,
                    DeviceName = payload.SN,
                    IPAddress=payload.Info.DeviceInfo.IpAddress,
                };
                atts.Add(attendance);
            }
        }

        if (!atts.Any()) return;
        await _publisher.Publish(new AttendancePayloadWrapper { AttLogs = atts });

        //store syncing record
        //this batch can be resend if something went DLQ arrise
        var attentties = new List<Attendance>();
        foreach (var attendance in atts)
        {
            var att = new Attendance
            {
                BatchId = batch,
                BranchId = attendance.BranchId,
                BioId = attendance.BioId,
                ClientId = attendance.ClientId,
                DepartmentId = attendance.DepartmentId,
                OperationAreaId = attendance.OperationAreaId,
                DeviceName = payload.SN,
                TenantId = attendance.TenantId,
                WorkDateTime = attendance.WorkDateTime,
            };
            attentties.Add(att);
        }
        await _service.AddRangeAsync(attentties);
        await _service.CommitChangesAsync(token);
    }
}
