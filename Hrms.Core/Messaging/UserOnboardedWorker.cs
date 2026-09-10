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
        if (!msg.EmployeeId.HasValue) return;

        var employee = await _employeeService.FineOneAsync(msg.EmployeeId.Value, context.CancellationToken);
        if (employee == null)
        {
            Log.Warning("UserOnboardedWorker: Employee {EmployeeId} not found for user {UserId}", msg.EmployeeId, msg.UserId);
            return;
        }

        await _employeeService.Context.Employees
             .Where(x => x.Id == msg.EmployeeId.Value)
             .ExecuteUpdateAsync(x =>
              x.SetProperty(xx => xx.UserId, msg.UserId)
             .SetProperty(xx => xx.Email, msg.Email),
             context.CancellationToken);

        await _employeeService.CommitChangesAsync(context.CancellationToken);
    }
}
