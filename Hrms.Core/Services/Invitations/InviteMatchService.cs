using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.Invitations;

/// <summary>
/// Ties the emails in a batch invitation to this company's records: each employee invite needs
/// the employee the email belongs to, a client login must not be an employee's email, and nobody
/// already linked to an account is invited again. Also records a client login once its invite is
/// accepted. Reads through the normal (confidentiality-filtered) queries, so an employee the
/// caller can't see reads as no match.
/// </summary>
public class InviteMatchService
{
    private readonly IUnitOfWorkService _uow;

    public InviteMatchService(IUnitOfWorkService uow) => _uow = uow;

    private static List<string> Normalize(IEnumerable<string> emails) =>
        emails.Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim().ToLowerInvariant())
            .Distinct()
            .Take(100)
            .ToList();

    public async Task<List<InviteMatchResult>> MatchEmployeesAsync(IEnumerable<string> emails, CancellationToken token)
    {
        var wanted = Normalize(emails);
        var employees = await _uow.Repository.FindAll<Employee>().AsNoTracking()
            .Where(x => x.Email != null && wanted.Contains(x.Email.Trim().ToLower()))
            .Select(x => new { x.Id, x.Email, x.UserId, x.FirstName, x.LastName })
            .ToListAsync(token);
        var clientLogins = await ClientLoginEmailsAsync(wanted, token);

        return wanted.Select(email =>
        {
            if (clientLogins.Contains(email))
                return new InviteMatchResult { Email = email, Status = InviteMatchStatus.ClientLogin };

            var employee = employees.FirstOrDefault(x => string.Equals(x.Email!.Trim(), email, StringComparison.OrdinalIgnoreCase));
            if (employee == null)
                return new InviteMatchResult { Email = email, Status = InviteMatchStatus.NoEmployee };

            return new InviteMatchResult
            {
                Email = email,
                Status = employee.UserId == null ? InviteMatchStatus.Matched : InviteMatchStatus.AlreadyLinked,
                EmployeeId = employee.Id,
                Name = $"{employee.FirstName} {employee.LastName}".Trim(),
            };
        }).ToList();
    }

    public async Task<List<InviteMatchResult>> MatchClientLoginsAsync(Guid clientId, IEnumerable<string> emails, CancellationToken token)
    {
        var wanted = Normalize(emails);
        var employees = await _uow.Repository.FindAll<Employee>().AsNoTracking()
            .Where(x => x.Email != null && wanted.Contains(x.Email.Trim().ToLower()))
            .Select(x => new { x.Email, x.FirstName, x.LastName })
            .ToListAsync(token);
        var clientLogins = await ClientLoginEmailsAsync(wanted, token);

        return wanted.Select(email =>
        {
            var employee = employees.FirstOrDefault(x => string.Equals(x.Email!.Trim(), email, StringComparison.OrdinalIgnoreCase));
            if (employee != null)
                return new InviteMatchResult
                {
                    Email = email,
                    Status = InviteMatchStatus.EmployeeEmail,
                    Name = $"{employee.FirstName} {employee.LastName}".Trim(),
                };
            return new InviteMatchResult
            {
                Email = email,
                Status = clientLogins.Contains(email) ? InviteMatchStatus.AlreadyLinked : InviteMatchStatus.Ok,
            };
        }).ToList();
    }

    /// <summary>
    /// Records the accepted client invite's account as a login of that client. Idempotent: an
    /// account already recorded (for any client) is left as it is.
    /// </summary>
    public async Task LinkClientLoginAsync(Guid clientId, Guid userId, string email, CancellationToken token)
    {
        if (await _uow.Repository.FindAll<ClientUserAccount>().AnyAsync(x => x.UserId == userId, token)) return;
        if (!await _uow.Repository.FindAll<Client>().AnyAsync(x => x.Id == clientId, token))
        {
            Log.Warning("InviteMatchService: client {ClientId} not found for user {UserId}", clientId, userId);
            return;
        }

        await _uow.Repository.AddAsync(new ClientUserAccount
        {
            ClientId = clientId,
            UserId = userId,
            Email = email.Trim().ToLowerInvariant(),
        }, token);
        await _uow.CommitChangesAsync("", token);
    }

    private async Task<HashSet<string>> ClientLoginEmailsAsync(List<string> wanted, CancellationToken token) =>
        (await _uow.Repository.FindAll<ClientUserAccount>().AsNoTracking()
            .Where(x => wanted.Contains(x.Email))
            .Select(x => x.Email)
            .ToListAsync(token))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
