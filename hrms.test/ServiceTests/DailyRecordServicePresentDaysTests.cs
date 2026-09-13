using Hrms.Core.Services;
using Hrms.Domain.Entities;
using Mapster;
using MapsterMapper;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// DailyRecordService.CountPresentDaysAsync/CountPresentDaysBatchAsync — the query backing
/// Leave.EligibilityBasis.PresentDays. "Present" here means every posted DailyRecord row that
/// isn't Absent/Incomplete/Skipped, so holidays and rest days (worked or not) count.
/// </summary>
public class DailyRecordServicePresentDaysTests
{
    private static DailyRecord Day(Guid employeeId, int day, WorkType type, bool posted = true) => new()
    {
        Id = Guid.NewGuid(),
        EmployeeId = employeeId,
        WorkDate = new DateOnly(2026, 1, day),
        WorkTypeEnum = type,
        Posted = posted,
    };

    private static DailyRecordService BuildService(params DailyRecord[] records)
    {
        var repo = Substitute.For<IRepository>();
        repo.FindAll<DailyRecord>().Returns(_ => records.ToList().BuildMockDbSet());

        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);

        return new DailyRecordService(
            uow,
            new TypeAdapterConfig(),
            Substitute.For<IMapper>(),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<DailyRecordService>>(),
            new LeaveDtrReconciliationService(uow, Substitute.For<Microsoft.Extensions.Logging.ILogger<LeaveDtrReconciliationService>>()),
            new PayrollBatchService(uow));
    }

    [Fact]
    public async Task CountPresentDaysAsync_ExcludesAbsentIncompleteAndSkipped()
    {
        var employeeId = Guid.NewGuid();
        var service = BuildService(
            Day(employeeId, 5, WorkType.RegularWorkDay),
            Day(employeeId, 6, WorkType.RestDay),
            Day(employeeId, 7, WorkType.LegalHoliday),
            Day(employeeId, 8, WorkType.Absent),
            Day(employeeId, 9, WorkType.Incomplete),
            Day(employeeId, 10, WorkType.Skipped));

        var count = await service.CountPresentDaysAsync(employeeId, new DateOnly(2026, 1, 31), CancellationToken.None);

        count.Should().Be(3); // RegularWorkDay + RestDay + LegalHoliday only
    }

    [Fact]
    public async Task CountPresentDaysAsync_ExcludesUnpostedAndOutOfRangeDays()
    {
        var employeeId = Guid.NewGuid();
        var service = BuildService(
            Day(employeeId, 5, WorkType.RegularWorkDay, posted: false), // not yet posted
            Day(employeeId, 20, WorkType.RegularWorkDay));              // after the cutoff below

        var count = await service.CountPresentDaysAsync(employeeId, new DateOnly(2026, 1, 10), CancellationToken.None);

        count.Should().Be(0);
    }

    [Fact]
    public async Task CountPresentDaysBatchAsync_GroupsCountsPerEmployee()
    {
        var empA = Guid.NewGuid();
        var empB = Guid.NewGuid();
        var service = BuildService(
            Day(empA, 5, WorkType.RegularWorkDay),
            Day(empA, 6, WorkType.RestDay),
            Day(empA, 7, WorkType.Absent),
            Day(empB, 5, WorkType.RegularWorkDay));

        var counts = await service.CountPresentDaysBatchAsync([empA, empB], new DateOnly(2026, 1, 31), CancellationToken.None);

        counts[empA].Should().Be(2);
        counts[empB].Should().Be(1);
    }

    [Fact]
    public async Task CountPresentDaysBatchAsync_EmptyEmployeeList_ReturnsEmptyWithoutQuerying()
    {
        var service = BuildService();

        var counts = await service.CountPresentDaysBatchAsync([], new DateOnly(2026, 1, 31), CancellationToken.None);

        counts.Should().BeEmpty();
    }
}
