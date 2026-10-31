using Hrms.Api.Documents;
using Hrms.Api.Documents.Fixed;
using Hrms.Api.Documents.Shared;
using Hrms.Domain.ValueObjects;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// PayslipDocumentFactory -- picks the payslip layout from the Payroll row, replacing the ternary
/// that used to be copied into PayrollsController and MeController.
/// </summary>
public class PayslipDocumentFactoryTests
{
    private static readonly EmployeeFullModel Employee = new();

    private static Payroll Row(SalaryType salary, PayrollType type, OtNdCalculationMethod method) => new()
    {
        SalaryType = salary,
        PayrollType = type,
        OtNdCalculationMethod = method,
    };

    [Theory]
    [InlineData(OtNdCalculationMethod.Compounded)]
    [InlineData(OtNdCalculationMethod.Additive)]
    public void FixedRegularPayroll_UsesFixedLayout_WhateverTheOtNdMethod(OtNdCalculationMethod method)
    {
        var doc = PayslipDocumentFactory.Create(Row(SalaryType.FIXED, PayrollType.Regular, method), Employee, null);

        doc.Should().BeOfType<PayslipFixedDocument>();
    }

    [Theory]
    [InlineData(OtNdCalculationMethod.Compounded)]
    [InlineData(OtNdCalculationMethod.Additive)]
    public void VariableRegularPayroll_UsesTheHoursLayout_WhateverTheOtNdMethod(OtNdCalculationMethod method)
    {
        var doc = PayslipDocumentFactory.Create(Row(SalaryType.VARIABLE, PayrollType.Regular, method), Employee, null);

        doc.Should().BeOfType<PayslipHoursDocument>();
    }

    [Theory]
    [InlineData(SalaryType.FIXED, PayrollType.ThirteenthMonth, OtNdCalculationMethod.Compounded)]
    [InlineData(SalaryType.FIXED, PayrollType.LastPay, OtNdCalculationMethod.Compounded)]
    [InlineData(SalaryType.FIXED, PayrollType.YearEndAdjustment, OtNdCalculationMethod.Compounded)]
    [InlineData(SalaryType.VARIABLE, PayrollType.ThirteenthMonth, OtNdCalculationMethod.Additive)]
    [InlineData(SalaryType.VARIABLE, PayrollType.LastPay, OtNdCalculationMethod.Compounded)]
    public void LumpSumPayrollTypes_KeepTheSharedLumpSumLayout(SalaryType salary, PayrollType type, OtNdCalculationMethod method)
    {
        var doc = PayslipDocumentFactory.Create(Row(salary, type, method), Employee, null);

        doc.Should().BeOfType<PayslipDocument>();
    }
}
