using Hrms.Domain;
using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class NotificationPreferenceConfig : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.Property(x => x.ApplicationType)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, ApprovalApplicationType.Leave));

        builder.HasIndex(x => new { x.EmployeeId, x.ApplicationType }).IsUnique();

        builder.HasOne(x => x.Employee).WithMany()
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
    }
}
