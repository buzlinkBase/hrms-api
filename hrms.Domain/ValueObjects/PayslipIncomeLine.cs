namespace Hrms.Domain.ValueObjects;

// One Other Income item as printed on a payslip: the income type's name and what this payroll
// paid for it. See IncomeAplDtlService.GetPayslipLinesAsync.
public record PayslipIncomeLine(string Label, decimal Amount);
