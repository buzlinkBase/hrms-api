using FluentValidation;
using Hrms.Domain.Entities;

namespace Hrms.Core.Validations;

public class DepartmentValidator : AbstractValidator<Department>
{
    public DepartmentValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Code is required.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Department name is required.");
    }
}
