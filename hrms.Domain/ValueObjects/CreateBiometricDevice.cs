namespace Hrms.Domain.ValueObjects;

public class CreateBiometricDevice  
{
    public string SN { get; set; }=string.Empty;
}

public class UpdateBiometricDevice: CreateBiometricDevice
{
    public Guid Id { get; set; }
    public string Status  { get; set; }
}
