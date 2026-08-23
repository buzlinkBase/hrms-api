
using Hrms.Domain.Entities;

namespace Hrms.Domain.ValueObjects;

public class DailyRecordRunModel : DailyRecord
{
    // Shadows the base entity's Employee (Entities.Employee) navigation property with the
    // payroll-run DTO shape. Mapster's registered Employee -> EmployeeModelPayrollRun config
    // (MappingProfile.cs) projects this automatically via ProjectToType<DailyRecordRunModel>.
    public new EmployeeModelPayrollRun? Employee { get; set; }
}