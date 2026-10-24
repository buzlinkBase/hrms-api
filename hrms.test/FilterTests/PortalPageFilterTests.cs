using DTR.Core;
using Hrms.Api.Filters;
using Hrms.Core.Services;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace hrms.test.FilterTests;

/// <summary>A switched-off My Portal page answers 404 on its endpoints; an on page runs the action.</summary>
public class PortalPageFilterTests
{
    private static async Task<(IActionResult? Result, bool ActionRan)> Run(PortalPage page, bool? payslipsOn)
    {
        using var db = new SqliteHrmsContext();
        if (payslipsOn == false)
        {
            await using var seed = db.NewContext();
            seed.Add(new GeneralSetting
            {
                IdentityType = PayrollSettingsIdentity.IdentityType,
                Description = PayrollSettingsIdentity.KeyPortalShowPayslips,
                Value = "False",
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var filter = new PortalPageFilter(
            page, new PortalVisibilityService(new GeneralSettingService(new UnitOfWorkService(context))));

        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var executing = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), new object());
        var ran = false;

        await filter.OnActionExecutionAsync(executing, () =>
        {
            ran = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
        });

        return (executing.Result, ran);
    }

    [Fact]
    public async Task ASwitchedOffPageAnswersNotFound()
    {
        var (result, ran) = await Run(PortalPage.Payslips, payslipsOn: false);

        result.Should().BeOfType<NotFoundResult>();
        ran.Should().BeFalse();
    }

    [Fact]
    public async Task AnUnsavedSwitchLetsTheActionRun()
    {
        var (result, ran) = await Run(PortalPage.Payslips, payslipsOn: null);

        result.Should().BeNull();
        ran.Should().BeTrue();
    }

    [Fact]
    public async Task AnotherPagesSwitchDoesNotBlockThisOne()
    {
        var (result, ran) = await Run(PortalPage.Dtr, payslipsOn: false);

        result.Should().BeNull();
        ran.Should().BeTrue();
    }
}
