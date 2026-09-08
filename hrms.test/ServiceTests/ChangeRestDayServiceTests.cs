using System.Linq.Expressions;
using Hrms.Domain.Entities;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// ChangeRestDayService.GetChangeRestDays — the sole query the DTR RestDayResolver reads
/// through (OverrideDayOffHandler/FallBackDayOffHandler). This is the actual gate that makes
/// self-service Change Rest Day requests safe: a ForApproval or Declined row must never affect
/// DTR calculation, only Approved ones. AddChangeOff/RequestChangeOffAsync themselves aren't
/// unit-tested here — they route through Context.ChangeRestDays (a real EF DbContext query for
/// batch-code counting), which needs a real database provider rather than the IRepository
/// mocking used elsewhere in this project.
/// </summary>
public class ChangeRestDayServiceTests
{
    private static ChangeRestDay BuildRow(Guid employeeId, DateOnly date, ApprovalStatus status) => new()
    {
        Id = Guid.NewGuid(),
        EmployeeId = employeeId,
        PayrollDate = date,
        DayName = DayName.Sunday,
        State = ChangeSchedState.REPLACEMENT,
        BatchCode = "COFF-TEST",
        ApprovalStatus = status,
    };

    private static ChangeRestDayService BuildService(params ChangeRestDay[] rows)
    {
        var repo = Substitute.For<IRepository>();
        repo.Find<ChangeRestDay>(Arg.Any<Expression<Func<ChangeRestDay, bool>>>())
            .Returns(call => rows.Where(call.Arg<Expression<Func<ChangeRestDay, bool>>>().Compile()).ToList().BuildMockDbSet());

        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);

        return new ChangeRestDayService(uow);
    }

    [Fact]
    public async Task GetChangeRestDays_OnlyReturnsApprovedRows()
    {
        var employeeId = Guid.NewGuid();
        var forApprovalDate = new DateOnly(2026, 9, 6);
        var approvedDate = new DateOnly(2026, 9, 13);
        var declinedDate = new DateOnly(2026, 9, 20);
        var service = BuildService(
            BuildRow(employeeId, forApprovalDate, ApprovalStatus.ForApproval),
            BuildRow(employeeId, approvedDate, ApprovalStatus.Approved),
            BuildRow(employeeId, declinedDate, ApprovalStatus.Declined));

        var result = await service.GetChangeRestDays(
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), [employeeId], CancellationToken.None);

        result.Should().ContainSingle();
        result.Keys.Should().ContainSingle(k => k.RestDay == approvedDate);
    }

    [Fact]
    public async Task GetChangeRestDays_ReturnsEmpty_WhenNoRowsApproved()
    {
        var employeeId = Guid.NewGuid();
        var service = BuildService(
            BuildRow(employeeId, new DateOnly(2026, 9, 6), ApprovalStatus.ForApproval),
            BuildRow(employeeId, new DateOnly(2026, 9, 13), ApprovalStatus.Declined));

        var result = await service.GetChangeRestDays(
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), [employeeId], CancellationToken.None);

        result.Should().BeEmpty();
    }
}
