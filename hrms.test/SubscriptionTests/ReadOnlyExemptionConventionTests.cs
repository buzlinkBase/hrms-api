using System.Reflection;
using Hrms.Api.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace hrms.test.SubscriptionTests;

/// <summary>
/// [AllowWhenReadOnly] lets a POST through SubscriptionAccessMiddleware while the company is
/// read-only, so it may only sit on actions that change nothing. The list is pinned: adding an
/// exemption is a deliberate, reviewed change here -- never a way to make a write work again.
/// </summary>
public class ReadOnlyExemptionConventionTests
{
    private static readonly string[] KnownPreviews =
    [
        $"{nameof(DailyRecordsController)}.{nameof(DailyRecordsController.Summary)}",
        $"{nameof(DailyRecordsController)}.{nameof(DailyRecordsController.Details)}",
        $"{nameof(PayrollsController)}.{nameof(PayrollsController.Calculate)}",
        $"{nameof(PayrollsController)}.{nameof(PayrollsController.PreviewYearEndAdjustment)}",
    ];

    [Fact]
    public void OnlyTheKnownPreviews_AreAllowedWhileReadOnly()
    {
        var exempted = typeof(PayrollsController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<AllowWhenReadOnlyAttribute>() != null
                    || t.GetCustomAttribute<AllowWhenReadOnlyAttribute>() != null)
                .Select(m => $"{t.Name}.{m.Name}"))
            .ToList();

        exempted.Should().BeEquivalentTo(KnownPreviews);
    }
}
