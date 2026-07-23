using MessagePack;

namespace Hrms.Domain.ValueObjects;

[MessagePackObject]
public class ConnectionStringResponse
{
    [Key(0)]
    public string? ConnectionString { get; set; }
}

