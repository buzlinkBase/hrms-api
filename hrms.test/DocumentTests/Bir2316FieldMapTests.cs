using Hrms.Api.Documents.Bir;
using Hrms.Domain.ValueObjects;
using PdfSharp.Pdf.IO;

namespace hrms.test.DocumentTests;

/// <summary>
/// Bir2316FieldMap -- the coordinate map PdfOverlayEngine draws Bir2316Model's values onto the
/// real wwwroot/Bir/2316Sep2021ENCS_Final.pdf template with. Catches the two ways a field map
/// can silently go wrong: a page index that doesn't exist on the real template, and a data
/// field nobody remembered to map after adding it to Bir2316Model.
/// </summary>
public class Bir2316FieldMapTests
{
    // Bir2316Model properties that are NOT expected to appear as a printed field on the form --
    // EmployeeId is an internal identifier, not certificate content; EmployeeNo and CivilStatus
    // have no matching box on the real 2316 form (confirmed via visual calibration -- see
    // Bir2316FieldMap); TotalSSS/TotalPhilHealth/TotalPagIbig are combined into the single
    // "GovtContributions" field rather than mapped 1:1, since the real form has one shared box
    // for "SSS, GSIS, PHIC, HDMF Mandatory Contributions & Union Dues", not three.
    private static readonly string[] ExcludedModelProperties =
        ["EmployeeId", "EmployeeNo", "CivilStatus", "TotalSSS", "TotalPhilHealth", "TotalPagIbig"];

    [Fact]
    public void HasNoDuplicateFieldNames()
    {
        Bir2316FieldMap.Fields.Select(f => f.Name).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void EveryFieldPageIndex_IsWithinTheRealTemplatesPageCount()
    {
        var templatePath = Path.Combine(TestPaths.HrmsApiWebRoot(), "Bir", "2316Sep2021ENCS_Final.pdf");
        using var pdf = PdfReader.Open(templatePath, PdfDocumentOpenMode.ReadOnly);

        foreach (var field in Bir2316FieldMap.Fields)
        {
            field.Page.Should().BeInRange(0, pdf.Pages.Count - 1,
                because: $"field '{field.Name}' must reference a page that exists on the real template");
        }
    }

    [Fact]
    public void EveryPrintableModelProperty_HasAMappedField()
    {
        var mappedNames = Bir2316FieldMap.Fields.Select(f => f.Name).ToHashSet();
        var modelProperties = typeof(Bir2316Model).GetProperties()
            .Select(p => p.Name)
            .Where(name => !ExcludedModelProperties.Contains(name));

        foreach (var propertyName in modelProperties)
        {
            mappedNames.Should().Contain(
                name => name == propertyName || name.EndsWith("." + propertyName),
                $"Bir2316Model.{propertyName} should have a corresponding field in Bir2316FieldMap");
        }
    }
}
