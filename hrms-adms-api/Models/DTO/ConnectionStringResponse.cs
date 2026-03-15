using MessagePack;

namespace Hrms.adms.Models.DTO;

[MessagePackObject]
public class ConnectionStringResponse
{
    [Key(0)]
    public string? ConnectionString { get; set; }
}
