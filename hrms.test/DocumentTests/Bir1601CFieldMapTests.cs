using Hrms.Api.Documents.Bir;
using Hrms.Domain.ValueObjects;
using PdfSharp.Pdf.IO;

namespace hrms.test.DocumentTests;

/// <summary>
/// Bir1601CFieldMap -- the coordinate map PdfOverlayEngine draws MonthlyRemittanceReturnModel's
/// values onto the real wwwroot/Bir/1601CfinalJan2018.pdf template with.
/// </summary>
public class Bir1601CFieldMapTests
{
    // Not expected to appear as a printed field: report-metadata/validation-warning properties
    // (not boxes on the actual government form), plus PeriodFrom/PeriodTo, which are combined
    // into the single "Period" field rather than mapped 1:1.
    private static readonly string[] ExcludedModelProperties =
        ["EmployeeCount", "HasUnwithheldTaxWarning", "UnclassifiedEmployeeCount", "PeriodFrom", "PeriodTo"];

    [Fact]
    public void HasNoDuplicateFieldNames()
    {
        Bir1601CFieldMap.Fields.Select(f => f.Name).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void EveryFieldPageIndex_IsWithinTheRealTemplatesPageCount()
    {
        var templatePath = Path.Combine(TestPaths.HrmsApiWebRoot(), "Bir", "1601CfinalJan2018.pdf");
        using var pdf = PdfReader.Open(templatePath, PdfDocumentOpenMode.ReadOnly);

        foreach (var field in Bir1601CFieldMap.Fields)
        {
            field.Page.Should().BeInRange(0, pdf.Pages.Count - 1,
                because: $"field '{field.Name}' must reference a page that exists on the real template");
        }
    }

    [Fact]
    public void EveryPrintableModelProperty_HasAMappedField()
    {
        var mappedNames = Bir1601CFieldMap.Fields.Select(f => f.Name).ToHashSet();
        var modelProperties = typeof(MonthlyRemittanceReturnModel).GetProperties()
            .Select(p => p.Name)
            .Where(name => !ExcludedModelProperties.Contains(name));

        foreach (var propertyName in modelProperties)
        {
            mappedNames.Should().Contain(
                name => name == propertyName || name.EndsWith("." + propertyName),
                $"MonthlyRemittanceReturnModel.{propertyName} should have a corresponding field in Bir1601CFieldMap");
        }
    }
}
