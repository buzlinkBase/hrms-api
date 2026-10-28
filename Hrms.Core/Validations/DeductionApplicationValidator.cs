using FluentValidation;
using Hrms.Domain.Entities;

namespace Hrms.Core.Validations;

public class DeductionApplicationValidator : AbstractValidator<DeductionApplication>
{
    public DeductionApplicationValidator(DeductionService _DeductionService,
        EmployeeService _employeeService)
    {

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate).WithMessage("End date must not be earlier than start date")
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

        // Attendance pay modes only make sense for a flat charge: the deduction must be flagged for
        // them in setup, and there can be no interest (a skipped period has no interest to skip).
        When(x => x.PayMode != OtherIncomePayMode.FixedSchedule, () =>
        {
            RuleFor(x => x.DeductionId)
                .MustAsync(async (id, ct) => (await _DeductionService.FineOneAsync(id, ct))?.AllowAttendanceRules == true)
                .WithMessage("This deduction isn't set up for attendance-based rules. Turn it on in Setup > Deduction first.");
            RuleFor(x => x.InterestRate).Equal(0m)
                .WithMessage("An attendance-based deduction can't carry interest");
            RuleFor(x => x.AttendanceBasis).NotEqual(OtherIncomeAttendanceBasis.None)
                .WithMessage("Choose what counts as present");
            RuleFor(x => x.MinHours).Must(h => h is null or (> 0 and <= 24))
                .WithMessage("Minimum hours must be between 0 and 24");
            RuleFor(x => x.MaxLateMinutes).Must(m => m is null or >= 0)
                .WithMessage("Late limit can't be negative");
            RuleFor(x => x.MaxUndertimeMinutes).Must(m => m is null or >= 0)
                .WithMessage("Undertime limit can't be negative");
            RuleFor(x => x.ApplicableDays).Must(d => (d & (int)OtherIncomeWeekdays.All) != 0)
                .WithMessage("Select at least one applicable day");
            RuleFor(x => x.DayTypes).Must(d => (d & (int)OtherIncomeDayTypes.All) != 0)
                .WithMessage("Select at least one day type");
            RuleFor(x => x.MinQualifyingDays).Must(d => d is null or > 0)
                .WithMessage("Minimum qualifying days must be at least 1");
        });
        RuleFor(x => x.FrequencyOfPayment)
            .Must(f => f is DeductionFrequency.Weekly or DeductionFrequency.SemiMonthly or DeductionFrequency.Monthly)
            .When(x => x.PayMode == OtherIncomePayMode.PeriodicGated)
            .WithMessage("A periodic attendance-gated deduction must be weekly, semi-monthly or monthly");

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
