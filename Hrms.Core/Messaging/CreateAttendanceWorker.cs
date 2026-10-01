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
        IPublishEndpoint publish)
    {
        _attService = attService;
        _uow = uow;
        _publish = publish;
    }

    public async Task Consume(ConsumeContext<AttendancePayloadWrapper> context)
    {
        var messages = context.Message.AttLogs;
        if (messages == null || messages.Count == 0) return;

        var atts = await new AttEmployeeSetter(_uow)
            .ParseAttLogs(messages, LOGSOURCE.SYNC);
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

    public async Task<List<Attendance>> ParseAttLogs(List<CreateAttendancePayload> messages, LOGSOURCE logSource)
    {
        var bioIds = messages
          .Select(x => x.BioId)
          .Distinct()
          .ToList();

        if (bioIds.Count == 0)  return new List<Attendance>();

        // Untracked projection of just the fields stamped onto the logs -- not tracked lazy-loading
        // Employee proxies, which the change tracker would otherwise hold for the whole batch.
        // Matched against every employee, confidential ones included: device punches must attach
        // to their owner whoever uploads them (nothing about the employee is returned to the caller).
        var employees = (await _service.Context.EmployeesForIntegrityChecks()
            .AsNoTracking()
            .Where(x => x.BioId.HasValue && bioIds.Contains(x.BioId.Value))
            .Select(x => new EmployeeRef(x.BioId!.Value, x.Id, x.BranchId, x.DepartmentId, x.ClientId, x.AreaId))
            .ToListAsync())
            .GroupBy(x => x.BioId)
            .ToDictionary(g => g.Key, g => g.First());

        var branchOfArea = await AttendanceLocationResolver.LoadBranchOfAreaAsync(_service.Context,
            messages.Select(x => x.OperationAreaId).Concat(employees.Values.Select(x => x.AreaId)), default);

        var atts = new List<Attendance>();
        foreach (var att in messages)
        {
            employees.TryGetValue(att.BioId, out EmployeeRef? employee);
            // The device's (or upload's) branch/client/site/department, blanks filled from the
            // employee's own -- an unregistered Bio ID keeps just the device's.
            var location = AttendanceLocationResolver.Resolve(
                new AttendanceLocation(att.BranchId, att.ClientId, att.OperationAreaId, att.DepartmentId),
                employee == null ? null : new AttendanceLocation(employee.BranchId, employee.ClientId, employee.AreaId, employee.DepartmentId),
                branchOfArea);
            var attendance = new Attendance
            {
                BatchCode = att.BatchId,
                BioId = att.BioId,
                WorkDateTime = att.WorkDateTime,
                EmployeeId = employee?.Id,
                BranchId = location.BranchId,
                DepartmentId = location.DepartmentId,
                ClientId = location.ClientId,
                OperationAreaId = location.OperationAreaId,
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

    private sealed record EmployeeRef(int BioId, Guid Id, Guid? BranchId, Guid? DepartmentId, Guid? ClientId, Guid? AreaId);
}