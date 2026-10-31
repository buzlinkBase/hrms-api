using Hrms.Domain;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Domain.ValueObjects;

namespace Hrms.Core.Services.Compliance;

// One employee's pay setup to check: what the save sends, plus where they work (the branch's
// region and wage-order class decide the minimum wage).
public sealed record EmployeePayCheck(
    Guid? BranchId,
    string? BranchCode,
    decimal DailyRate,
    ComputationBasis? Sss = null,
    ComputationBasis? PhilHealth = null,
    ComputationBasis? PagIbig = null,
    ComputationBasis? WithholdingTax = null,
    EmployeeEligibility? Eligibility = null)
{
    public static EmployeePayCheck From(CreateEmployee employee) => new(
        employee.BranchId,
        null,
        employee.DailyRate,
        employee.SSSRate?.ComputationType,
        employee.PHICRate?.ComputationType,
        employee.HDMFRate?.ComputationType,
        employee.TaxRate?.ComputationType,
        From(employee.Settings));

    public static EmployeeEligibility? From(CreateEmployeeSetting? settings) => settings is null
        ? null
        : new EmployeeEligibility(
            settings.IsEligibleForOvertime,
            settings.IsEligibleForRegularHolidayPay,
            settings.IsEligibleForNightDifferential,
            settings.IsEligibleForLeaveCredits,
            settings.IsEligibleFor13thMonth);
}

// Employee saves (single, settings-only or a whole upload) whose pay setup differs from the statutory
// defaults: resolves each one's regional minimum wage, runs the EmployeePay rules, and logs each
// deviating setting for the company (ComplianceAcknowledgmentService).
public class EmployeePayDeviationService : BaseService<Employee>
{
    private readonly ComplianceAcknowledgmentService _compliance;

    public EmployeePayDeviationService(IUnitOfWorkService uow, ComplianceAcknowledgmentService compliance) : base(uow)
    {
        _compliance = compliance;
    }

    public async Task RecordDeviationsAsync(
        IReadOnlyCollection<EmployeePayCheck> employees,
        ComplianceActor actor,
        CancellationToken token = default)
    {
        if (employees.Count == 0) return;
        var deviations = StatutoryDeviationRules.Distinct(
            (await SubjectsAsync(employees, token)).SelectMany(StatutoryDeviationRules.Evaluate));
        await _compliance.RecordDeviationsAsync(
            ComplianceAcknowledgmentService.CompanyScope, deviations, actor, token);
    }

    // One subject per employee, in the order given (OutstandingDeviationService pairs them back up).
    internal async Task<List<EmployeePaySubject>> SubjectsAsync(
        IReadOnlyCollection<EmployeePayCheck> employees, CancellationToken token)
    {
        var branchIds = employees.Where(e => e.BranchId != null).Select(e => e.BranchId!.Value).Distinct().ToList();
        var branchCodes = employees.Where(e => !string.IsNullOrWhiteSpace(e.BranchCode)).Select(e => e.BranchCode!.Trim()).Distinct().ToList();
        var branches = await Context.Set<Branch>().AsNoTracking()
            .Where(b => branchIds.Contains(b.Id) || branchCodes.Contains(b.Code))
            .Select(b => new { b.Id, b.Code, b.RegionCode, b.WageOrderClass })
            .ToListAsync(token);
        List<MinimumWageRate> wageRates = branches.Any(b => !string.IsNullOrEmpty(b.RegionCode))
            ? await Context.Set<MinimumWageRate>().AsNoTracking().ToListAsync(token)
            : [];
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8)); // Manila

        return employees.Select(e =>
        {
            var branch = branches.FirstOrDefault(b => b.Id == e.BranchId)
                ?? branches.FirstOrDefault(b => string.Equals(b.Code, e.BranchCode?.Trim(), StringComparison.OrdinalIgnoreCase));
            var minimum = branch is null
                ? null
                : MinimumWageEarnerResolver.ResolveRegionRate(wageRates, branch.RegionCode, branch.WageOrderClass, today);
            return new EmployeePaySubject(
                e.Sss, e.PhilHealth, e.PagIbig, e.WithholdingTax, e.Eligibility, e.DailyRate, minimum);
        }).ToList();
    }
}
