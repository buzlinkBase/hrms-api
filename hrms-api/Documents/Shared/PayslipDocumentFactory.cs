using Hrms.Api.Documents.Fixed;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using QuestPDF.Infrastructure;

namespace Hrms.Api.Documents.Shared;

// One place that decides which payslip layout a Payroll row prints with, instead of the same
// ternary copied into every controller that prints a payslip (Payrolls and Me/portal).
// Chain of Responsibility: the layouts are tried in order and the first that handles the row
// builds the document, so a new layout is one new class added to the list (open/closed) and the
// controllers never change. StandardPayslipLayout is last and handles everything.
internal interface IPayslipLayout
{
    bool Handles(Payroll payroll);
    IDocument Create(Payroll payroll, EmployeeFullModel employee, Company? company, PayslipOtherIncome otherIncome, byte[]? logo);
}

internal static class PayslipDocumentFactory
{
    // Order matters: most specific first.
    private static readonly IReadOnlyList<IPayslipLayout> Layouts = new IPayslipLayout[]
    {
        new FixedSalaryPayslipLayout(),
        new HoursPayslipLayout(),
        new StandardPayslipLayout(),
    };

    // otherIncome: the itemized Other Income lines for this payroll (see PayslipOtherIncome);
    // omitted, every layout prints the category totals saved on the row.
    // logo: the company logo picture for the letterhead (see CompanyLetterheadService); omitted, the
    // header is plain text.
    public static IDocument Create(Payroll payroll, EmployeeFullModel employee, Company? company,
        PayslipOtherIncome? otherIncome = null, byte[]? logo = null) =>
        Layouts.First(l => l.Handles(payroll))
            .Create(payroll, employee, company, otherIncome ?? PayslipOtherIncome.Categories, logo);

    // FIXED employees aren't paid by the hour, so "hours x rate" doesn't apply whatever the
    // company's OT/ND method is. Lump-sum payroll types (13th Month, Last Pay, Year-End
    // Adjustment) have no basic-salary breakdown, so they keep the shared lump-sum layout.
    internal sealed class FixedSalaryPayslipLayout : IPayslipLayout
    {
        public bool Handles(Payroll p) => p.SalaryType == SalaryType.FIXED && p.PayrollType == PayrollType.Regular;
        public IDocument Create(Payroll p, EmployeeFullModel e, Company? c, PayslipOtherIncome o, byte[]? logo) => new PayslipFixedDocument(p, e, c, o, logo);
    }

    // Follows the OT/ND method actually used for the run (recorded per row on
    // Payroll.OtNdCalculationMethod, not the live company setting, so an old payslip reprints in
    // the format it was originally correct for): Additive's itemized rows reconcile to Gross
    // Income exactly.
    internal sealed class HoursPayslipLayout : IPayslipLayout
    {
        public bool Handles(Payroll p) => p.OtNdCalculationMethod == OtNdCalculationMethod.Additive;
        public IDocument Create(Payroll p, EmployeeFullModel e, Company? c, PayslipOtherIncome o, byte[]? logo) => new PayslipHoursDocument(p, e, c, o, logo);
    }

    // Compounded's Standard format is the client-approved default.
    internal sealed class StandardPayslipLayout : IPayslipLayout
    {
        public bool Handles(Payroll p) => true;
        public IDocument Create(Payroll p, EmployeeFullModel e, Company? c, PayslipOtherIncome o, byte[]? logo) => new PayslipDocument(p, e, c, o, logo);
    }
}
