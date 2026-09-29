namespace Hrms.Domain.ValueObjects;

// An employee's pay columns -- the part of the profile that is a compensation figure (the cash bond
// rate isn't: it's an Entries figure, like the cash bond ledger). Shared by
// the entity, the create/update payloads and the response models, so hiding them from a partial
// confidential clearance (Operations / Entries) is one rule, not one per model.
public interface IEmployeePayFields
{
    decimal MonthlyRate { get; set; }
    decimal DailyRate { get; set; }
    decimal Cola { get; set; }
    decimal? FactorDays { get; set; }
    string BankName { get; set; }
    string BankNo { get; set; }
}

// A response that can come back with the pay hidden (FiguresHidden tells the UI why it's blank).
public interface IEmployeePayView : IEmployeePayFields
{
    Guid Id { get; }
    CreateSSSRate? SSSRate { get; set; }
    CreatePHICRate? PHICRate { get; set; }
    CreateHDMFRate? HDMFRate { get; set; }
    CreateTaxRate? TaxRate { get; set; }
    bool FiguresHidden { get; set; }
}

public static class EmployeePayFields
{
    public static void CopyPayTo(this IEmployeePayFields from, IEmployeePayFields to)
    {
        to.MonthlyRate = from.MonthlyRate;
        to.DailyRate = from.DailyRate;
        to.Cola = from.Cola;
        to.FactorDays = from.FactorDays;
        to.BankName = from.BankName;
        to.BankNo = from.BankNo;
    }

    public static void HidePay(this IEmployeePayView view)
    {
        view.MonthlyRate = 0;
        view.DailyRate = 0;
        view.Cola = 0;
        view.FactorDays = null;
        view.BankName = string.Empty;
        view.BankNo = string.Empty;
        view.SSSRate = null;
        view.PHICRate = null;
        view.HDMFRate = null;
        view.TaxRate = null;
        view.FiguresHidden = true;
    }
}
