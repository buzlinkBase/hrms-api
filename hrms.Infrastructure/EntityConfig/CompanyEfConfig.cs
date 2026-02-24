using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

internal class CompanyEfConfig : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasData(
            new Company() { Id = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"), TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"), Code = "0001", ShortName = "TC", Description = "Test Company", TotalWorkingDays = 26 }
            );
    }
}

internal class BranchEfConfig : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.HasData(
            new Branch() { Id = Guid.NewGuid(), TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"), Code = "0001", ShortName = "MB", Description = "Main Branch" }
            );
    }
}
