using Hrms.Domain.Entities.EmployeeEntities;

namespace DTR.Core;

public class EmployeePolicyRule
{
    public EmployeePolicyKey Key { get; set; }
    //public OvertimeEligibilityRule? OvertimeEligibilityRule { get; set; }
    //public OvertimeInclusionPolicy? OvertimeInclusionPolicy { get; set; }

}

public record struct EmployeePolicyKey(Guid EmployeeId);