using Hrms.Core.Services;
using Hrms.Domain;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Domain.ValueObjects;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Onepunch.Common.Lib.DTO;

namespace hrms.test.ServiceTests;

/// <summary>
/// A resigned/terminated/retired/deceased employee must lose their login: on the change into a
/// separated status EmployeeService tells Tenant Service (EmployeeSeparated) to revoke the linked
/// account's membership. Only the change fires it, so access an admin re-grants by hand sticks.
/// </summary>
public class EmployeeSeparationAccessTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid Group = Guid.NewGuid();

    private static EmployeeService BuildService(IUnitOfWorkService uow, IPublishEndpoint publisher)
    {
        _ = TestMapping.GlobalSettings;
        return new EmployeeService(
            uow, Substitute.For<IMapper>(), TypeAdapterConfig.GlobalSettings,
            new DepartmentService(uow), new PayrollGroupService(uow), new BranchService(uow),
            new CostCenterService(uow), new PositionService(uow), new SectionService(uow),
            publisher: publisher);
    }

    private static async Task<Employee> SeedAsync(SqliteHrmsContext db, EmploymentStatus status, Guid? userId)
    {
        await using var seed = db.NewContext(CompanyId);
        var employee = new Employee
        {
            Id = Guid.NewGuid(), TenantId = CompanyId, PayrollGroupId = Group, FirstName = "Ana", LastName = "Reyes",
            EmploymentStatus = status, UserId = userId,
        };
        seed.PayrollGroups.Add(new PayrollGroup { Id = Group, TenantId = CompanyId });
        seed.Employees.Add(employee);
        await seed.SaveChangesAsync();
        return employee;
    }

    private static async Task UpdateStatusAsync(SqliteHrmsContext db, Guid employeeId, EmploymentStatus status, IPublishEndpoint publisher)
    {
        await using var context = db.NewContext(CompanyId);
        await BuildService(new UnitOfWorkService(context), publisher).UpdateAsync(new UpdateEmployee
        {
            Id = employeeId, PayrollGroupId = Group, FirstName = "Ana", LastName = "Reyes", EmploymentStatus = status,
        }, CancellationToken.None);
    }

    [Theory]
    [InlineData(EmploymentStatus.Resigned)]
    [InlineData(EmploymentStatus.Terminated)]
    [InlineData(EmploymentStatus.Retired)]
    [InlineData(EmploymentStatus.Deceased)]
    public async Task SeparatingAnEmployeeWithAnAccount_RevokesThatAccount(EmploymentStatus status)
    {
        using var db = new SqliteHrmsContext();
        var userId = Guid.NewGuid();
        var employee = await SeedAsync(db, EmploymentStatus.Regular, userId);
        var publisher = Substitute.For<IPublishEndpoint>();

        await UpdateStatusAsync(db, employee.Id, status, publisher);

        await publisher.Received(1).Publish(
            Arg.Is<EmployeeSeparated>(m => m.UserId == userId && m.TenantId == CompanyId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeparatingAnEmployeeWithNoAccount_PublishesNothing()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedAsync(db, EmploymentStatus.Regular, null);
        var publisher = Substitute.For<IPublishEndpoint>();

        await UpdateStatusAsync(db, employee.Id, EmploymentStatus.Resigned, publisher);

        await publisher.DidNotReceive().Publish(Arg.Any<EmployeeSeparated>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SavingAnAlreadySeparatedEmployee_DoesNotRevokeAgain()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedAsync(db, EmploymentStatus.Resigned, Guid.NewGuid());
        var publisher = Substitute.For<IPublishEndpoint>();

        await UpdateStatusAsync(db, employee.Id, EmploymentStatus.Resigned, publisher);

        await publisher.DidNotReceive().Publish(Arg.Any<EmployeeSeparated>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnActiveStatusChange_PublishesNothing()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedAsync(db, EmploymentStatus.Probationary, Guid.NewGuid());
        var publisher = Substitute.For<IPublishEndpoint>();

        await UpdateStatusAsync(db, employee.Id, EmploymentStatus.Regular, publisher);

        await publisher.DidNotReceive().Publish(Arg.Any<EmployeeSeparated>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnImportThatSeparatesAnEmployee_RevokesTheAccountToo()
    {
        using var db = new SqliteHrmsContext();
        var userId = Guid.NewGuid();
        var employee = await SeedAsync(db, EmploymentStatus.Regular, userId);
        var publisher = Substitute.For<IPublishEndpoint>();

        await using (var context = db.NewContext(CompanyId))
        {
            var tracked = await context.Employees.AsNoTracking().SingleAsync(x => x.Id == employee.Id);
            tracked.EmploymentStatus = EmploymentStatus.Terminated;
            await BuildService(new UnitOfWorkService(context), publisher)
                .AddOrUpdateRange([tracked], CancellationToken.None);
        }

        await publisher.Received(1).Publish(
            Arg.Is<EmployeeSeparated>(m => m.UserId == userId && m.TenantId == CompanyId),
            Arg.Any<CancellationToken>());
    }
}
