using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig
{
    public class OperationalAreaConfig : IEntityTypeConfiguration<CostCenters>
    {
        public void Configure(EntityTypeBuilder<CostCenters> builder)
        {
            builder.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
