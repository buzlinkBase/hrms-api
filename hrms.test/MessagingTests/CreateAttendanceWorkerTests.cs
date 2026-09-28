using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using MassTransit;
using NSubstitute;
using Onepunch.Common.Lib.DTO;
using OnePunch.Auth.Core.Messaging;

namespace hrms.test.MessagingTests;

/// <summary>
/// CreateAttendanceWorker / AttEmployeeSetter: the Bio ID -> employee lookup is an untracked
/// projection (not tracked lazy-loading Employee proxies held for the whole batch), and an empty
/// batch is a no-op instead of throwing on messages[0].
/// </summary>
public class CreateAttendanceWorkerTests
{
    [Fact]
    public async Task EmptyBatch_DoesNothing()
    {
        var publish = Substitute.For<IPublishEndpoint>();
        var worker = new CreateAttendanceWorker(null!, null!, publish);
        var context = Substitute.For<ConsumeContext<AttendancePayloadWrapper>>();
        context.Message.Returns(new AttendancePayloadWrapper { AttLogs = [] });

        await worker.Consume(context);

        await publish.DidNotReceiveWithAnyArgs().Publish<BatchAttConfirmation>(default!);
    }

    [Fact]
    public async Task ParseAttLogs_StampsRegisteredEmployees_AndFlagsUnknownBioIds()
    {
        using var db = new SqliteHrmsContext();
        var branchId = Guid.NewGuid();
        Employee employee;
        await using (var seed = db.NewContext())
        {
            var payrollGroupId = Guid.NewGuid();
            seed.PayrollGroups.Add(new PayrollGroup { Id = payrollGroupId });
            employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = payrollGroupId, BioId = 101, BranchId = null };
            seed.Employees.Add(employee);
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var setter = new AttEmployeeSetter(new UnitOfWorkService(context));
        var when = new DateTime(2026, 9, 28, 8, 0, 0);

        var atts = await setter.ParseAttLogs(
        [
            new CreateAttendancePayload { BatchId = "b1", BioId = 101, WorkDateTime = when, BranchId = branchId },
            new CreateAttendancePayload { BatchId = "b1", BioId = 999, WorkDateTime = when },
        ], LOGSOURCE.SYNC);

        atts.Should().HaveCount(2);
        atts[0].EmployeeId.Should().Be(employee.Id);
        atts[0].BranchId.Should().Be(branchId, "the log's own branch wins over the employee's");
        atts[0].EditRemarks.Should().BeEmpty();
        atts[1].EmployeeId.Should().BeNull();
        atts[1].EditRemarks.Should().Be("Unregistered Employee Bio Id");

        context.ChangeTracker.Entries<Employee>().Should().BeEmpty("the lookup must not track employees");
    }
}
