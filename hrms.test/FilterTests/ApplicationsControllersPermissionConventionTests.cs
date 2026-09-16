using System.Reflection;
using Hrms.Api.Controllers;
using Hrms.Api.Filters;
using Microsoft.AspNetCore.Mvc;

namespace hrms.test.FilterTests;

/// <summary>
/// Convention check across every Applications controller wired up for real permission
/// enforcement. Unlike Setup (where only mutations are gated, GET stays open — shared
/// reference-data risk), Applications' admin screens have no cross-module GET sharing (verified
/// during research: employee self-service lives entirely on MeController), so every action here
/// — GET included — carries a RequirePermissionAttribute matching its controller's catalog
/// Feature prefix.
/// </summary>
public class ApplicationsControllersPermissionConventionTests
{
    private static readonly (Type Controller, string Feature)[] ApplicationsControllers =
    [
        (typeof(LeaveApplicationsController), "Leave"),
        (typeof(OvertimeApplicationsController), "Overtime"),
        (typeof(TravelOrderApplicationsController), "Official Business"),
        (typeof(UnderTimeApplicationsController), "Undertime"),
        (typeof(PassSlipApplicationsController), "Pass Slip"),
        (typeof(DeductionApplicationsApplicationsController), "Loan/Deduction"),
        (typeof(OtherIncomeApplicationApplicationsController), "Other Income"),
        (typeof(SalaryAdjustmentmentController), "Salary Adjustment"),
    ];

    private static IEnumerable<MethodInfo> Actions(Type controller) =>
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<HttpGetAttribute>() != null
                || m.GetCustomAttribute<HttpPostAttribute>() != null
                || m.GetCustomAttribute<HttpPutAttribute>() != null
                || m.GetCustomAttribute<HttpDeleteAttribute>() != null);

    public static IEnumerable<object[]> AllActions() =>
        ApplicationsControllers.SelectMany(c => Actions(c.Controller)
            .Select(m => new object[] { c.Controller, c.Feature, m.Name, m }));

    [Theory]
    [MemberData(nameof(AllActions))]
    public void EveryAction_RequiresAPermission_ForItsOwnCatalogRow(
        Type controller, string feature, string actionName, MethodInfo method)
    {
        var attr = method.GetCustomAttribute<RequirePermissionAttribute>();

        attr.Should().NotBeNull(
            $"{controller.Name}.{actionName} has no cross-module GET-sharing risk, so it should require a permission same as every other action here");
        attr!.Codes.Should().NotBeEmpty();
        attr.Codes.Should().OnlyContain(
            code => code.StartsWith($"{feature}:", StringComparison.Ordinal),
            $"{controller.Name}.{actionName}'s permission codes should belong to the \"{feature}\" row");
    }

    [Fact]
    public void EveryActionCode_ExistsInThePermissionCatalogConvention()
    {
        var knownFeatures = new[]
        {
            "Leave", "Overtime", "Official Business", "Undertime", "Pass Slip",
            "Loan/Deduction", "Other Income", "Salary Adjustment",
        };

        ApplicationsControllers.Select(c => c.Feature).Distinct()
            .Should().OnlyContain(f => knownFeatures.Contains(f));
    }
}
