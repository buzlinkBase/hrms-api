using FluentValidation;
using Hrms.Core.Validations;
using Hrms.Domain.Entities;

namespace hrms.test.ServiceTests;

/// <summary>
/// The Loan/Deduction form derives EndDate from the last installment date, so a one-term loan
/// legitimately ends on its start date. The validator must accept that and only reject an
/// EndDate that falls before StartDate.
/// </summary>
public class DeductionApplicationValidatorTests
{
    private static async Task<FluentValidation.Results.ValidationResult> ValidateEndDateAsync(
        DateOnly start,
        DateOnly end)
    {
        // Only the EndDate rule runs, so the service dependencies are never touched.
        var validator = new DeductionApplicationValidator(null!, null!);
        var model = new DeductionApplication { StartDate = start, EndDate = end };

        return await validator.ValidateAsync(
            model,
            options => options.IncludeProperties(x => x.EndDate));
    }

    [Fact]
    public async Task EndDate_EqualToStartDate_IsValid()
    {
        var day = new DateOnly(2026, 10, 15);

        var result = await ValidateEndDateAsync(day, day);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task EndDate_AfterStartDate_IsValid()
    {
        var result = await ValidateEndDateAsync(new DateOnly(2026, 10, 15), new DateOnly(2026, 11, 15));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task EndDate_BeforeStartDate_IsInvalid()
    {
        var result = await ValidateEndDateAsync(new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 14));

        result.IsValid.Should().BeFalse();
    }
}
