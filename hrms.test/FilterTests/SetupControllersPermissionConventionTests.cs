using System.Reflection;
using Hrms.Api.Controllers;
using Hrms.Api.Filters;
using Microsoft.AspNetCore.Mvc;

namespace hrms.test.FilterTests;

/// <summary>
/// Convention check across every Setup controller wired up for real permission enforcement
/// (see the plan: "Real Permission Enforcement for the Setup Module"). Rather than a full
/// integration test per controller — each needs its own dependency graph mocked, and
/// RequirePermissionAttribute's own logic is already covered by RequirePermissionAttributeTests
/// — this reflects over every mutating (POST/PUT/DELETE) action on these controllers and
/// verifies it carries a RequirePermissionAttribute whose codes belong to that controller's
/// catalog row, and that no GET action was accidentally gated (View is intentionally left
/// ungated — see the plan's "shared reference-data" finding).
/// </summary>
public class SetupControllersPermissionConventionTests
{
    // (Controller, catalog Feature) -- every controller currently wired for Setup enforcement.
    // EmployeesController deliberately omitted from the blanket scan below and checked
    // separately: most of its actions (filter, GetAll, GetFull, DownloadTemplate, GetFullById,
    // Print201) are GET reads used across the whole app, but Seed/RemoveSeeded are POST/DELETE
    // dev-only endpoints gated like any other mutation despite not being HttpPost/HttpDelete on
    // a single obvious CRUD action name -- the blanket "every POST/PUT/DELETE" scan still finds
    // them correctly, so it IS included below; called out here only because it's the one
    // controller with non-CRUD-shaped extra actions worth naming explicitly.
    private static readonly (Type Controller, string Feature)[] SetupControllers =
    [
        (typeof(PayrollGroupsController), "Organization Setup"),
        (typeof(DepartmentsController), "Organization Setup"),
        (typeof(SectionsController), "Organization Setup"),
        (typeof(PositionsController), "Organization Setup"),
        (typeof(BranchesController), "Organization Setup"),
        (typeof(CostCentersController), "Organization Setup"),
        (typeof(ClientsController), "Workforce Setup"),
        (typeof(EmployeesController), "Workforce Setup"),
        (typeof(TimeShiftsController), "Time Shift Setup"),
        (typeof(DeductionsController), "Deductions & Income Setup"),
        (typeof(DeductionTypesController), "Deductions & Income Setup"),
        (typeof(OtherIncomesController), "Deductions & Income Setup"),
        (typeof(OtherIncomeTypesController), "Deductions & Income Setup"),
        (typeof(LeavesController), "Leave Setup"),
        (typeof(SSSController), "Statutory Tables"),
        (typeof(PHICsController), "Statutory Tables"),
        (typeof(HDMFsController), "Statutory Tables"),
        (typeof(WTaxsController), "Statutory Tables"),
        (typeof(AnnualTaxsController), "Statutory Tables"),
    ];

    private static bool IsMutating(MethodInfo m) =>
        m.GetCustomAttribute<HttpPostAttribute>() != null ||
        m.GetCustomAttribute<HttpPutAttribute>() != null ||
        m.GetCustomAttribute<HttpDeleteAttribute>() != null;

    private static bool IsRead(MethodInfo m) =>
        m.GetCustomAttribute<HttpGetAttribute>() != null;

    private static IEnumerable<MethodInfo> Actions(Type controller) =>
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

    public static IEnumerable<object[]> MutatingActions() =>
        SetupControllers.SelectMany(c => Actions(c.Controller)
            .Where(IsMutating)
            .Select(m => new object[] { c.Controller, c.Feature, m.Name, m }));

    public static IEnumerable<object[]> ReadActions() =>
        SetupControllers.SelectMany(c => Actions(c.Controller)
            .Where(IsRead)
            .Select(m => new object[] { c.Controller, m.Name, m }));

    [Theory]
    [MemberData(nameof(MutatingActions))]
    public void MutatingAction_RequiresAPermission_ForItsOwnCatalogRow(
        Type controller, string feature, string actionName, MethodInfo method)
    {
        var attr = method.GetCustomAttribute<RequirePermissionAttribute>();

        attr.Should().NotBeNull(
            $"{controller.Name}.{actionName} mutates Setup data and must require a permission");
        attr!.Codes.Should().NotBeEmpty();
        attr.Codes.Should().OnlyContain(
            code => code.StartsWith($"{feature}:", StringComparison.Ordinal),
            $"{controller.Name}.{actionName}'s permission codes should belong to the \"{feature}\" row");
    }

    [Theory]
    [MemberData(nameof(ReadActions))]
    public void ReadAction_IsNotGated(Type controller, string actionName, MethodInfo method)
    {
        // GET endpoints on these entities are shared reference-data lookups consumed well
        // outside Setup (Work Rotation, Timekeeping, DTR, Payroll, Change Schedule, Biometric)
        // -- gating them would break every one of those. Only mutations are Setup-exclusive.
        method.GetCustomAttribute<RequirePermissionAttribute>().Should().BeNull(
            $"{controller.Name}.{actionName} is a read and must stay reachable by every authenticated caller");
    }

    [Fact]
    public void EveryMutatingActionCode_ExistsInThePermissionCatalogConvention()
    {
        // Sanity check on the table above itself: every feature name used must be one of the
        // seven Setup catalog rows (tenantstore's PermissionCatalogSeederService.Catalog) --
        // catches a typo'd feature name in this test file's own table.
        var knownFeatures = new[]
        {
            "Organization Setup", "Workforce Setup", "Time Shift Setup",
            "Deductions & Income Setup", "Leave Setup", "Statutory Tables", "Biometric Setup",
        };

        SetupControllers.Select(c => c.Feature).Distinct()
            .Should().OnlyContain(f => knownFeatures.Contains(f));
    }
}
