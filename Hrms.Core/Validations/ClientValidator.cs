using FluentValidation;
using Hrms.Domain.Entities;

namespace Hrms.Core.Validations;

public class ClientValidator : AbstractValidator<Client>
{
    public ClientValidator()
    {
        //RuleFor(x => x.Code).NotEmpty().WithMessage("Code is required.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Client name is required.");
    }
}
