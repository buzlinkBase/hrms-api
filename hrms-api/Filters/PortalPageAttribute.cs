using Hrms.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Hrms.Api.Filters;

/// <summary>
/// Marks a self-service action as backing one My Portal page. When the company switched that page
/// off (Company Settings > Payroll Settings) the action answers 404, so hiding a page can't be
/// bypassed by calling its endpoint directly.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class PortalPageAttribute : TypeFilterAttribute
{
    public PortalPageAttribute(PortalPage page) : base(typeof(PortalPageFilter))
    {
        Arguments = [page];
    }
}

public class PortalPageFilter : IAsyncActionFilter
{
    private readonly PortalPage _page;
    private readonly PortalVisibilityService _visibility;

    public PortalPageFilter(PortalPage page, PortalVisibilityService visibility)
    {
        _page = page;
        _visibility = visibility;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!(await _visibility.GetAsync()).IsVisible(_page))
        {
            context.Result = new NotFoundResult();
            return;
        }

        await next();
    }
}
