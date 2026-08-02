using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class DepartmentConfig : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder
             .HasOne(d => d.Head)
             .WithOne(e => e.HeadedDepartment) // Glues HeadedDepartment to d.Head
             .HasForeignKey<Department>(d => d.HeadId)
             .OnDelete(DeleteBehavior.Restrict);
    }
}
