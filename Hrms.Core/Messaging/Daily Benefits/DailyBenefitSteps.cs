using Hrms.Core.Messaging.BenefitWorkers;
using Hrms.Core.Messaging.LeaveWorkers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hrms.Core.Messaging.DailyBenefits;

// One company's benefits run for one day. The date is the day being processed -- after downtime
// the Quartz job catches up missed days, so it isn't necessarily today.
public sealed record RunDailyBenefits(DateOnly Date, int FiscalYearStartMonth);

/// <summary>
/// One step of the daily benefits run (Strategy): whether it applies on a date, and what it does.
/// Steps are stateless; each runs with the services of its own scope (see DailyBenefitsWorker),
/// in <see cref="Order"/> -- carry-over before the period grant, record opening before accrual.
/// </summary>
public interface IDailyBenefitStep
{
    int Order { get; }
    string Name { get; }
    bool AppliesOn(RunDailyBenefits run);
    Task RunAsync(IServiceProvider services, RunDailyBenefits run, CancellationToken token);
}

// Closes the fiscal year that just ended -- on the new year's first day, so its balances are final.
public sealed class LeaveCarryOverStep : IDailyBenefitStep
{
    public int Order => 10;
    public string Name => "Leave carry-over";
    public bool AppliesOn(RunDailyBenefits run) => FiscalYearHelper.IsFiscalYearStart(run.Date, run.FiscalYearStartMonth);

    public Task RunAsync(IServiceProvider services, RunDailyBenefits run, CancellationToken token) =>
        services.GetRequiredService<LeaveCarryOverWorker>().RunAsync(
            new RunLeaveCarryOver(FiscalYearHelper.FiscalYearOf(run.Date, run.FiscalYearStartMonth) - 1, run.FiscalYearStartMonth), token);
}

public sealed class LeavePeriodGrantStep : IDailyBenefitStep
{
    public int Order => 20;
    public string Name => "Leave period grant";
    public bool AppliesOn(RunDailyBenefits run) => FiscalYearHelper.IsFiscalYearStart(run.Date, run.FiscalYearStartMonth);

    public Task RunAsync(IServiceProvider services, RunDailyBenefits run, CancellationToken token) =>
        services.GetRequiredService<LeavePeriodGrantWorker>().RunAsync(
            new RunLeavePeriodGrant(FiscalYearHelper.FiscalYearOf(run.Date, run.FiscalYearStartMonth), run.FiscalYearStartMonth), token);
}

public sealed class AccrualCreditOpeningStep : IDailyBenefitStep
{
    public int Order => 30;
    public string Name => "Accrual credit opening";
    public bool AppliesOn(RunDailyBenefits run) => run.Date.Day == 1;

    public Task RunAsync(IServiceProvider services, RunDailyBenefits run, CancellationToken token) =>
        services.GetRequiredService<AccrualCreditOpener>().OpenMissingAsync(run.Date, run.FiscalYearStartMonth, token);
}

public sealed class LeaveAccrualStep : IDailyBenefitStep
{
    public int Order => 40;
    public string Name => "Leave accrual";
    public bool AppliesOn(RunDailyBenefits run) => run.Date.Day == 1;

    public Task RunAsync(IServiceProvider services, RunDailyBenefits run, CancellationToken token) =>
        services.GetRequiredService<LeaveAccrualWorker>().RunAsync(
            new RunLeaveAccrual(run.Date, run.FiscalYearStartMonth), token);
}

public sealed class UniformAllowanceAccrualStep : IDailyBenefitStep
{
    public int Order => 50;
    public string Name => "Uniform allowance accrual";
    // Every day: each client's policy picks its own credit day (or each employee's anniversary).
    public bool AppliesOn(RunDailyBenefits run) => true;

    public Task RunAsync(IServiceProvider services, RunDailyBenefits run, CancellationToken token) =>
        services.GetRequiredService<UniformAllowanceAccrualWorker>().RunAsync(
            new RunUniformAllowanceAccrual(run.Date), token);
}

public static class DailyBenefitsRegistration
{
    public static IServiceCollection AddDailyBenefits(this IServiceCollection services)
    {
        services.AddSingleton<IDailyBenefitStep, LeaveCarryOverStep>();
        services.AddSingleton<IDailyBenefitStep, LeavePeriodGrantStep>();
        services.AddSingleton<IDailyBenefitStep, AccrualCreditOpeningStep>();
        services.AddSingleton<IDailyBenefitStep, LeaveAccrualStep>();
        services.AddSingleton<IDailyBenefitStep, UniformAllowanceAccrualStep>();

        // What the steps run -- MassTransit registers the consumers too; TryAdd keeps one each.
        services.TryAddScoped<LeaveCarryOverWorker>();
        services.TryAddScoped<LeavePeriodGrantWorker>();
        services.TryAddScoped<LeaveAccrualWorker>();
        services.TryAddScoped<UniformAllowanceAccrualWorker>();
        services.TryAddScoped<AccrualCreditOpener>();
        return services;
    }
}
