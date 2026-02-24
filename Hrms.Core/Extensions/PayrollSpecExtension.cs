namespace Hrms.Core.Extensions;

public static class PayrollSpecExtension
{
    public static IPayrollSpec<T> Or<T>(this IPayrollSpec<T> spec, IPayrollSpec<T> other) =>
        new OrPayrollSpec<T>(spec, other);

    public static IPayrollSpec<T> And<T>(this IPayrollSpec<T> spec, IPayrollSpec<T> other) =>
        new AndPayrollSpec<T>(spec, other);

    public static IPayrollSpec<T> Not<T>(this IPayrollSpec<T> spec) =>
        new NotPayrollSpec<T>(spec);

    public static IPayrollSpec<T> AndNot<T>(this IPayrollSpec<T> spec, IPayrollSpec<T> other) =>
        new AndPayrollSpec<T>(spec, new NotPayrollSpec<T>(other));

    public static IPayrollSpec<T> OrNot<T>(this IPayrollSpec<T> spec, IPayrollSpec<T> other) =>
        new OrPayrollSpec<T>(spec, new NotPayrollSpec<T>(other));

}
