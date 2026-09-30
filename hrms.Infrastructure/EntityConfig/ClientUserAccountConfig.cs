using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class ClientUserAccountConfig : IEntityTypeConfiguration<ClientUserAccount>
{
    public void Configure(EntityTypeBuilder<ClientUserAccount> builder)
    {
        builder.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId);
        // An account belongs to at most one client per company.
        builder.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Email });
    }
}
