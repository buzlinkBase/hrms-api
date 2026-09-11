using Hrms.Domain.Entities;
using Mapster;

namespace hrms.test.ServiceTests;

/// <summary>
/// Regression coverage for a bug where "Deduction Type" silently never persisted from the admin
/// Deduction setup form, and never came back on read (so the edit form couldn't pre-fill it
/// either). Root cause: the entity field is named CategoryId, but the DTOs use DeductionTypeId --
/// bare Mapster convention mapping only copies identically-named members, so it dropped the value
/// both ways until MappingProfile added an explicit .Map() for each direction.
/// </summary>
public class DeductionServiceTests
{
    public DeductionServiceTests()
    {
        TypeAdapterConfig.GlobalSettings.Scan(typeof(MappingProfile).Assembly);
    }

    [Fact]
    public void CreateDeduction_MapsDeductionTypeIdToCategoryId()
    {
        var deductionTypeId = Guid.NewGuid();
        var payload = new CreateDeduction
        {
            Code = "SSSLOAN",
            Name = "SSS Loan",
            Status = "ACTIVE",
            DeductionTypeId = deductionTypeId,
        };

        var entity = payload.Adapt<Deduction>();

        entity.CategoryId.Should().Be(deductionTypeId);
    }

    [Fact]
    public void UpdateDeduction_MapsDeductionTypeIdToCategoryId()
    {
        var deductionTypeId = Guid.NewGuid();
        var payload = new UpdateDeduction
        {
            Id = Guid.NewGuid(),
            Code = "SSSLOAN",
            Name = "SSS Loan",
            Status = "ACTIVE",
            DeductionTypeId = deductionTypeId,
        };
        var existing = new Deduction { Id = payload.Id, Code = "OLD", Name = "Old Name" };

        payload.Adapt(existing);

        existing.CategoryId.Should().Be(deductionTypeId);
    }

    [Fact]
    public void Deduction_MapsCategoryIdBackToDeductionTypeId()
    {
        var categoryId = Guid.NewGuid();
        var entity = new Deduction { Code = "SSSLOAN", Name = "SSS Loan", CategoryId = categoryId };

        var model = entity.Adapt<DeductionModel>();

        model.DeductionTypeId.Should().Be(categoryId);
    }
}
