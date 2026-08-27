namespace Hrms.Core.Policies.OtherIncome;

internal class ScheduledIncomePolicy : PayrollPolicyBase<AllowancePipeData, PayrollContext>
{
    public override AllowancePipeData ApplyIfSatisfied(AllowancePipeData line, PayrollContext context)
    {
        // Get all scheduled incomes for this employee
        if (!context.Payload.Incomes.TryGetValue(new EmployeeKey(context.Employee.Id), out var incomes)
            || incomes == null || !incomes.Any())
        {
            // no incomes, return unchanged line
            return line;
        }
        var effectiveIncomes = incomes;
        if (!effectiveIncomes.Any()) return line;

        // Assign grouped incomes
        line.AllIncome = effectiveIncomes;
        line.RegularAllowances = effectiveIncomes.Where(x => x.Type.HasValue && x.Type == IncomeClassType.Regular).ToList();
        line.Deminimises = effectiveIncomes.Where(x => x.Type.HasValue && x.Type == IncomeClassType.Deminimis).ToList();
        line.Commissions = effectiveIncomes.Where(x => x.Type.HasValue && x.Type == IncomeClassType.Commission).ToList();
        line.Bonuses = effectiveIncomes.Where(x => x.Type.HasValue && x.Type == IncomeClassType.SpecialBonus).ToList();
        line.Reimbursements = effectiveIncomes.Where(x => x.Type.HasValue && x.Type == IncomeClassType.Reimbursement).ToList();
        line.OtherIncome = effectiveIncomes.Where(x => x.Type.HasValue && x.Type == IncomeClassType.Others).ToList();

        //TODO capture saved prorated allowance from db to be included for the current month for SSS computation
        line.ProratedAllowances = context.Payload.ProratedAllowance
                                .Where(x => x.EmployeeId == context.Employee.Id)
                                .ToList();

        // Update running total
        line.RunningTotal += effectiveIncomes.Sum(x => x.Amount);
        return line;

    }
}

