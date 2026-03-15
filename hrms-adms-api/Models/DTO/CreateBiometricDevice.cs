namespace Hrms.adms.Models.DTO;
public class CreateBiometricDevice  
{
    public string SN { get; set; }=string.Empty;
    public Guid? BranchId { get; set; }
    public Guid? ClientId  { get; set; }
}
public class UpdateBiometricDevice: CreateBiometricDevice
{
    public Guid Id { get; set; }
    public string Status  { get; set; }
}
