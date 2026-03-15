using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hrms.Domain.ValueObjects;

[MessagePackObject]
public class ConnectionStringResponse
{
    [Key(0)]
    public string? ConnectionString { get; set; }
}

