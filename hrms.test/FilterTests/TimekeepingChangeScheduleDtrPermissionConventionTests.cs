using System.Reflection;
using Hrms.Api.Controllers;
using Hrms.Api.Filters;

namespace hrms.test.FilterTests;

/// <summary>
/// Convention check across the Timekeeping, Change Schedule, and DTR Generation controllers.
/// AttendanceController and DailyRecordsController in particular have several exceptions to the
/// usual HTTP-verb convention (a GET that writes, POSTs that are pure queries, one GET shared
/// across two catalog rows) — this asserts the exact expected code(s) per action rather than a
/// shared prefix, so those exceptions stay pinned.
/// </summary>
public class TimekeepingChangeScheduleDtrPermissionConventionTests
{
    private static RequirePermissionAttribute? AttributeOn(Type controller, string actionName) =>
        controller.GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            ?.GetCustomAttribute<RequirePermissionAttribute>();

    private static void AssertCodes(Type controller, string actionName, params string[] expectedCodes)
    {
        var attr = AttributeOn(controller, actionName);
        attr.Should().NotBeNull($"{controller.Name}.{actionName} should require a permission");
        attr!.Codes.Should().BeEquivalentTo(expectedCodes);
    }

    [Fact]
    public void AttendanceController_Tag_IsGatedAsEdit_DespiteBeingAGet()
    {
        // AttendanceController overloads Unregistered(TagEmployeeRequest) (the "tag" GET that
        // actually writes) and Unregistered(UnRegisteredAttendance) (the plain "unregistered"
        // GET) -- disambiguate by parameter type since Type.GetMethod(name) throws
        // AmbiguousMatchException for overloaded methods.
        var tagAction = typeof(AttendanceController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Single(m => m.Name == nameof(AttendanceController.Unregistered)
                && m.GetParameters()[0].ParameterType == typeof(TagEmployeeRequest));

        var attr = tagAction.GetCustomAttribute<RequirePermissionAttribute>();

        // Persists a bio-id tag (_attendanceService.Tag) -- not a read.
        attr.Should().NotBeNull();
        attr!.Codes.Should().BeEquivalentTo(["Unregistered Employees:Edit"]);
    }

    [Theory]
    [InlineData(nameof(AttendanceController.Upload), "Upload Attendance:Edit")]
    [InlineData(nameof(AttendanceController.ManualEntry), "Attendance Manual Entry:Edit")]
    [InlineData(nameof(AttendanceController.UpdateAtt), "Attendance Manual Entry:Edit")]
    [InlineData(nameof(AttendanceController.GetManualEntry), "Attendance Manual Entry:View")]
    [InlineData(nameof(AttendanceController.GetShiftAttendance), "Attendance Manual Entry:View")]
    [InlineData(nameof(AttendanceController.RawRowLogs), "Raw Logs:View")]
    public void AttendanceController_ActionRequiresTheExpectedCode(string actionName, string expectedCode)
    {
        AssertCodes(typeof(AttendanceController), actionName, expectedCode);
    }

    [Fact]
    public void AttendanceController_Delete_BothOverloads_RequireAttendanceManualEntryDelete()
    {
        var deletes = typeof(AttendanceController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == nameof(AttendanceController.Delete))
            .ToList();

        var expected = new[] { "Attendance Manual Entry:Delete" };
        deletes.Should().HaveCount(2);
        deletes.Should().OnlyContain(m =>
            m.GetCustomAttribute<RequirePermissionAttribute>()!.Codes.SequenceEqual(expected));
    }

    [Theory]
    [InlineData(nameof(WorkSchedulePlansController.Get), "Work Rotation:View")]
    public void WorkSchedulePlansController_ActionRequiresTheExpectedCode(string actionName, string expectedCode)
    {
        AssertCodes(typeof(WorkSchedulePlansController), actionName, expectedCode);
    }

    [Fact]
    public void WorkSchedulePlansController_Delete_BothOverloads_RequireWorkRotationDelete()
    {
        var deletes = typeof(WorkSchedulePlansController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name is nameof(WorkSchedulePlansController.Delete) or nameof(WorkSchedulePlansController.DeleteBatch))
            .ToList();

        var expected = new[] { "Work Rotation:Delete" };
        deletes.Should().HaveCount(2);
        deletes.Should().OnlyContain(m =>
            m.GetCustomAttribute<RequirePermissionAttribute>()!.Codes.SequenceEqual(expected));
    }

    [Theory]
    [InlineData(nameof(ChangeRestDaysController.Post), "Change Rest Day:Create")]
    [InlineData(nameof(ChangeRestDaysController.Get), "Change Rest Day:View")]
    [InlineData(nameof(ChangeRestDaysController.Delete), "Change Rest Day:Delete")]
    [InlineData(nameof(ChangeRestDaysController.DeleteBatch), "Change Rest Day:Delete")]
    [InlineData(nameof(ChangeRestDaysController.Approve), "Change Rest Day:Approve")]
    [InlineData(nameof(ChangeRestDaysController.Decline), "Change Rest Day:Approve")]
    public void ChangeRestDaysController_ActionRequiresTheExpectedCode(string actionName, string expectedCode)
    {
        AssertCodes(typeof(ChangeRestDaysController), actionName, expectedCode);
    }

    [Theory]
    [InlineData(nameof(ChangeHolidaysController.Post), "Change Holiday:Create")]
    [InlineData(nameof(ChangeHolidaysController.Get), "Change Holiday:View")]
    [InlineData(nameof(ChangeHolidaysController.DeleteEmp), "Change Holiday:Delete")]
    [InlineData(nameof(ChangeHolidaysController.DeleteBatch), "Change Holiday:Delete")]
    public void ChangeHolidaysController_ActionRequiresTheExpectedCode(string actionName, string expectedCode)
    {
        AssertCodes(typeof(ChangeHolidaysController), actionName, expectedCode);
    }

    [Theory]
    [InlineData(nameof(DailyRecordsController.Post), "DTR Master:Manage")]
    [InlineData(nameof(DailyRecordsController.Summary), "DTR Summary:View")]
    [InlineData(nameof(DailyRecordsController.Details), "DTR Master:View")]
    [InlineData(nameof(DailyRecordsController.ApproveBatch), "DTR Master:Approve")]
    [InlineData(nameof(DailyRecordsController.DeclineBatch), "DTR Master:Approve")]
    [InlineData(nameof(DailyRecordsController.UnpostBatch), "DTR Summary:Manage")]
    [InlineData(nameof(DailyRecordsController.DTRDetailView), "DTR Master:View")]
    [InlineData(nameof(DailyRecordsController.DeleteBatch), "DTR Master:Manage")]
    [InlineData(nameof(DailyRecordsController.GenerateRawColumnarView), "Raw Logs:View")]
    [InlineData(nameof(DailyRecordsController.CleanRowView), "Raw Logs:View")]
    [InlineData(nameof(DailyRecordsController.CleanColumnarView), "Raw Logs:View")]
    [InlineData(nameof(DailyRecordsController.IncompleteColumnarLog), "Incomplete Punches:View")]
    public void DailyRecordsController_ActionRequiresTheExpectedCode(string actionName, string expectedCode)
    {
        AssertCodes(typeof(DailyRecordsController), actionName, expectedCode);
    }

    [Fact]
    public void DailyRecordsController_GetCodes_IsGated_AnyOfDtrMasterOrDtrSummaryView()
    {
        // Shared by both the DTR Master and DTR Summary screens.
        AssertCodes(typeof(DailyRecordsController), nameof(DailyRecordsController.GetCodes),
            "DTR Master:View", "DTR Summary:View");
    }
}
