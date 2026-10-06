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

    [Fact]
    public void VariableAdditive_KeepsTheHoursLayout()
    {
        var doc = PayslipDocumentFactory.Create(Row(SalaryType.VARIABLE, PayrollType.Regular, OtNdCalculationMethod.Additive), Employee, null);

        doc.Should().BeOfType<PayslipHoursDocument>();
    }

    [Fact]
    public void VariableCompounded_KeepsTheStandardLayout()
    {
        var doc = PayslipDocumentFactory.Create(Row(SalaryType.VARIABLE, PayrollType.Regular, OtNdCalculationMethod.Compounded), Employee, null);

        doc.Should().BeOfType<PayslipDocument>();
    }

    [Theory]
    [InlineData(PayrollType.ThirteenthMonth)]
    [InlineData(PayrollType.LastPay)]
    [InlineData(PayrollType.YearEndAdjustment)]
    public void FixedLumpSumPayrollTypes_KeepTheSharedLumpSumLayout(PayrollType type)
    {
        var doc = PayslipDocumentFactory.Create(Row(SalaryType.FIXED, type, OtNdCalculationMethod.Compounded), Employee, null);

        doc.Should().BeOfType<PayslipDocument>();
    }
}
