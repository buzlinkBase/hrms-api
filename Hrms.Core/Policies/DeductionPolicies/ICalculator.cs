namespace Hrms.Core;

public interface ICalculator<T, U> where U : class, new() where T : class, new()
{
    T Calculate(U context);
}
public interface IPolicyCalculator<R, C, L>
{
    R Calculate(C context, L line);
}

public interface IDeductionCalculator : IPolicyCalculator<DeductionPipeData, DeductionPayloadContext, DeductionPipeData> { }
