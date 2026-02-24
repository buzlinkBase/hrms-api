namespace Hrms.Domain.ValueObjects;

public class CreateAssignAsset
{
    public Guid EmployeeId { get; set; }
    public string AssetType { get; set; }
    public string AssetDescription { get; set; }
    public string Model { get; set; }
    public string Brand { get; set; }
    public string SerialNo { get; set; }
    public int Qty { get; set; }
    public DateOnly IssuanceDate { get; set; }
    public DateOnly ReturnedDate { get; set; }
    public string Remarks { get; set; }
    public string File { get; set; }
}
public class UpdateAssignAsset : CreateAssignAsset
{
    public Guid Id { get; set; }
}

public class AssignAssetModel : UpdateAssignAsset;