using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class HolidayAreaConfig : IEntityTypeConfiguration<HolidayArea>
{
    public void Configure(EntityTypeBuilder<HolidayArea> builder)
    {
        builder.HasIndex(x => new { x.HolidayId, x.AreaId }).IsUnique();

        builder.HasOne(x => x.Holiday).WithMany(x => x.Areas)
            .HasForeignKey(x => x.HolidayId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Area).WithMany()
            .HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
    }
}
