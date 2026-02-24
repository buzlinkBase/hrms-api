namespace Hrms.Core.Policies;

public class DeductionValidator
{
    public static bool CanApply(decimal forDeduction, DeductionPipeData line, DeductionPayloadContext context)
    {
        var remainingGross = line.RemainingGrossBalance - forDeduction;
        return remainingGross > forDeduction;
    }
}
