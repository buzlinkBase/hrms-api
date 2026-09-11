using Hrms.Api.Documents;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using PdfSharp.Pdf.IO;

namespace hrms.test.DocumentTests;

/// <summary>
/// Smoke tests for MonthlyRemittanceReturnOverlayDocument -- same automated ceiling as
/// Bir2316OverlayDocumentTests (well-formed PDF, expected page count; field placement needs
/// manual visual verification).
/// </summary>
public class MonthlyRemittanceReturnOverlayDocumentTests
{
    private static MonthlyRemittanceReturnModel SampleData() => new()
    {
        PeriodFrom = new DateOnly(2026, 9, 1),
        PeriodTo = new DateOnly(2026, 9, 30),
        AmendedReturn = false,
        EmployeeCount = 5,
        Line15_TotalCompensation = 500_000m,
        Line16A_StatutoryMinimumWage = 50_000m,
        Line16B_MWEPremiumPay = 5_000m,
        Line16C_OtherNonTaxable = 10_000m,
        Line17_TotalNonTaxable = 65_000m,
        Line18_TaxableCompensation = 435_000m,
        Line19_TaxWithheld = 40_000m,
        HasUnwithheldTaxWarning = false,
        UnclassifiedEmployeeCount = 0,
    };

    private static Company SampleCompany() => new()
    {
        Description = "Acme Corp",
        TIN = "000-111-222-000",
        RDOCode = "039",
        AuthorizedSignatoryName = "Maria Santos",
        AuthorizedSignatoryTitle = "HR Director",
    };

    [Fact]
    public void Generate_ProducesAWellFormedPdf_WithTwoPages()
    {
        var document = new MonthlyRemittanceReturnOverlayDocument(SampleData(), SampleCompany(), TestPaths.HrmsApiWebRoot());

        var bytes = document.Generate();

        bytes.Should().NotBeEmpty();
        using var pdf = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.ReadOnly);
        pdf.Pages.Count.Should().Be(2);
    }

    [Fact]
    public void Generate_DoesNotThrow_WhenCompanyIsNull()
    {
        var document = new MonthlyRemittanceReturnOverlayDocument(SampleData(), company: null, TestPaths.HrmsApiWebRoot());

        var act = () => document.Generate();

        act.Should().NotThrow();
    }
}
