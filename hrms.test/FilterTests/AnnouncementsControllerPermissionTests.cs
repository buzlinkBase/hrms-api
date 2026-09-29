using System.Reflection;
using Hrms.Api.Controllers;
using Hrms.Api.Filters;
using Microsoft.AspNetCore.Mvc.Routing;

namespace hrms.test.FilterTests;

/// <summary>
/// Publishing is gated on the "Announcements" catalog row (tenantstore's
/// PermissionCatalogSeederService); reading your own announcements needs no permission at all.
/// </summary>
public class AnnouncementsControllerPermissionTests
{
    private static IEnumerable<MethodInfo> Actions(Type controller) =>
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

    [Theory]
    [InlineData(nameof(AnnouncementsController.List), "Announcements:View")]
    [InlineData(nameof(AnnouncementsController.Get), "Announcements:View")]
    [InlineData(nameof(AnnouncementsController.Stats), "Announcements:View")]
    [InlineData(nameof(AnnouncementsController.Recipients), "Announcements:View")]
    [InlineData(nameof(AnnouncementsController.CreateDraft), "Announcements:Create")]
    [InlineData(nameof(AnnouncementsController.Duplicate), "Announcements:Create")]
    [InlineData(nameof(AnnouncementsController.Update), "Announcements:Edit")]
    [InlineData(nameof(AnnouncementsController.PublishNow), "Announcements:Publish")]
    [InlineData(nameof(AnnouncementsController.Schedule), "Announcements:Publish")]
    [InlineData(nameof(AnnouncementsController.Unschedule), "Announcements:Publish")]
    [InlineData(nameof(AnnouncementsController.Unpublish), "Announcements:Publish")]
    [InlineData(nameof(AnnouncementsController.Republish), "Announcements:Publish")]
    [InlineData(nameof(AnnouncementsController.Remind), "Announcements:Publish")]
    [InlineData(nameof(AnnouncementsController.Pin), "Announcements:Publish")]
    [InlineData(nameof(AnnouncementsController.Unpin), "Announcements:Publish")]
    [InlineData(nameof(AnnouncementsController.Archive), "Announcements:Publish")]
    [InlineData(nameof(AnnouncementsController.Restore), "Announcements:Publish")]
    [InlineData(nameof(AnnouncementsController.Remove), "Announcements:Delete")]
    [InlineData(nameof(AnnouncementsController.RestoreDeleted), "Announcements:Delete")]
    public void PublisherAction_RequiresItsPermission(string actionName, string code)
    {
        var method = typeof(AnnouncementsController).GetMethod(actionName)!;

        method.GetCustomAttribute<RequirePermissionAttribute>()!.Codes.Should().Equal(code);
    }

    [Fact]
    public void EveryPublisherAction_IsGatedOnTheAnnouncementsRow()
    {
        Actions(typeof(AnnouncementsController)).Should().OnlyContain(m =>
            m.GetCustomAttribute<RequirePermissionAttribute>() != null &&
            m.GetCustomAttribute<RequirePermissionAttribute>()!.Codes.All(c => c.StartsWith("Announcements:")));
    }

    [Fact]
    public void MyAnnouncements_NeedNoPermission()
    {
        Actions(typeof(MyAnnouncementsController)).Should().NotBeEmpty()
            .And.OnlyContain(m => m.GetCustomAttribute<RequirePermissionAttribute>() == null);
    }
}
