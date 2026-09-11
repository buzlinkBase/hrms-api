using Hrms.Api.Documents.Bir;
using Hrms.Api.Documents.Shared;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;

namespace Hrms.Api.Documents;

// BIR Form 1601-C — Monthly Remittance Return of Income Taxes Withheld on Compensation. Filed
// through eBIRForms/eFPS (BIR does not accept a raw file upload for this return the way
// SSS/PhilHealth/Pag-IBIG do), so this exists to give the preparer an exact copy of the return
// with the figures already filled in, stamped onto the actual official template
// (wwwroot/Bir/1601CfinalJan2018.pdf) rather than a from-scratch replica — see
// Bir1601CFieldMap and PdfOverlayEngine.
//
// The official form has no per-employee table (only company-wide aggregate Lines 15-19) — the
// per-employee breakdown the previous QuestPDF version added isn't part of the real form, so it
// isn't reproduced here. Per-employee detail is still available via the on-screen 1601-C report
// table (GET .../1601c) and the separate Alphalist report/print action.
public class MonthlyRemittanceReturnOverlayDocument
{
    private readonly MonthlyRemittanceReturnModel _data;
    private readonly Company? _company;
    private readonly string _webRootPath;

    public MonthlyRemittanceReturnOverlayDocument(MonthlyRemittanceReturnModel data, Company? company, string webRootPath)
    {
        _data = data;
        _company = company;
        _webRootPath = webRootPath;
    }

    public byte[] Generate(bool debug = false)
    {
        var templatePath = Path.Combine(_webRootPath, "Bir", "1601CfinalJan2018.pdf");
        return PdfOverlayEngine.Render(templatePath, Bir1601CFieldMap.Fields, BuildValues(), debug);
    }

    private Dictionary<string, string?> BuildValues() => new()
    {
        ["Period"] = $"{_data.PeriodFrom:MMM dd} – {_data.PeriodTo:MMM dd, yyyy}",

        ["Employer.RegisteredName"] = ReportDocumentStyle.CompanyName(_company),
        ["Employer.TIN"] = _company?.TIN,
        ["Employer.RDOCode"] = _company?.RDOCode,
        ["AmendedReturn"] = _data.AmendedReturn ? "Yes" : "No",

        ["Line15_TotalCompensation"] = ReportDocumentStyle.Money(_data.Line15_TotalCompensation),
        ["Line16A_StatutoryMinimumWage"] = ReportDocumentStyle.Money(_data.Line16A_StatutoryMinimumWage),
        ["Line16B_MWEPremiumPay"] = ReportDocumentStyle.Money(_data.Line16B_MWEPremiumPay),
        ["Line16C_OtherNonTaxable"] = ReportDocumentStyle.Money(_data.Line16C_OtherNonTaxable),
        ["Line17_TotalNonTaxable"] = ReportDocumentStyle.Money(_data.Line17_TotalNonTaxable),
        ["Line18_TaxableCompensation"] = ReportDocumentStyle.Money(_data.Line18_TaxableCompensation),
        ["Line19_TaxWithheld"] = ReportDocumentStyle.Money(_data.Line19_TaxWithheld),

        ["Employer.SignatoryName"] = string.IsNullOrWhiteSpace(_company?.AuthorizedSignatoryName)
            ? null
            : _company.AuthorizedSignatoryName,
        ["Employer.SignatoryTitle"] = string.IsNullOrWhiteSpace(_company?.AuthorizedSignatoryTitle)
            ? null
            : _company.AuthorizedSignatoryTitle,
    };
}
