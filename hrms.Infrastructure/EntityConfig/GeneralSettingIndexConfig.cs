using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

internal class GeneralSettingIndexConfig : IEntityTypeConfiguration<GeneralSetting>
{
    public void Configure(EntityTypeBuilder<GeneralSetting> builder)
    {
        // Every GeneralSettingService query (per-company, per-client-batch, replace-by-identity,
        // delete-by-identity) filters on IdentityType (+ IdentityTypeId), and this table grows
        // by identity × setting key — keep that lookup a seek instead of a scan as it grows.
        builder.HasIndex(x => new { x.IdentityType, x.IdentityTypeId });
    }
}
