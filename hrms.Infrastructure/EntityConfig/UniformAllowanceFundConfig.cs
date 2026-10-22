using Hrms.Domain;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class UniformAllowanceFundConfig : IEntityTypeConfiguration<UniformAllowanceFund>
{
    public void Configure(EntityTypeBuilder<UniformAllowanceFund> builder)
    {
        builder.HasIndex(x => x.EmployeeId).IsUnique();
    }
}

public class UniformAllowanceLedgerConfig : IEntityTypeConfiguration<UniformAllowanceLedger>
{
    public void Configure(EntityTypeBuilder<UniformAllowanceLedger> builder)
    {
        builder.Property(x => x.EntryType)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, UniformAllowanceEntryType.Adjustment));

        builder.Property(x => x.AccrualDedupeKey).HasMaxLength(100);
        // Null for every non-Accrual entry -- multiple nulls never collide in a unique index,
        // so this only actually constrains Accrual rows. See UniformAllowanceAccrualWorker.
        builder.HasIndex(x => x.AccrualDedupeKey).IsUnique();
    }
}

public class UniformAllowancePolicyConfig : IEntityTypeConfiguration<UniformAllowancePolicy>
{
    public void Configure(EntityTypeBuilder<UniformAllowancePolicy> builder)
    {
        // One version per client per start date; the resolver picks the latest one on or before a day.
        builder.HasIndex(x => new { x.ClientId, x.EffectiveFrom }).IsUnique();

        builder.Property(x => x.Frequency).HasConversion(
            v => v.ToString(), v => EnumParserConfig.SafeParseEnum(v, UniformCreditFrequency.Monthly));
        builder.Property(x => x.AmountMode).HasConversion(
            v => v.ToString(), v => EnumParserConfig.SafeParseEnum(v, UniformAmountMode.FixedPerPeriod));
        builder.Property(x => x.Expiry).HasConversion(
            v => v.ToString(), v => EnumParserConfig.SafeParseEnum(v, UniformBalanceExpiry.CarryOver));
        builder.Property(x => x.OnSeparation).HasConversion(
            v => v.ToString(), v => EnumParserConfig.SafeParseEnum(v, UniformSeparationRule.KeepForRelease));
        builder.Property(x => x.RehireRule).HasConversion(
            v => v.ToString(), v => EnumParserConfig.SafeParseEnum(v, UniformRehireRule.Restart));

        // Stored as "Regular,Probationary"; an unknown name (a status since removed) is dropped.
        builder.Property(x => x.EligibleStatuses)
            .HasConversion(
                v => string.Join(',', v),
                v => ParseStatuses(v))
            .Metadata.SetValueComparer(new ValueComparer<List<EmploymentStatus>>(
                (a, b) => a!.SequenceEqual(b!),
                v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s)),
                v => v.ToList()));
        builder.Property(x => x.EligibleStatuses).HasMaxLength(500);

        builder.Property(x => x.ChangeReason).HasMaxLength(500);
        builder.Property(x => x.CreatedByName).HasMaxLength(200);
    }

    private static List<EmploymentStatus> ParseStatuses(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Enum.TryParse<EmploymentStatus>(s, out var status) ? (EmploymentStatus?)status : null)
            .Where(s => s.HasValue)
            .Select(s => s!.Value)
            .ToList();
}

public class UniformAllowanceCreditLogConfig : IEntityTypeConfiguration<UniformAllowanceCreditLog>
{
    public void Configure(EntityTypeBuilder<UniformAllowanceCreditLog> builder)
    {
        // One decision per employee per credit date -- a re-run of the same day never decides twice.
        builder.HasIndex(x => new { x.EmployeeId, x.CreditDate }).IsUnique();
        builder.HasIndex(x => new { x.ClientId, x.CreditDate });

        builder.Property(x => x.Outcome).HasConversion(
            v => v.ToString(), v => EnumParserConfig.SafeParseEnum(v, UniformCreditOutcome.Skipped));
        builder.Property(x => x.ReasonCode).HasMaxLength(50);
        builder.Property(x => x.ReasonText).HasMaxLength(300);
    }
}
