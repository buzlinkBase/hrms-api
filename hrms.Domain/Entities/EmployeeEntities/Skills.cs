using BuzlinkRepository;

namespace Hrms.Domain.Entities.EmployeeEntities
{
    [DisableSoftDelete]
    public class Skill : BaseEntity
    {
        public Guid? EmployeeId { get; set; }
        public string Name { get; set; }
        public double Level { get; set; }
    }
}
