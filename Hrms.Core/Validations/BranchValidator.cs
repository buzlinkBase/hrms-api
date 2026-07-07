using FluentValidation;
using Hrms.Domain.Entities;

namespace Hrms.Core.Validations;

public class BranchValidator : AbstractValidator<Branch>
{
    public BranchValidator( )
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Code is required.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Branch name is required.");
    }
}
