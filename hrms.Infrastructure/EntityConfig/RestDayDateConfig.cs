using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

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
