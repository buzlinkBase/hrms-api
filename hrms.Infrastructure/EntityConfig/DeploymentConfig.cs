using Hrms.Domain;
using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class ClientSiteConfig : IEntityTypeConfiguration<ClientSite>
{
    public void Configure(EntityTypeBuilder<ClientSite> builder)
    {
        builder.Property(x => x.Code).HasMaxLength(40);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.SubAreaLabel).HasMaxLength(100);
        builder.HasIndex(x => x.ClientId);
        builder.HasIndex(x => x.AreaId);
    }
}

public class ClientPostConfig : IEntityTypeConfiguration<ClientPost>
{
    public void Configure(EntityTypeBuilder<ClientPost> builder)
    {
        builder.Property(x => x.Code).HasMaxLength(40);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Position).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasIndex(x => x.ClientSiteId);
    }
}

public class PostRequirementConfig : IEntityTypeConfiguration<PostRequirement>
{
    public void Configure(EntityTypeBuilder<PostRequirement> builder)
    {
        builder.Property(x => x.ShiftLabel).HasMaxLength(60);
        builder.Property(x => x.DaysOfWeek).HasMaxLength(40);
        builder.HasIndex(x => new { x.PostId, x.EffectiveFrom });
    }
}

public class DeploymentConfig : IEntityTypeConfiguration<Deployment>
{
    public void Configure(EntityTypeBuilder<Deployment> builder)
    {
        builder.Property(x => x.Role)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, DeploymentRole.Regular)
            )
            .HasMaxLength(20);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.EventTag).HasMaxLength(200);
        builder.HasIndex(x => new { x.EmployeeId, x.StartDate });
        builder.HasIndex(x => new { x.PostId, x.StartDate });
    }
}
