using Hrms.Core.Services.Storage;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Domain.ValueObjects;

namespace Hrms.Core.Services;

// Birthday greetings. Reads go through the normal (tenant- and confidentiality-filtered) queries,
// so a confidential employee the caller may not see never shows up. Only current employees count.
public class BirthdayService
{
    private readonly IUnitOfWorkService _uow;
    private readonly ImageUrlResolver _imageUrls;

    public BirthdayService(IUnitOfWorkService uow, ImageUrlResolver imageUrls)
    {
        _uow = uow;
        _imageUrls = imageUrls;
    }

    // Coworkers to greet today: only those who opted in.
    public async Task<List<CoworkerBirthdayModel>> GetTodayAsync(DateOnly today, CancellationToken token)
    {
        var rows = await _uow.Repository
            .Find<Employee>(e => e.DateResigned == null && e.ShareBirthday && e.DOB != null)
            .AsNoTracking()
            .Select(e => new
            {
                e.Id,
                e.FirstName,
                e.LastName,
                e.ProfileImg,
                Dob = e.DOB!.Value,
                Department = e.Department != null ? e.Department.Name : string.Empty,
                Position = e.Position != null ? e.Position.Name : string.Empty,
            })
            .ToListAsync(token);

        var celebrants = rows
            .Where(r => DaysUntilNextBirthday(DateOnly.FromDateTime(r.Dob), today) == 0)
            .OrderBy(r => r.FirstName).ThenBy(r => r.LastName)
            .ToList();

        var result = new List<CoworkerBirthdayModel>(celebrants.Count);
        foreach (var r in celebrants)
        {
            result.Add(new CoworkerBirthdayModel
            {
                Id = r.Id,
                FullName = $"{r.FirstName} {r.LastName}".Trim(),
                ProfileImg = await _imageUrls.DisplayUrlAsync(r.ProfileImg, token),
                DepartmentName = r.Department,
                PositionName = r.Position,
            });
        }
        return result;
    }

    // For HR: everyone with a birthday in the next `days` days (today included), nearest first.
    public async Task<List<UpcomingBirthdayModel>> GetUpcomingAsync(DateOnly today, int days, int limit, CancellationToken token)
    {
        var rows = await _uow.Repository
            .Find<Employee>(e => e.DateResigned == null && e.DOB != null)
            .AsNoTracking()
            .Select(e => new
            {
                e.Id,
                e.FirstName,
                e.LastName,
                e.ShareBirthday,
                Dob = e.DOB!.Value,
                Department = e.Department != null ? e.Department.Name : string.Empty,
            })
            .ToListAsync(token);

        return rows
            .Select(r =>
            {
                var dob = DateOnly.FromDateTime(r.Dob);
                return new UpcomingBirthdayModel
                {
                    Id = r.Id,
                    FullName = $"{r.FirstName} {r.LastName}".Trim(),
                    DepartmentName = r.Department,
                    Month = dob.Month,
                    Day = dob.Day,
                    DaysUntil = DaysUntilNextBirthday(dob, today),
                    ShareBirthday = r.ShareBirthday,
                };
            })
            .Where(m => m.DaysUntil <= days)
            .OrderBy(m => m.DaysUntil).ThenBy(m => m.FullName)
            .Take(limit)
            .ToList();
    }

    // Employee-controlled opt-in; touches only the given employee's own row.
    public async Task<bool> SetShareAsync(Guid employeeId, bool share, CancellationToken token)
    {
        var employee = await _uow.Repository.Find<Employee>(e => e.Id == employeeId).FirstOrDefaultAsync(token);
        if (employee == null) return false;
        employee.ShareBirthday = share;
        // The unit of work opens a transaction on creation; only CommitChangesAsync commits it.
        await _uow.CommitChangesAsync("", token);
        return true;
    }

    // Whole days from `today` to the next occurrence of the birthday (0 = today). A Feb 29
    // birthday is observed on Feb 28 in non-leap years.
    public static int DaysUntilNextBirthday(DateOnly dob, DateOnly today)
    {
        var next = Occurrence(dob, today.Year);
        if (next < today) next = Occurrence(dob, today.Year + 1);
        return next.DayNumber - today.DayNumber;
    }

    private static DateOnly Occurrence(DateOnly dob, int year) =>
        dob.Month == 2 && dob.Day == 29 && !DateTime.IsLeapYear(year)
            ? new DateOnly(year, 2, 28)
            : new DateOnly(year, dob.Month, dob.Day);
}
