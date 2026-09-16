using System.Reflection;
using Hrms.Api.Controllers;
using Hrms.Api.Filters;
using Microsoft.AspNetCore.Mvc;

namespace hrms.test.FilterTests;

/// <summary>
/// Convention check across the Reports module's controllers. Unlike Applications (one uniform
/// Feature per controller) or Setup (uniform action-per-verb), Reports actions map to whichever
/// of its 4 catalog rows fits their subject matter (Government Statutory Reports, Payroll
/// Reports, BIR Reports, Attendance Reports), split further by View vs Export depending on
/// whether the action returns JSON or a file -- so this asserts the exact expected code per
/// action rather than a shared prefix.
/// </summary>
public class ReportsControllersPermissionConventionTests
{
    private static RequirePermissionAttribute? AttributeOn(Type controller, string actionName) =>
        controller.GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            ?.GetCustomAttribute<RequirePermissionAttribute>();

    public static IEnumerable<object[]> PayrollReportsActions() =>
    [
        [nameof(PayrollReportsController.SssRemittance), "Government Statutory Reports:View"],
        [nameof(PayrollReportsController.PhilHealthRemittance), "Government Statutory Reports:View"],
        [nameof(PayrollReportsController.PagIbigRemittance), "Government Statutory Reports:View"],
        [nameof(PayrollReportsController.WTaxRemittance), "Government Statutory Reports:View"],
        [nameof(PayrollReportsController.BankDisbursement), "Payroll Reports:View"],
        [nameof(PayrollReportsController.DeductionLedger), "Payroll Reports:View"],
        [nameof(PayrollReportsController.CashBond), "Payroll Reports:View"],
        [nameof(PayrollReportsController.LeaveLedger), "Payroll Reports:View"],
        [nameof(PayrollReportsController.RetirementLedger), "Payroll Reports:View"],
        [nameof(PayrollReportsController.UniformAllowanceLedger), "Payroll Reports:View"],
        [nameof(PayrollReportsController.ReimbursementList), "Payroll Reports:View"],
        [nameof(PayrollReportsController.CostSummary), "Payroll Reports:View"],
        [nameof(PayrollReportsController.YtdSummary), "Payroll Reports:View"],
        [nameof(PayrollReportsController.ThirteenthMonthPay), "Payroll Reports:View"],
        [nameof(PayrollReportsController.ThirteenthMonthPayPrint), "Payroll Reports:Export"],
        [nameof(PayrollReportsController.MonthlyRemittanceReturn), "BIR Reports:View"],
        [nameof(PayrollReportsController.MonthlyRemittanceReturnPrint), "BIR Reports:Export"],
        [nameof(PayrollReportsController.Alphalist), "BIR Reports:View"],
        [nameof(PayrollReportsController.AlphalistPrint), "BIR Reports:Export"],
        [nameof(PayrollReportsController.AlphalistExport), "BIR Reports:Export"],
        [nameof(PayrollReportsController.Bir2316), "BIR Reports:View"],
        [nameof(PayrollReportsController.Bir2316Print), "BIR Reports:Export"],
        [nameof(PayrollReportsController.SssR3Print), "BIR Reports:Export"],
        [nameof(PayrollReportsController.SssR3Export), "BIR Reports:Export"],
        [nameof(PayrollReportsController.PhilHealthEprsPrint), "BIR Reports:Export"],
        [nameof(PayrollReportsController.PhilHealthEprsExport), "BIR Reports:Export"],
        [nameof(PayrollReportsController.PagIbigMcrfPrint), "BIR Reports:Export"],
        [nameof(PayrollReportsController.PagIbigMcrfExport), "BIR Reports:Export"],
    ];

    [Theory]
    [MemberData(nameof(PayrollReportsActions))]
    public void PayrollReportsController_ActionRequiresTheExpectedCode(string actionName, string expectedCode)
    {
        var attr = AttributeOn(typeof(PayrollReportsController), actionName);

        attr.Should().NotBeNull($"PayrollReportsController.{actionName} should require a permission");
        attr!.Codes.Should().ContainSingle().Which.Should().Be(expectedCode);
    }

    [Fact]
    public void PayrollReportsController_HasExactly28GatedActions()
    {
        // Sanity check on the table above itself -- catches a forgotten row if the controller
        // ever grows a new report action.
        var gatedActionCount = typeof(PayrollReportsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Count(m => m.GetCustomAttribute<RequirePermissionAttribute>() != null);

        gatedActionCount.Should().Be(28);
    }

    [Theory]
    [InlineData(nameof(DailyRecordsController.TardinessReport), "Attendance Reports:View")]
    [InlineData(nameof(DailyRecordsController.RosterReport), "Attendance Reports:View")]
    public void DailyRecordsController_ReportAction_RequiresTheExpectedCode(string actionName, string expectedCode)
    {
        var attr = AttributeOn(typeof(DailyRecordsController), actionName);

        attr.Should().NotBeNull($"DailyRecordsController.{actionName} should require a permission");
        attr!.Codes.Should().ContainSingle().Which.Should().Be(expectedCode);
    }

    [Theory]
    [InlineData(nameof(RetirementController.Adjust), "Payroll Reports:Edit")]
    public void RetirementController_ActionRequiresTheExpectedCode(string actionName, string expectedCode)
    {
        var attr = AttributeOn(typeof(RetirementController), actionName);

        attr.Should().NotBeNull($"RetirementController.{actionName} should require a permission");
        attr!.Codes.Should().ContainSingle().Which.Should().Be(expectedCode);
    }

    [Theory]
    [InlineData(nameof(UniformAllowanceController.Balances), "Payroll Reports:View")]
    [InlineData(nameof(UniformAllowanceController.Adjust), "Payroll Reports:Edit")]
    [InlineData(nameof(UniformAllowanceController.Release), "Payroll Reports:Edit")]
    public void UniformAllowanceController_ActionRequiresTheExpectedCode(string actionName, string expectedCode)
    {
        var attr = AttributeOn(typeof(UniformAllowanceController), actionName);

        attr.Should().NotBeNull($"UniformAllowanceController.{actionName} should require a permission");
        attr!.Codes.Should().ContainSingle().Which.Should().Be(expectedCode);
    }
}
