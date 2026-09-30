using Hrms.Domain.Entities.EmployeeEntities;
using MassTransit;

namespace Hrms.Core.Messaging;

public class UserOnboardedWorker : IConsumer<UserOnboarded>
{
    private readonly EmployeeService _employeeService;
    private readonly Services.Invitations.InviteMatchService _inviteMatch;

    public UserOnboardedWorker(EmployeeService employeeService, Services.Invitations.InviteMatchService inviteMatch)
    {
        _employeeService = employeeService;
        _inviteMatch = inviteMatch;
    }

    public async Task Consume(ConsumeContext<UserOnboarded> context)
    {
        var msg = context.Message;

        // A client invite links the account to its client, never to an employee who happens to
        // share the email.
        if (msg.ClientId is { } clientId && clientId != Guid.Empty)
        {
            await _inviteMatch.LinkClientLoginAsync(clientId, msg.UserId, msg.Email, context.CancellationToken);
            return;
        }

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

        // Only an employee already linked to a DIFFERENT account is left alone. This used to be
        // `msg.UserId != employee.UserId`, which is also true for a not-yet-linked employee
        // (UserId null) -- the normal case -- so no invitation ever actually linked anyone.
        if (employee.UserId != null && employee.UserId != msg.UserId)
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
