using Hrms.Domain.Entities.EmployeeEntities;
using MassTransit;

namespace Hrms.Core.Messaging;

public class UserOnboardedWorker : IConsumer<UserOnboarded>
{
    private readonly EmployeeService _employeeService;

    public UserOnboardedWorker(EmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    public async Task Consume(ConsumeContext<UserOnboarded> context)
    {
        var msg = context.Message;
        Employee? employee = null;

        if (msg.EmployeeId.HasValue)
        {
            employee = await _employeeService.FineOneAsync(msg.EmployeeId.Value, context.CancellationToken);
        }
        if (employee == null)
        {
            employee = await _employeeService.FineOneByEmailAsync(msg.Email, context.CancellationToken);
        }

        if (employee == null)
        {
            Log.Warning("UserOnboardedWorker: Employee {EmployeeId} not found for user {UserId}", msg.EmployeeId, msg.UserId);
            return;
        }

        if (msg.UserId != employee.UserId)
        {
            Log.Warning("UserOnboardedWorker: different user inviting same employee/email emp:{0} , user1:{1} user2:{2} ,, fullName: {3}", msg.EmployeeId, employee.UserId, msg.UserId, employee.FullName());
            return;
        }

        await _employeeService.Context.Employees
             .Where(x => x.Id == employee.Id)
             .ExecuteUpdateAsync(x =>
              x.SetProperty(xx => xx.UserId, msg.UserId)
             .SetProperty(xx => xx.Email, msg.Email),
             context.CancellationToken);

        await _employeeService.CommitChangesAsync(context.CancellationToken);

    }
}
