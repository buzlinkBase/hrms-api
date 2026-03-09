using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace Hrms.Api.Controllers.Adms;

public interface ICDataProcessor
{
    Task ProcessAsync(BioPayload payload, CancellationToken token = default);
}
public class AttLogTableProcessor : AttendanceService, ICDataProcessor
{
    private readonly ITenantProvider _provider;
    private readonly EmployeeService _employeeService;
    public AttLogTableProcessor(IUnitOfWorkService uow,
        ITenantProvider provider,
        EmployeeService employeeService) : base(uow)
    {
        _provider = provider;
        _employeeService = employeeService;
    }
    public async Task ProcessAsync(BioPayload payload, CancellationToken token = default)
    {
        var atts = new List<Attendance>();
        var employees = await _employeeService
            .GetQueryable()
            .Where(x => x.BioId != 0 && x.TenantId == _provider.TenantId)
            .Select(x => new { x.Id, x.BioId })
            .GroupBy(x => x.BioId)
            .ToDictionaryAsync(x => x.Key, x => x.Select(x => x.Id).First(), token)
        ;
        var lines = payload.RawData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var fields = line.Split('\t');
            if (fields.Length >= 2)
            {
                var bioId = int.TryParse(fields[0], out int id) ? id : 0;
                if (!employees.TryGetValue(bioId, out Guid empId)) continue;
                var attendance = new Attendance()
                {
                    BioId = bioId,
                    WorkDateTime = DateTime.TryParse(fields[1], out DateTime dt) ? dt : DateTime.Now,
                    EmployeeId = empId,
                    TenantId=_provider.TenantId
                };
                atts.Add(attendance);
            }
        }

        if (!atts.Any()) return;
        await AddRangeAsync(atts, token);
        await CommitChangesAsync(token);

    }
}
