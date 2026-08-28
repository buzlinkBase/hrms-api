using Hrms.Domain;
using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class ClientBillingInfoConfig : IEntityTypeConfiguration<ClientBillingInfo>
{
    public void Configure(EntityTypeBuilder<ClientBillingInfo> builder)
    {
        builder.Property(x => x.BillingCycle)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, BillingCycle.Monthly)
            );

        builder.HasIndex(x => x.ClientId).IsUnique();
    }
}
