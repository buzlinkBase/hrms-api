using FluentValidation;
using Hrms.Domain.Entities;

namespace Hrms.Core.Validations;

public class CompanyValidator : AbstractValidator<Company>
{
    public CompanyValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Code is required.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Company name is required.");

    }
}
