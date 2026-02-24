
using BuzlinkRepository;

namespace Hrms.Domain.Entities.EmployeeEntities
{
    [DisableSoftDelete]
    public class Education : BaseEntity
    {
        public Guid? EmployeeId { get; set; }
        public string SchoolName { get; set; }
        public int YearGraduated { get; set; }
    }
}
