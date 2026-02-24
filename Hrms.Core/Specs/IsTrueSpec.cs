namespace Hrms.Core.Specs;

public class IsTrueSpec<Context> : IPayrollSpec<Context>
{
    public bool IsSatisfiedBy(Context context) => true;
}

public class IsFalse<Context> : IPayrollSpec<Context>
{
    public bool IsSatisfiedBy(Context context) => false;
}
