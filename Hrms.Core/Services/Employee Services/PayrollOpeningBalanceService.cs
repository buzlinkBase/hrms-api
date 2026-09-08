using Hrms.Domain.Entities.EmployeeEntities;
using Mapster;

namespace Hrms.Core.Services;

public class PayrollOpeningBalanceService : BaseService<PayrollOpeningBalance>
{
    public PayrollOpeningBalanceService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public async Task AddAsync(PayrollOpeningBalance model, CancellationToken token)
    {
        ApplyComputedTotals(model);
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task UpdateAsync(UpdatePayrollOpeningBalance payload, CancellationToken token)
    {
        var existing = await Context.PayrollOpeningBalances.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        payload.Adapt(existing);
        ApplyComputedTotals(existing);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<PayrollOpeningBalance>> FindAllAsync(Guid empId, CancellationToken token)
    {
        return await GetQueryable()
            .Where(x => x.EmployeeId == empId)
            .OrderByDescending(x => x.Year)
            .ToListAsync(token);
    }

    // Consumed by PayrollReportService's YTD aggregations (GetYtdSummaryAsync/
    // GetThirteenthMonthAsync/GetAnnualTaxAnnualizationInputsAsync/GetAlphalistAsync/
    // Get2316DataAsync) to fold pre-cutover figures into each report's year sums.
    public async Task<Dictionary<Guid, PayrollOpeningBalance>> FindAllByYearAsync(int year, CancellationToken token)
    {
        var rows = await GetQueryable()
            .Where(x => x.Year == year)
            .ToListAsync(token);
        return rows.ToDictionary(x => x.EmployeeId);
    }

    public async Task<PayrollOpeningBalance?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }

    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }

    // GrossIncome/TotalDeductions/NetPay are never trusted from the client — always
    // recomputed from the entered components so they can never drift out of sync.
    // internal (not private) so it's directly unit-testable — see
    // PayrollOpeningBalanceServiceTests.
    internal static void ApplyComputedTotals(PayrollOpeningBalance model)
    {
        model.GrossIncome = model.BasicPay + model.OvertimePay + model.HolidayPay +
            model.Allowances + model.OtherIncome + model.Bonuses;
        model.TotalDeductions = model.SSSContribution + model.PhilHealthContribution +
            model.PagIbigContribution + model.WithholdingTax + model.OtherDeductions;
        model.NetPay = model.GrossIncome - model.TotalDeductions;
    }
}
