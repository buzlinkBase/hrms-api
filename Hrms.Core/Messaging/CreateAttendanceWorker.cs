using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using MassTransit;
namespace OnePunch.Auth.Core.Messaging;

public class CreateAttendanceWorker : IConsumer<AttendancePayloadWrapper>
{
    private readonly AttendanceService _attService;
    private readonly IUnitOfWorkService _uow;
    private readonly IPublishEndpoint _publish;
    public CreateAttendanceWorker(
        AttendanceService attService,
        IUnitOfWorkService uow,
        IPublishEndpoint publish,
        EmployeeService employeeService)
    {
        _attService = attService;
        _uow = uow;
        _publish = publish;
    }

    public async Task Consume(ConsumeContext<AttendancePayloadWrapper> context)
    {
        var messages = context.Message.AttLogs;

        var atts = await new AttEmployeeSetter(_uow)
            .ParseAttLogs(messages, LOGSOURCE.SYNC);

        await _attService.AddRangeAsync(atts);
        if (await _attService.CommitChangesAsync(context.CancellationToken))
        {
            await _publish.Publish(new BatchAttConfirmation
            {
                BatchId = messages[0].BatchId,
            });
        }
    }
}

public class AttEmployeeSetter
{
    private readonly IUnitOfWorkService _service;
    public AttEmployeeSetter(IUnitOfWorkService service)
    {
        _service = service;
    }

    public async Task<List<Attendance>> ParseAttLogs(List<CreateAttendancePayload> messages, LOGSOURCE logSource)
    {
        var bioIds = messages
          .Select(x => x.BioId)
          .Distinct()
          .ToList();

        if (bioIds.Count == 0)  return new List<Attendance>();

        var employees = await _service.Context.Employees
        .Where(x => x.BioId.HasValue && bioIds.Contains(x.BioId.Value))
        .GroupBy(x => x.BioId!.Value)
        .ToDictionaryAsync(
            g => g.Key,
            g => g.First() 
        );

        var atts = new List<Attendance>();
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
                OperationAreaId = att.OperationAreaId ?? employee?.AreaId,
                DeviceName = att.DeviceName,
                IP = att.IPAddress,
                Boundary = att.Coordinates,
                LogSource = logSource,
                EditRemarks = employee == null ? "Unregistered Employee Bio Id" : ""
            };
            atts.Add(attendance);
        }
        return atts;
    }
}