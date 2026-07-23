using MessagePack;

namespace Hrms.Domain.ValueObjects;

[MessagePackObject]
public partial class CreateWorkRotationPlan
{
    [Key(0)] public DateOnly PayrollDate { get; set; }
    [Key(1)] public Guid EmployeeId { get; set; }
    [Key(2)] public Guid TimeShiftId { get; set; }
}

[MessagePackObject]
public partial class UpdateWorkSchedulePlan : CreateWorkRotationPlan
{
    [IgnoreMember]
    public Guid Id { get; set; }
    [IgnoreMember]
    public string Status { get; set; }
}

[MessagePackObject]
public partial class WorkSchedulePlanModel : UpdateWorkSchedulePlan
{
}

