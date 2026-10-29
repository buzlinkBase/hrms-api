using BuzlinkRepository;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;

// A project site a supervisor may schedule: with Work Rotation:ManageOwnTeam they can then enter
// schedules for employees assigned to that site (on top of their direct reports) and may only
// send people to their granted sites. A supervisor with no rows keeps the direct-reports-only rule.
[DisableSoftDelete]
public class SupervisorSiteGrant : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee? Employee { get; set; }

    public Guid AreaId { get; set; }
    public virtual CostCenters? Area { get; set; }
}
