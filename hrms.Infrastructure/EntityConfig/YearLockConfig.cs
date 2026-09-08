using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class YearLockConfig : IEntityTypeConfiguration<YearLock>
{
    public void Configure(EntityTypeBuilder<YearLock> builder)
    {
        // TenantId included explicitly — a plain unique index on Year alone would be a real DB
        // constraint (unaffected by the runtime tenant query filter) and would wrongly block a
        // second tenant from ever locking the same calendar year.
        builder.HasIndex(x => new { x.TenantId, x.Year }).IsUnique();
    }
}
