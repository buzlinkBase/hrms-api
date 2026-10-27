using Hrms.Domain;
using Hrms.Domain.Entities.Approvals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class ApprovalDelegationConfig : IEntityTypeConfiguration<ApprovalDelegation>
{
    public void Configure(EntityTypeBuilder<ApprovalDelegation> builder)
    {
        builder.Property(x => x.ApplicationType)
            .HasConversion(
                v => v.HasValue ? v.Value.ToString() : null,
                v => v == null ? (ApprovalApplicationType?)null : EnumParserConfig.SafeParseEnum(v, ApprovalApplicationType.Leave))
            .HasMaxLength(32);
        builder.Property(x => x.Reason).HasMaxLength(500);

        // "Who has delegated to me" runs on every eligibility check; "what have I delegated" on the profile card.
        builder.HasIndex(x => new { x.DelegateEmployeeId, x.IsActive });
        builder.HasIndex(x => new { x.DelegatorEmployeeId, x.IsActive });

        builder.HasOne(x => x.Delegator).WithMany()
            .HasForeignKey(x => x.DelegatorEmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Delegate).WithMany()
            .HasForeignKey(x => x.DelegateEmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
