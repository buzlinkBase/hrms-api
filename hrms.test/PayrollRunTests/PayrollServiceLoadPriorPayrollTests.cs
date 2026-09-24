using Hrms.Core.Services;
using Hrms.Domain.Entities;
using hrms.test.TestSupport;
using MapsterMapper;
using NSubstitute;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// LoadPostedPayrollAsync feeds StatutoryHelper's Get*GrossBaseRate (via
/// CalculatorPayload.PostedPriorPayrolls) with "gross already earned earlier this month," which
/// SSSHelper/PHICHelper/HDMFHelper's GetBalance then nets against the SSS/PHIC/HDMF contribution
/// ledger (SSSContributionService.LoadContributionsAsync et al.). That ledger is written
/// unconditionally at Generate time and scoped by month/year only -- but this query used to also
/// require IsPosted, so an earlier-this-month draft (the normal state until someone approves it,
/// per the Payroll Posting Approval flow) was invisible here while already counted as posted in
/// the ledger. That mismatch made GetBalance net a small period-only bracket lookup against a
/// contribution it never actually saw the gross behind, flooring to zero instead of truing up.
/// This test locks in the fix: both posted and still-draft payrolls for the month must be
/// included.
/// </summary>
public class PayrollServiceLoadPriorPayrollTests
{
    [Fact]
    public async Task LoadPostedPayrollAsync_IncludesBothPostedAndDraftPayrolls_ForTheSameMonth()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Payrolls.Add(new Payroll
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeId,
                PayrollDate = new DateOnly(2026, 9, 15),
                GrossIncome = 12_055.50m,
                IsPosted = false,
            });
            seed.Payrolls.Add(new Payroll
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeId,
                PayrollDate = new DateOnly(2026, 9, 30),
                GrossIncome = 7_560.00m,
                IsPosted = true,
            });
            // A different month -- must not leak into the September lookup.
            seed.Payrolls.Add(new Payroll
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeId,
                PayrollDate = new DateOnly(2026, 8, 31),
                GrossIncome = 9_999.00m,
                IsPosted = true,
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = new PayrollService(uow, Substitute.For<IMapper>());

        var result = await service.LoadPostedPayrollAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 15), CancellationToken.None);

        var key = new EmployeeKey(employeeId);
        result.Should().ContainKey(key);
        result[key].Should().HaveCount(2, "both the still-draft and the posted September payroll must be included");
        result[key].Sum(x => x.GrossIncome).Should().Be(19_615.50m);
        result[key].Should().OnlyContain(x => x.PayrollDate.Month == 9 && x.PayrollDate.Year == 2026);
    }
}
