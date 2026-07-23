using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

internal class RestDayDateConfig : IEntityTypeConfiguration<RestDayDate>
{
    public void Configure(EntityTypeBuilder<RestDayDate> builder)
    {
        builder
       .HasOne(r => r.Employee)
       .WithMany()
       .HasForeignKey(x => x.EmployeeId)
       .IsRequired(false);
    }
}
