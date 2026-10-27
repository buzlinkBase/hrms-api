using Hrms.Domain;
using Hrms.Domain.Entities.Approvals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class ApprovalWorkflowConfig : IEntityTypeConfiguration<ApprovalWorkflow>
{
    public void Configure(EntityTypeBuilder<ApprovalWorkflow> builder)
    {
        builder.Property(x => x.ApplicationType)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, ApprovalApplicationType.Leave));

        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Priority).HasDefaultValue(100);

        // Lookup index for the resolution query (the active workflows of a type). Not a DB-level
        // uniqueness constraint — "only one active per (ApplicationType, scope signature)" is
        // enforced by ApprovalWorkflowService on activation, same as every other business-rule-level
        // uniqueness in this codebase.
        builder.HasIndex(x => new { x.ApplicationType, x.ScopeDepartmentId, x.IsActive });

        builder.HasOne(x => x.ScopeDepartment).WithMany()
            .HasForeignKey(x => x.ScopeDepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ApprovalWorkflowStepConfig : IEntityTypeConfiguration<ApprovalWorkflowStep>
{
    public void Configure(EntityTypeBuilder<ApprovalWorkflowStep> builder)
    {
        builder.Property(x => x.ApproverType)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, ApproverType.Person));

        builder.Property(x => x.NoteRequirement)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, NoteRequirement.Optional));

        builder.Property(x => x.ConditionMetric)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, ApprovalStepConditionMetric.None))
            .HasMaxLength(16);
        builder.Property(x => x.ConditionOperator)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, ApprovalConditionOperator.GreaterOrEqual))
            .HasMaxLength(16);

        builder.Property(x => x.OnUnresolved)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, UnresolvedApproverPolicy.Block))
            .HasMaxLength(16);

        builder.HasIndex(x => new { x.ApprovalWorkflowId, x.StepNumber }).IsUnique();

        builder.HasOne(x => x.Workflow).WithMany(x => x.Steps)
            .HasForeignKey(x => x.ApprovalWorkflowId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ApproverEmployee).WithMany()
            .HasForeignKey(x => x.ApproverEmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApproverDepartment).WithMany()
            .HasForeignKey(x => x.ApproverDepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApproverPosition).WithMany()
            .HasForeignKey(x => x.ApproverPositionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ApprovalWorkflowStepGroupConfig : IEntityTypeConfiguration<ApprovalWorkflowStepGroup>
{
    public void Configure(EntityTypeBuilder<ApprovalWorkflowStepGroup> builder)
    {
        builder.Property(x => x.ApproverType)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, ApproverType.Person));
        builder.Property(x => x.OnUnresolved)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, UnresolvedApproverPolicy.Block))
            .HasMaxLength(16);

        builder.HasIndex(x => new { x.ApprovalWorkflowStepId, x.GroupNumber }).IsUnique();

        builder.HasOne(x => x.Step).WithMany(x => x.Groups)
            .HasForeignKey(x => x.ApprovalWorkflowStepId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ApproverDepartment).WithMany()
            .HasForeignKey(x => x.ApproverDepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApproverPosition).WithMany()
            .HasForeignKey(x => x.ApproverPositionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ApprovalWorkflowStepApproverConfig : IEntityTypeConfiguration<ApprovalWorkflowStepApprover>
{
    public void Configure(EntityTypeBuilder<ApprovalWorkflowStepApprover> builder)
    {
        builder.HasIndex(x => new { x.ApprovalWorkflowStepId, x.EmployeeId }).IsUnique();

        builder.HasOne(x => x.Step).WithMany(x => x.NamedApprovers)
            .HasForeignKey(x => x.ApprovalWorkflowStepId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Employee).WithMany()
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ApprovalWorkflowScopeConfig : IEntityTypeConfiguration<ApprovalWorkflowScope>
{
    public void Configure(EntityTypeBuilder<ApprovalWorkflowScope> builder)
    {
        builder.Property(x => x.Dimension)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, ApprovalScopeDimension.Department))
            .HasMaxLength(32);
        builder.Property(x => x.TargetValue).HasMaxLength(64);

        builder.HasIndex(x => new { x.ApprovalWorkflowId, x.Dimension, x.TargetId, x.TargetValue }).IsUnique();

        builder.HasOne(x => x.Workflow).WithMany(x => x.Scopes)
            .HasForeignKey(x => x.ApprovalWorkflowId).OnDelete(DeleteBehavior.Cascade);
    }
}
