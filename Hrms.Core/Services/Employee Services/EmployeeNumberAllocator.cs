using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

/// <summary>
/// Hands out employee numbers for a company that turned Employee ID numbering on ("Auto"). With
/// numbering Off it does nothing, and the number stays whatever is typed.
///
/// The counter lives on the Company row and moves with a compare-and-swap UPDATE, so two people
/// adding employees at the same moment can never receive the same number. A number is spent when
/// it is handed out, even if the save that follows fails: gaps are fine, duplicates are not.
/// Numbers already used by an existing employee (typed by hand earlier) are skipped.
/// </summary>
public class EmployeeNumberAllocator
{
    private const int MaxAttempts = 8;

    private readonly IUnitOfWorkService _uow;
    private bool? _isAuto;

    public EmployeeNumberAllocator(IUnitOfWorkService uow) => _uow = uow;

    private HrmsContext Context => _uow.Context;

    /// <summary>Whether this company numbers its employees automatically. Read once per instance.</summary>
    public async Task<bool> IsAutoAsync(CancellationToken token = default)
    {
        // No database behind the unit of work (a service built over a mocked repository): nothing to number.
        if (_uow.Context is null) return false;

        _isAuto ??= EmployeeIdSettings.IsAuto(await Context.Companies
            .Select(c => c.EmployeeIdMode)
            .FirstOrDefaultAsync(token));
        return _isAuto.Value;
    }

    /// <summary>
    /// <paramref name="count"/> fresh, unused numbers, or an empty list when numbering is Off (or the
    /// company has no settings row yet).
    /// </summary>
    public async Task<List<string>> ReserveAsync(int count, CancellationToken token = default)
    {
        var numbers = new List<string>();
        if (count <= 0 || !await IsAutoAsync(token)) return numbers;

        var settings = await Context.Companies
            .Select(c => new { c.Id, c.EmployeeIdPrefix, c.EmployeeIdPadding, c.EmployeeIdIncludeYear })
            .FirstOrDefaultAsync(token);
        if (settings == null) return numbers;

        var year = PhTime.Today(DateTime.UtcNow).Year;
        while (numbers.Count < count)
        {
            var first = await ReserveRangeAsync(settings.Id, count - numbers.Count, token);
            var candidates = Enumerable.Range(first, count - numbers.Count)
                .Select(n => EmployeeIdSettings.Format(
                    settings.EmployeeIdPrefix, settings.EmployeeIdPadding, settings.EmployeeIdIncludeYear, year, n))
                .ToList();

            // Every employee counts, confidential ones included: a number must be unique company-wide.
            var taken = (await Context.EmployeesForIntegrityChecks()
                    .Where(e => candidates.Contains(e.EmployeeNo))
                    .Select(e => e.EmployeeNo)
                    .ToListAsync(token))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            numbers.AddRange(candidates.Where(c => !taken.Contains(c)));
        }
        return numbers;
    }

    public async Task<string?> ReserveOneAsync(CancellationToken token = default) =>
        (await ReserveAsync(1, token)).FirstOrDefault();

    // Moves the counter forward by count and returns the first number of the block it claimed.
    private async Task<int> ReserveRangeAsync(Guid companyId, int count, CancellationToken token)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var current = await Context.Companies
                .Where(c => c.Id == companyId)
                .Select(c => c.NextEmployeeNo)
                .FirstAsync(token);
            // A counter that was never set (0) starts at 1.
            var first = Math.Max(current, 1);

            var claimed = await Context.Companies
                .Where(c => c.Id == companyId && c.NextEmployeeNo == current)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.NextEmployeeNo, first + count), token);
            if (claimed == 1) return first;
        }
        throw new InvalidOperationException("Couldn't reserve an employee number right now. Please try again.");
    }
}
