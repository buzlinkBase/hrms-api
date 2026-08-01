using MassTransit;
using Serilog;

namespace Hrms.Core.Messaging;

/// <summary>
/// Flow B (Invited User Onboarding): consumes UserJoinToTenantPayload, the wire realization of
/// the design doc's AccountLinked event, once a user's account is linked to a tenant on the
/// identity-platform side.
///
/// Deliberately does NOT fabricate an Employee row here: Employee requires real HR business
/// data (PayrollGroupId, HireDate, compensation, etc.) that this event doesn't carry and that
/// shouldn't be invented — doing so would create an invalid/incomplete payroll record. This is
/// the extension point for a future "pending onboarding" workflow (e.g. surfacing linked-but-
/// not-yet-onboarded accounts to HR staff) once the product defines that mapping.
/// </summary>
public class AccountLinkedWorker : IConsumer<UserJoinToTenantPayload>
{
    public Task Consume(ConsumeContext<UserJoinToTenantPayload> context)
    {
        var message = context.Message;
        //Log.Logger.Information(
        //    "AccountLinkedWorker: user {UserId} linked to tenant {TenantId} ({TenantName}) with roles {Roles} — awaiting HR onboarding",
        //    message.UserId, message.TenantId, message.TenantName, string.Join(", ", message.Roles));
        return Task.CompletedTask;

    }
}
