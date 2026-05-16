using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using MassTransit;
using static MassTransit.Monitoring.Performance.BuiltInCounters;
namespace OnePunch.Auth.Core.Messaging;

public class CreateAttendanceWorker : IConsumer<List<CreateAttendancePayload>>
{

    private readonly AttendanceService _attService;
    private readonly IUnitOfWorkService _unitOfWorkService;
    private readonly IPublishEndpoint _publish;

    public CreateAttendanceWorker(
        AttendanceService attService,
        IUnitOfWorkService unitOfWorkService,
        IPublishEndpoint publish,
        EmployeeService employeeService)
    {
        _attService = attService;
        _unitOfWorkService = unitOfWorkService;
        _publish = publish;
    }

    public async Task Consume(ConsumeContext<List<CreateAttendancePayload>> context)
    {
        var messages = context.Message;
        var atts = await new AttEmployeeSetter(_unitOfWorkService)
            .SetAttendace(messages,LOGSOURCE.ADMS);
        await _attService.AddRangeAsync(atts);
        await _publish.Publish(new BatchAttConfirmation
        {
            BatchId = messages[0].BatchId,
        });
        await _attService.CommitChangesAsync(context.CancellationToken);
    }
}


public class AttEmployeeSetter
{
    private readonly IUnitOfWorkService _service;
    public AttEmployeeSetter(IUnitOfWorkService service)
    {
        _service = service;
    }

    public async Task<List<Attendance>> SetAttendace(List<CreateAttendancePayload> messages, LOGSOURCE logSource)
    {
        //TODO separate unregistered here
        var atts = new List<Attendance>();
        var employees = await _service.Context.Employees
             .Where(x => x.BioId != 0)
             .ToDictionaryAsync(x => x.BioId, x => x);

        foreach (var att in messages)
        {
            employees.TryGetValue(att.BioId, out Employee? employee);
            var attendance = new Attendance
            {
                BatchCode = att.BatchId,
                BioId = att.BioId,
                WorkDateTime = att.WorkDateTime,
                EmployeeId = employee?.Id,
                BranchId = att.BranchId ?? employee?.BranchId,
                DepartmentId = att.DepartmentId ?? employee?.DepartmentId,
                ClientId = att.ClientId ?? employee?.ClientId,
                DeviceName = att.DeviceName,
                Boundary = att.Coordinates,
                LogSource = logSource,
            };
            atts.Add(attendance);
        }
        return atts;
    }
}