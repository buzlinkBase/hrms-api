using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.Invitations;

/// <summary>
/// Tells the Users page which employee or client each company member belongs to. An accepted
/// account is linked by its user id (Employee.UserId / ClientUserAccount.UserId); an invite not
/// yet accepted only has its email to go on. A client link always wins over an employee sharing
/// the same email, the same rule UserOnboardedWorker applies when an invite is accepted.
/// </summary>
public class UserLinkService
{
    private const int MaxKeys = 1000;

    private readonly IUnitOfWorkService _uow;

    public UserLinkService(IUnitOfWorkService uow) => _uow = uow;

    public async Task<List<UserLinkResult>> GetLinksAsync(UserLinkRequest request, CancellationToken token)
    {
        var userIds = request.UserIds.Where(x => x != Guid.Empty).Distinct().Take(MaxKeys).ToList();
        var emails = request.Emails.Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim().ToLowerInvariant())
            .Distinct()
            .Take(MaxKeys)
            .ToList();
        if (userIds.Count == 0 && emails.Count == 0) return [];

        var clients = await _uow.Repository.FindAll<ClientUserAccount>().AsNoTracking()
            .Where(x => userIds.Contains(x.UserId) || emails.Contains(x.Email))
            .Select(x => new UserLinkResult
            {
                UserId = x.UserId,
                Email = x.Email,
                Type = UserLinkType.Client,
                ClientId = x.ClientId,
                ClientCode = x.Client!.Code,
                ClientName = x.Client!.Name,
            })
            .ToListAsync(token);

        var employees = await _uow.Repository.FindAll<Employee>().AsNoTracking()
            .Where(x => (x.UserId != null && userIds.Contains(x.UserId.Value))
                || (x.Email != null && emails.Contains(x.Email.Trim().ToLower())))
            .Select(x => new UserLinkResult
            {
                UserId = x.UserId,
                Email = x.Email!.Trim().ToLower(),
                Type = UserLinkType.Employee,
                EmployeeId = x.Id,
                EmployeeNo = x.EmployeeNo,
                DepartmentName = x.Department != null ? x.Department.Name : null,
                Name = (x.FirstName + " " + x.LastName).Trim(),
            })
            .ToListAsync(token);

        // Ordered by precedence: the first source that knows a member decides what it is.
        IReadOnlyList<Func<Guid, UserLinkResult?>> byUserId =
        [
            id => clients.FirstOrDefault(x => x.UserId == id),
            id => employees.FirstOrDefault(x => x.UserId == id),
        ];
        IReadOnlyList<Func<string, UserLinkResult?>> byEmail =
        [
            email => clients.FirstOrDefault(x => string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase)),
            email => employees.FirstOrDefault(x => string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase)),
        ];

        var linked = userIds
            .Select(id => byUserId.Select(resolve => resolve(id)).FirstOrDefault(x => x != null))
            .OfType<UserLinkResult>()
            .ToList();

        // Emails only matter for members without an account yet; an email result never carries a
        // user id, so the page can't mistake it for an accepted account.
        var pending = emails
            .Select(email => byEmail.Select(resolve => resolve(email)).FirstOrDefault(x => x != null))
            .OfType<UserLinkResult>()
            .Select(x => new UserLinkResult
            {
                Email = x.Email.ToLowerInvariant(),
                Type = x.Type,
                EmployeeId = x.EmployeeId,
                EmployeeNo = x.EmployeeNo,
                DepartmentName = x.DepartmentName,
                ClientId = x.ClientId,
                ClientCode = x.ClientCode,
                ClientName = x.ClientName,
                Name = x.Name,
            })
            .ToList();

        return [.. linked, .. pending];
    }
}
