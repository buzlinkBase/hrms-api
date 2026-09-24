using Hrms.Domain;
using Hrms.Domain.Entities.Approvals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class ApprovalInstanceConfig : IEntityTypeConfiguration<ApprovalInstance>
{
    public void Configure(EntityTypeBuilder<ApprovalInstance> builder)
    {
        builder.Property(x => x.ApplicationType)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, ApprovalApplicationType.Leave));

        builder.Property(x => x.Status)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, ApprovalInstanceStatus.InProgress));

        // One live instance per application record. Looked up by (ApplicationType, ApplicationId)
        // from every one of the 5 controllers, so this is the hot lookup path.
        builder.HasIndex(x => new { x.ApplicationType, x.ApplicationId }).IsUnique();

        builder.HasOne(x => x.Workflow).WithMany()
            .HasForeignKey(x => x.ApprovalWorkflowId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Applicant).WithMany()
            .HasForeignKey(x => x.ApplicantEmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReassignedApprover).WithMany()
            .HasForeignKey(x => x.ReassignedApproverEmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ApprovalActionConfig : IEntityTypeConfiguration<ApprovalAction>
{
    public void Configure(EntityTypeBuilder<ApprovalAction> builder)
    {
        builder.Property(x => x.Action)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, ApprovalActionType.Approved));

        builder.Property(x => x.Note).HasMaxLength(2000);

        builder.HasIndex(x => new { x.ApprovalInstanceId, x.StepNumber });

        builder.HasOne(x => x.Instance).WithMany(x => x.Actions)
            .HasForeignKey(x => x.ApprovalInstanceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Actor).WithMany()
            .HasForeignKey(x => x.ActorEmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
