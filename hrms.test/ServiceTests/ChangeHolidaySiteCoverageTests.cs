using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Microsoft.EntityFrameworkCore;
using Onepunch.Common.Lib.Exceptions;

namespace hrms.test.ServiceTests;

/// <summary>
/// Change Holiday on a localized special holiday: only employees assigned to one of the
/// holiday's project sites can have it moved. Nationwide and legal holidays are unaffected.
/// </summary>
public class ChangeHolidaySiteCoverageTests
{
    private static readonly DateOnly Day = new(2026, 9, 9);
    private static readonly Guid Cebu = Guid.NewGuid();
    private static readonly Guid Manila = Guid.NewGuid();

    private sealed record Seeded(SqliteHrmsContext Db, Guid CebuEmployee, Guid ManilaEmployee, Guid Localized, Guid Nationwide);

    private static async Task<Seeded> SeedAsync()
    {
        var db = new SqliteHrmsContext();
        await using var seed = db.NewContext();
        seed.Areas.AddRange(
            new CostCenters { Id = Cebu, Code = "6000", Name = "Cebu City Area" },
            new CostCenters { Id = Manila, Code = "1000", Name = "Manila Area" });
        var payrollGroupId = Guid.NewGuid();
        seed.PayrollGroups.Add(new PayrollGroup { Id = payrollGroupId });
        var cebuEmployee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = payrollGroupId, LastName = "Reyes", FirstName = "Ana", AreaId = Cebu };
        var manilaEmployee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = payrollGroupId, LastName = "Cruz", FirstName = "Ben", AreaId = Manila };
        seed.Employees.AddRange(cebuEmployee, manilaEmployee);

        var localized = new Holiday { Id = Guid.NewGuid(), Description = "Osmeña Day", HolType = HolidayType.SPECIAL, HolDate = Day, HolYear = 2026 };
        localized.Areas.Add(new HolidayArea { AreaId = Cebu });
        var nationwide = new Holiday { Id = Guid.NewGuid(), Description = "Ninoy Aquino Day", HolType = HolidayType.SPECIAL, HolDate = Day, HolYear = 2026 };
        seed.Holidays.AddRange(localized, nationwide);
        await seed.SaveChangesAsync();
        return new(db, cebuEmployee.Id, manilaEmployee.Id, localized.Id, nationwide.Id);
    }

    private static CreateChangeHoliday Move(Guid holidayId, params Guid[] employeeIds) => new()
    {
        HolidayId = holidayId,
        PayrollDateFrom = Day,
        PayrollDateTo = Day.AddDays(3),
        EmployeeIds = employeeIds,
    };

    private static async Task<int> RowsAsync(SqliteHrmsContext db)
    {
        await using var read = db.NewContext();
        return await read.ChangeHolidays.CountAsync();
    }

    [Fact]
    public async Task LocalizedHoliday_EmployeeAtACoveredSite_IsSaved()
    {
        var s = await SeedAsync();
        using var _ = s.Db;
        await using (var context = s.Db.NewProductionLikeContext())
        {
            await new ChangeHolidayService(new UnitOfWorkService(context)).AddAsync(Move(s.Localized, s.CebuEmployee), default);
        }

        (await RowsAsync(s.Db)).Should().Be(2, "one override on the holiday date, one replacement");
    }

    [Fact]
    public async Task LocalizedHoliday_EmployeeOutsideItsSites_IsRejectedByName_AndNothingIsSaved()
    {
        var s = await SeedAsync();
        using var _ = s.Db;
        await using (var context = s.Db.NewProductionLikeContext())
        {
            var act = () => new ChangeHolidayService(new UnitOfWorkService(context))
                .AddAsync(Move(s.Localized, s.CebuEmployee, s.ManilaEmployee), default);

            (await act.Should().ThrowAsync<GuardException>()).Which.Message.Should().Contain("Cruz, Ben").And.NotContain("Reyes");
        }

        (await RowsAsync(s.Db)).Should().Be(0);
    }

    [Fact]
    public async Task NationwideHoliday_AnyEmployee_IsSaved()
    {
        var s = await SeedAsync();
        using var _ = s.Db;
        await using (var context = s.Db.NewProductionLikeContext())
        {
            await new ChangeHolidayService(new UnitOfWorkService(context)).AddAsync(Move(s.Nationwide, s.CebuEmployee, s.ManilaEmployee), default);
        }

        (await RowsAsync(s.Db)).Should().Be(4);
    }
}
