using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

internal class ComplianceAcknowledgmentConfig : IEntityTypeConfiguration<ComplianceAcknowledgment>
{
    public void Configure(EntityTypeBuilder<ComplianceAcknowledgment> builder)
    {
        builder.Property(e => e.Scope).HasMaxLength(64).IsRequired();
        builder.Property(e => e.SettingKey).HasMaxLength(64).IsRequired();
        builder.Property(e => e.SettingLabel).HasMaxLength(128).IsRequired();
        builder.Property(e => e.StatutoryValue).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ConfiguredValue).HasMaxLength(64).IsRequired();
        builder.Property(e => e.UserEmail).HasMaxLength(256);
        builder.Property(e => e.IpAddress).HasMaxLength(64);
        builder.Property(e => e.AcknowledgedAtUtc)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        // "Already acknowledged at this value?" on every save of a deviating setting.
        builder.HasIndex(e => new { e.TenantId, e.Scope, e.SettingKey })
            .HasDatabaseName("IX_ComplianceAcknowledgment_Tenant_Scope_Key");
    }
}
