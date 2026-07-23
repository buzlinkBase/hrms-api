using FluentValidation;
using Hrms.Domain.Entities;

namespace Hrms.Core.Validations;

public class DeductionApplicationValidator : AbstractValidator<DeductionApplication>
{
    public DeductionApplicationValidator(DeductionService _DeductionService,
        EmployeeService _employeeService)
    {

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate).WithMessage("End date must be greater than start date")
            .NotEmpty().WithMessage("End Date is required")
            .NotNull().WithMessage("End Date is required")
            ;

        RuleFor(x => x.StartDate)
          .NotEmpty().WithMessage("Start Date is required")
          .NotNull().WithMessage("Start Date is required")
          ;

        RuleFor(x => x.Breakdown)
         .NotEmpty()
         .When(x => x.Terms > 0)
         .WithMessage("Breakdown is required when Terms > 0");

        RuleFor(x => x.DeductionId)
          .NotEmpty().WithMessage("Deduction type is required")
          .NotNull().WithMessage("Deduction type is required")
          .NotEqual(Guid.Empty).WithMessage("Deduction type is required")
          .MustAsync(async (x, ct) => await _DeductionService.FineOneAsync(x, ct) != null).WithMessage("Deduction type is required")
          ;

        RuleFor(x => x.EmployeeId)
           .NotEmpty().WithMessage("employee is required")
           .NotNull().WithMessage("employee is required")
           .NotEqual(Guid.Empty).WithMessage("employee is required")
           .MustAsync(async (x, ct) => await _employeeService.FineOneAsync(x, ct) != null).WithMessage("employee is required")
           ;

    }
}
