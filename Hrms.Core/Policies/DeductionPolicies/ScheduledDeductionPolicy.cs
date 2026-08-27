
namespace Hrms.Core.Policies;

internal class ScheduledDeductionPolicy : PayrollPolicyBase<DeductionPipeData, DeductionPayloadContext>
{
    public override DeductionPipeData ApplyIfSatisfied(DeductionPipeData line, DeductionPayloadContext context)
    {
        context.Payload.Deductions.TryGetValue(new EmployeeKey(context.Employee.Id), out var deductions);
        if (line.IsLimit || deductions == null || !deductions.Any()) return line;

        var processDeductions = new List<DeductionInfo>();
        foreach (var deduction in deductions)
        {
            deduction.Type = DeductionInfoType.Others;
            var MinimumTakeHomeAmount = context.Payload.CompanyPolicy.RequiredTakehomePercentage;
            if (!DeductionValidator.CanApply(deduction.Amount, line, context))
            {
                line.IsLimit = true;
                break;
            }

            line.RemainingGrossBalance -= deduction.Amount;
            processDeductions.Add(deduction);
        }

        line.RunningTotal += processDeductions.Sum(x => x.Amount);
        line.ScheduledDeductions = processDeductions;
        return line;

    }
}

