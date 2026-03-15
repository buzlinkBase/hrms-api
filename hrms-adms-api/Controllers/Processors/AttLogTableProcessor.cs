using Hrms.adms.api.Controllers;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.EntityFrameworkCore;
namespace Hrms.adms.api.Controllers.Processors;

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
            .ToDictionaryAsync(x=>x.BioId,x=>x);

        var lines = payload.RawData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var batch= Guid.NewGuid().ToString();   
        foreach (var line in lines)
        {
            var fields = line.Split('\t');
            if (fields.Length >= 2)
            {
                var bioId = int.TryParse(fields[0], out int id) ? id : 0;
                Guid? employeeId = null;
                if (employees.TryGetValue(bioId, out Employee? emp   ))
                {
                    employeeId = emp.Id;
                }
                var attendance = new Attendance()
                {
                    BioId = bioId,
                    WorkDateTime = DateTime.TryParse(fields[1], out DateTime dt) ? dt : DateTime.Now,
                    EmployeeId = employeeId ,
                    TenantId = _provider.TenantId,
                    BranchId  = emp?.BranchId,
                    ClientId = emp?.ClientId,
                    DepartmentId = emp?.DepartmentId,
                    BatchCode= batch,
                    DeviceName = payload.SN,
                    LogSource = LOGSOURCE.ADMS
                };
                atts.Add(attendance);
            }
        }

        if (!atts.Any()) return;
        await AddRangeAsync(atts, token);
        await CommitChangesAsync(token);

    }
}
