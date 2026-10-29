using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class SupervisorSiteGrantConfig : IEntityTypeConfiguration<SupervisorSiteGrant>
{
    public void Configure(EntityTypeBuilder<SupervisorSiteGrant> builder)
    {
        builder.HasIndex(x => new { x.EmployeeId, x.AreaId }).IsUnique();

        // A grant means nothing without the supervisor or the site, so it goes with either.
        builder.HasOne(x => x.Employee).WithMany()
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Area).WithMany()
            .HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Cascade);
    }
}
