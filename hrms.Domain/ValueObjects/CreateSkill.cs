namespace Hrms.Domain.ValueObjects;

public class CreateSkill
{
    public Guid? EmployeeId { get; set; }
    public string Name { get; set; }
    public double Level { get; set; }
}

public class UpdateSkill : CreateSkill
{
    public Guid Id { get; set; }
}
public class SkillModel : UpdateSkill;