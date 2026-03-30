using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using MassTransit;
namespace OnePunch.Auth.Core.Messaging;

public class CreateAttendanceWorker : IConsumer<List<CreateAttendancePayload>>
{

    private readonly AttendanceService _attService;
    private readonly ITenantProvider _tenantProvider;
    private readonly EmployeeService _employeeService;

    public CreateAttendanceWorker(AttendanceService attService,
        EmployeeService employeeService)
    {
        _attService = attService;
        _employeeService = employeeService;
    }

    public async Task Consume(ConsumeContext<List<CreateAttendancePayload>> context)
    {
        var messages = context.Message;
        var atts = new List<Attendance>();
        var batch = Guid.NewGuid().ToString();

        var tenant = _tenantProvider;

        var employees = await _employeeService
             .GetQueryable()
             .Where(x => x.BioId != 0)
             .ToDictionaryAsync(x => x.BioId, x => x);

        foreach (var att in messages)
        {
            Guid? employeeId = null;
            if (employees.TryGetValue(att.BioId, out Employee? emp))
            {
                employeeId = emp.Id;
            }
            var attendance = new Attendance()
            {
                BatchCode = batch,
                BioId = att.BioId,
                WorkDateTime = att.WorkDateTime,
                EmployeeId = employeeId,
                BranchId = att.BranchId,
                DepartmentId = att.DepartmentId,
                ClientId = att.ClientId,
                DeviceName = att.DeviceName,
                Coordinates = att.Coordinates,
                LogSource = LOGSOURCE.ADMS,
                RecordStatus = DTRStatus.OPEN,
            };
            atts.Add(attendance);
        }
        await _attService.AddRangeAsync(atts);
        await _attService.CommitChangesAsync(context.CancellationToken);
    }
}
