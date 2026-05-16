using FluentValidation;
using Hrms.Domain.Entities;

namespace Hrms.Core.Validations;

public class BranchValidator : AbstractValidator<Branch>
{
    public BranchValidator(IUnitOfWorkService uow,
        ITenantProvider tenantProvider)
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Code is required.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Branch name is required.");

        RuleFor(x => x.Code)
            .Must(x => uow.Repository.Find<Branch>(xx => xx.Code == x).Any())
            .WithMessage("Code is already exists");

    }
}
