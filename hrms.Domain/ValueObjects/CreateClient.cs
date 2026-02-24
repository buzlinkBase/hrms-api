namespace Hrms.Domain.ValueObjects;

public class CreateClient
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; }

}

public class UpdateClient : CreateClient
{
    public Guid Id { get; set; }
}

public class ClientModel : UpdateArea
{
}