using Hrms.Api.Documents;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using PdfSharp.Pdf.IO;

namespace hrms.test.DocumentTests;

/// <summary>
/// Smoke tests for Bir2316OverlayDocument -- the automated ceiling for verifying this class:
/// confirms it produces a well-formed PDF with the expected page count. Whether each value
/// actually lands in the right box on the real form can't be asserted by a unit test; that
/// needs manual visual verification (see Bir2316FieldMap's calibration note).
/// </summary>
public class Bir2316OverlayDocumentTests
{
    private static Bir2316Model SampleData() => new()
    {
        EmployeeId = Guid.NewGuid(),
        EmployeeNo = "EMP-0001",
        FullName = "Dela Cruz, Juan S.",
        TIN = "123-456-789-000",
        RDOCode = "044",
        Address = "123 Sample St., Quezon City",
        CivilStatus = "Single",
        Year = 2026,
        GrossCompensation = 360_000m,
        NonTaxableCompensation = 30_000m,
        TaxableCompensation = 300_000m,
        ThirteenthMonthPay = 30_000m,
        TotalSSS = 14_400m,
        TotalPhilHealth = 9_000m,
        TotalPagIbig = 2_400m,
        TotalTaxWithheld = 25_000m,
    };

    private static Company SampleCompany() => new()
    {
        Description = "Acme Corp",
        TIN = "000-111-222-000",
        RDOCode = "039",
        Address = "456 Business Ave., Makati City",
        AuthorizedSignatoryName = "Maria Santos",
        AuthorizedSignatoryTitle = "HR Director",
    };

    [Fact]
    public void Generate_ProducesAWellFormedPdf_WithOnePage()
    {
        var document = new Bir2316OverlayDocument(SampleData(), SampleCompany(), TestPaths.HrmsApiWebRoot());

        var bytes = document.Generate();

        bytes.Should().NotBeEmpty();
        using var pdf = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.ReadOnly);
        pdf.Pages.Count.Should().Be(1);
    }

    [Fact]
    public void Generate_DoesNotThrow_WhenCompanyIsNull()
    {
        var document = new Bir2316OverlayDocument(SampleData(), company: null, TestPaths.HrmsApiWebRoot());

        var act = () => document.Generate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Generate_DebugMode_StillProducesAWellFormedPdf()
    {
        var document = new Bir2316OverlayDocument(SampleData(), SampleCompany(), TestPaths.HrmsApiWebRoot());

        var bytes = document.Generate(debug: true);

        using var pdf = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.ReadOnly);
        pdf.Pages.Count.Should().Be(1);
    }
}
