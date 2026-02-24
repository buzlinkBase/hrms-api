using MessagePack;

namespace Hrms.Domain.ValueObjects;

public class CreateBranch
{
    public string Code { get; set; }
    public string Description { get; set; }
    public string ShortName { get; set; }
    public string Address { get; set; }
    public Guid? HeadId { get; set; }
    public string Status { get; set; }

}
public class UpdateBranch : CreateBranch
{
    public Guid Id { get; set; }
}

[MessagePackObject]
public class BranchModel
{
    [Key(0)]
    public Guid Id { get; set; }
    [Key(1)]
    public required Guid TenantId { get; set; }
    [Key(2)]
    public required string Name { get; set; }
    [Key(3)]
    public string? Address { get; set; }
    [Key(4)]
    public string? Contact { get; set; }
    [Key(5)]
    public string? ManagerName { get; set; }
    [Key(6)]
    public string Status { get; set; }
}
