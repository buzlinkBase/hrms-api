using Hrms.Domain;
using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class ClientRateTableConfig : IEntityTypeConfiguration<ClientRateTable>
{
    public void Configure(EntityTypeBuilder<ClientRateTable> builder)
    {
        builder.Property(x => x.Type)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, RateType.REGULAR)
            );

        builder.HasIndex(x => new { x.ClientId, x.Type }).IsUnique();
    }
}
